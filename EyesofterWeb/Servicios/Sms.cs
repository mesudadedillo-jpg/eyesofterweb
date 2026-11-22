namespace EyesofterWeb.Servicios
{
    public interface ISms
    {
        /// <summary>false = no hay proveedor configurado (entonces el código se manda por correo).</summary>
        bool Disponible { get; }

        /// <param name="numero">10 dígitos (México) o ya en formato internacional (+52...).</param>
        Task<bool> EnviarAsync(string numero, string texto);
    }

    /// <summary>
    /// SMS con la API de Twilio, solo para el código de recuperación de contraseña. Es OPCIONAL:
    /// el correo (Resend) es gratis y es el canal principal. Twilio da crédito de prueba gratis,
    /// pero en la cuenta de prueba solo envía a números verificados; para todos los tutores hay
    /// que cargarle saldo (≈ US$0.05 por SMS a México).
    ///
    /// Variables (las mismas en el servidor de la app): TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN,
    /// TWILIO_NUMERO (remitente, p. ej. +15005550006) y, si no es México, SMS_PREFIJO_PAIS (+52).
    /// </summary>
    public class SmsTwilio : ISms
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly IHostEnvironment _entorno;
        private readonly ILogger<SmsTwilio> _log;

        public SmsTwilio(IHttpClientFactory http, IConfiguration config, IHostEnvironment entorno, ILogger<SmsTwilio> log)
        {
            _http = http;
            _config = config;
            _entorno = entorno;
            _log = log;
        }

        private string? Sid => _config["TWILIO_ACCOUNT_SID"];
        private string? Token => _config["TWILIO_AUTH_TOKEN"];
        private string? Remitente => _config["TWILIO_NUMERO"];

        public bool Disponible => !string.IsNullOrWhiteSpace(Sid) && !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(Remitente);

        /// <summary>"7751234567" → "+527751234567". Si ya trae "+", se deja igual.</summary>
        public static string Internacional(string numero, string prefijo = "+52")
        {
            var limpio = numero.Trim();
            if (limpio.StartsWith('+')) return "+" + new string(limpio.Where(char.IsDigit).ToArray());
            var digitos = new string(limpio.Where(char.IsDigit).ToArray());
            return prefijo + (digitos.Length > 10 ? digitos[^10..] : digitos);
        }

        public async Task<bool> EnviarAsync(string numero, string texto)
        {
            if (!Disponible)
            {
                if (_entorno.IsDevelopment()) _log.LogWarning("SMS NO enviado (sin TWILIO_*). Para {Numero}: {Texto}", numero, texto);
                return false;
            }
            var destino = Internacional(numero, _config["SMS_PREFIJO_PAIS"] ?? "+52");
            var cliente = _http.CreateClient();
            cliente.Timeout = TimeSpan.FromSeconds(15);
            using var peticion = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(Sid!)}/Messages.json")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["To"] = destino, ["From"] = Remitente!, ["Body"] = texto })
            };
            peticion.Headers.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{Sid}:{Token}")));
            try
            {
                var r = await cliente.SendAsync(peticion);
                if (!r.IsSuccessStatusCode) _log.LogError("Twilio respondió {Codigo} al enviar SMS", (int)r.StatusCode);
                return r.IsSuccessStatusCode;
            }
            catch (Exception e)
            {
                _log.LogError(e, "Falló el envío de SMS");
                return false;
            }
        }
    }
}
