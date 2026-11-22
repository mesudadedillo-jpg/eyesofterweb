using System.Net.Http.Json;

namespace EyesofterWeb.Servicios
{
    public interface ICorreo
    {
        Task<bool> EnviarAsync(string destino, string asunto, string html);
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
