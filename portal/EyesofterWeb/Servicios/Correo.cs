using System.Net.Http.Json;

namespace EyesofterWeb.Servicios
{
    public interface ICorreo
    {
        Task<bool> EnviarAsync(string destino, string asunto, string html);
    }

    /// <summary>
    /// El proveedor que se usa según las variables de Railway:
    ///  - BREVO_API_KEY → Brevo (no necesita dominio propio: el remitente puede ser un Gmail verificado en Brevo).
    ///  - RESEND_API_KEY → Resend (para enviar a cualquiera necesita un dominio verificado en Resend).
    /// Las dos usan CORREO_REMITENTE ("EYESOFTER &lt;avisos@ejemplo.com&gt;" o solo el correo). Si están las dos, gana Brevo.
    /// El servidor de la app (eyesofterandroid) lee las mismas variables.
    /// </summary>
    public class CorreoConfigurado : ICorreo
    {
        private readonly ICorreo? _proveedor;
        private readonly ILogger<CorreoConfigurado> _log;
        private readonly IHostEnvironment _entorno;

        public CorreoConfigurado(IHttpClientFactory http, IConfiguration config, IHostEnvironment entorno, ILoggerFactory logs)
        {
            _log = logs.CreateLogger<CorreoConfigurado>();
            _entorno = entorno;
            _proveedor = Proveedor(config) switch
            {
                "Brevo" => new CorreoBrevo(http, config, logs.CreateLogger<CorreoBrevo>()),
                "Resend" => new CorreoResend(http, config, entorno, logs.CreateLogger<CorreoResend>()),
                _ => null
            };
        }

        /// <summary>"Brevo", "Resend" o null (sin correo).</summary>
        public static string? Proveedor(IConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config["CORREO_REMITENTE"])) return null;
            if (!string.IsNullOrWhiteSpace(config["BREVO_API_KEY"])) return "Brevo";
            if (!string.IsNullOrWhiteSpace(config["RESEND_API_KEY"])) return "Resend";
            return null;
        }

        public Task<bool> EnviarAsync(string destino, string asunto, string html)
        {
            if (_proveedor is not null) return _proveedor.EnviarAsync(destino, asunto, html);
            if (_entorno.IsDevelopment()) _log.LogWarning("Correo NO enviado (sin BREVO_API_KEY ni RESEND_API_KEY). Para {Destino}: {Asunto}\n{Html}", destino, asunto, html);
            else _log.LogError("No se pudo enviar correo a {Destino}: falta BREVO_API_KEY (o RESEND_API_KEY) y CORREO_REMITENTE", destino);
            return Task.FromResult(false);
        }

        /// <summary>"EYESOFTER &lt;avisos@ejemplo.com&gt;" → ("EYESOFTER", "avisos@ejemplo.com"); "avisos@ejemplo.com" → ("EYESOFTER", "avisos@ejemplo.com").</summary>
        public static (string Nombre, string Correo) Remitente(string remitente)
        {
            var r = remitente.Trim();
            var abre = r.LastIndexOf('<');
            var cierra = r.LastIndexOf('>');
            if (abre >= 0 && cierra > abre)
            {
                var nombre = r[..abre].Trim().Trim('"').Trim();
                return (nombre.Length > 0 ? nombre : "EYESOFTER", r[(abre + 1)..cierra].Trim());
            }
            return ("EYESOFTER", r);
        }
    }

    /// <summary>
    /// Correo con la API HTTP de Brevo (gratis hasta 300 correos al día). No pide dominio propio: basta
    /// con verificar en Brevo el correo remitente (Senders → Add a sender), p. ej. un Gmail del proyecto.
    /// HTTP y no SMTP: Railway bloquea SMTP en sus planes básicos.
    /// </summary>
    public class CorreoBrevo : ICorreo
    {
        public const string Url = "https://api.brevo.com/v3/smtp/email";
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly ILogger<CorreoBrevo> _log;

        public CorreoBrevo(IHttpClientFactory http, IConfiguration config, ILogger<CorreoBrevo> log)
        {
            _http = http;
            _config = config;
            _log = log;
        }

        public async Task<bool> EnviarAsync(string destino, string asunto, string html)
        {
            var clave = _config["BREVO_API_KEY"];
            var remitente = _config["CORREO_REMITENTE"];
            if (string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(remitente))
            {
                _log.LogError("No se pudo enviar correo a {Destino}: falta BREVO_API_KEY o CORREO_REMITENTE", destino);
                return false;
            }
            var (nombre, correo) = CorreoConfigurado.Remitente(remitente);
            var cliente = _http.CreateClient();
            cliente.Timeout = TimeSpan.FromSeconds(15);
            using var peticion = new HttpRequestMessage(HttpMethod.Post, Url)
            {
                Content = JsonContent.Create(new
                {
                    sender = new { name = nombre, email = correo },
                    to = new[] { new { email = destino } },
                    subject = asunto,
                    htmlContent = html
                })
            };
            peticion.Headers.Add("api-key", clave.Trim());
            peticion.Headers.Accept.ParseAdd("application/json");
            try
            {
                var r = await cliente.SendAsync(peticion);
                if (!r.IsSuccessStatusCode)
                {
                    var detalle = await r.Content.ReadAsStringAsync();
                    _log.LogError("Brevo respondió {Codigo} al enviar a {Destino}: {Detalle}", (int)r.StatusCode, destino, detalle.Length > 300 ? detalle[..300] : detalle);
                }
                return r.IsSuccessStatusCode;
            }
            catch (Exception e)
            {
                _log.LogError(e, "Falló el envío de correo a {Destino}", destino);
                return false;
            }
        }
    }

    /// <summary>
    /// Correo con la API de Resend (C-RI-02): recuperación de contraseña. Mismas variables que
    /// el servidor de la app (RESEND_API_KEY, CORREO_REMITENTE). Sin configurar, solo se registra
    /// en el log que no se pudo enviar (en desarrollo se escribe el enlace para probar).
    /// </summary>
    public class CorreoResend : ICorreo
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly IHostEnvironment _entorno;
        private readonly ILogger<CorreoResend> _log;

        public CorreoResend(IHttpClientFactory http, IConfiguration config, IHostEnvironment entorno, ILogger<CorreoResend> log)
        {
            _http = http;
            _config = config;
            _entorno = entorno;
            _log = log;
        }

        public async Task<bool> EnviarAsync(string destino, string asunto, string html)
        {
            var clave = _config["RESEND_API_KEY"];
            var remitente = _config["CORREO_REMITENTE"];
            if (string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(remitente))
            {
                if (_entorno.IsDevelopment()) _log.LogWarning("Correo NO enviado (sin RESEND_API_KEY). Para {Destino}: {Asunto}\n{Html}", destino, asunto, html);
                else _log.LogError("No se pudo enviar correo a {Destino}: falta RESEND_API_KEY o CORREO_REMITENTE", destino);
                return false;
            }
            var cliente = _http.CreateClient();
            cliente.Timeout = TimeSpan.FromSeconds(15);
            using var peticion = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
            {
                Content = JsonContent.Create(new { from = remitente, to = new[] { destino }, subject = asunto, html })
            };
            peticion.Headers.Authorization = new("Bearer", clave);
            try
            {
                var r = await cliente.SendAsync(peticion);
                if (!r.IsSuccessStatusCode) _log.LogError("Resend respondió {Codigo} al enviar a {Destino}", (int)r.StatusCode, destino);
                return r.IsSuccessStatusCode;
            }
            catch (Exception e)
            {
                _log.LogError(e, "Falló el envío de correo a {Destino}", destino);
                return false;
            }
        }
    }
}
