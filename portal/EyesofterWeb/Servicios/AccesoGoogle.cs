using System.Security.Claims;
using System.Text.Encodings.Web;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EyesofterWeb.Servicios
{
    /// <summary>Por qué no se pudo entrar con Google (cada motivo tiene su mensaje para la persona).</summary>
    public enum RechazoGoogle
    {
        /// <summary>Google no mandó el correo de la cuenta.</summary>
        SinCorreo,
        /// <summary>Google no confirma que el correo sea de esa persona.</summary>
        CorreoNoVerificado,
        /// <summary>Ese correo no tiene cuenta en EYESOFTER (no hay registro público, C-RC-06).</summary>
        SinCuenta,
        /// <summary>La cuenta de EYESOFTER está desactivada.</summary>
        CuentaDesactivada,
        /// <summary>La cuenta de EYESOFTER ya está vinculada con OTRA cuenta de Google.</summary>
        OtraCuentaGoogle,
        /// <summary>Esa cuenta de Google ya está vinculada con OTRA cuenta de EYESOFTER.</summary>
        GoogleDeOtraCuenta
    }

    /// <summary>Cuenta de EYESOFTER que corresponde a la cuenta de Google, o por qué no hay ninguna.</summary>
    public record ResultadoGoogle(ApplicationUser? Usuario, bool RecienVinculada = false, RechazoGoogle? Rechazo = null)
    {
        public bool Exito => Usuario is not null && Rechazo is null;
        public static ResultadoGoogle Rechazado(RechazoGoogle motivo) => new(null, false, motivo);
    }

    /// <summary>
    /// "Iniciar sesión con Google" (OAuth 2.0 / OpenID Connect).
    ///
    /// Google solo dice QUIÉN es la persona; las cuentas las sigue creando la Dirección (no hay
    /// registro público, C-RC-06). Reglas:
    ///  - La primera vez, la cuenta de Google se vincula con la cuenta de EYESOFTER que tiene el MISMO
    ///    correo, solo si Google confirma que el correo es suyo (email_verified).
    ///  - Después se reconoce por el identificador de Google (no por el correo), aunque cambie su correo de Gmail.
    ///  - Una cuenta de EYESOFTER se vincula con UNA sola cuenta de Google.
    ///  - Al vincular se manda un correo de aviso a la cuenta (por si no fue la persona).
    ///  - Las cuentas desactivadas no entran; el bloqueo por intentos y la verificación en dos pasos
    ///    del personal se siguen aplicando (lo hace SignInManager en el controlador).
    /// </summary>
    public class AccesoGoogle
    {
        /// <summary>Nombre del esquema y del proveedor guardado en asp_net_user_logins.</summary>
        public const string Proveedor = "Google";
        public const string NombreVisible = "Google";
        /// <summary>Claim con el "email_verified" de https://www.googleapis.com/oauth2/v3/userinfo.</summary>
        public const string ClaimCorreoVerificado = "email_verified";

        private readonly UserManager<ApplicationUser> _usuarios;
        private readonly ApplicationDbContext _db;
        private readonly Auditoria _auditoria;
        private readonly ICorreo _correo;
        private readonly OpcionesEyesofter _opciones;
        private readonly ILogger<AccesoGoogle> _log;

        public AccesoGoogle(UserManager<ApplicationUser> usuarios, ApplicationDbContext db, Auditoria auditoria, ICorreo correo,
                            IOptions<OpcionesEyesofter> opciones, ILogger<AccesoGoogle> log)
        {
            _usuarios = usuarios;
            _db = db;
            _auditoria = auditoria;
            _correo = correo;
            _opciones = opciones.Value;
            _log = log;
        }

        // ---------- Configuración ----------

        /// <summary>
        /// ID y secreto del cliente OAuth (Google Cloud → APIs y servicios → Credenciales).
        /// Variables GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET (Railway) o Authentication:Google:ClientId / ClientSecret.
        /// </summary>
        public static (string? ClientId, string? ClientSecret) Credenciales(IConfiguration config)
        {
            static string? Valor(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            return (Valor(config["GOOGLE_CLIENT_ID"]) ?? Valor(config["Authentication:Google:ClientId"]),
                    Valor(config["GOOGLE_CLIENT_SECRET"]) ?? Valor(config["Authentication:Google:ClientSecret"]));
        }

        /// <summary>Sin las dos variables no se registra Google ni se muestra el botón.</summary>
        public static bool Configurado(IConfiguration config)
        {
            var (id, secreto) = Credenciales(config);
            return id is not null && secreto is not null;
        }

        // ---------- Lo que mandó Google ----------

        /// <summary>Correo de la cuenta de Google (en minúsculas y sin espacios), o null si no vino.</summary>
        public static string? Correo(ClaimsPrincipal principal)
        {
            var correo = principal.FindFirstValue(ClaimTypes.Email)?.Trim();
            return string.IsNullOrEmpty(correo) || !correo.Contains('@') ? null : correo.ToLowerInvariant();
        }

        /// <summary>Google confirma que el correo es de esa persona (email_verified = true).</summary>
        public static bool CorreoVerificado(ClaimsPrincipal principal) =>
            bool.TryParse(principal.FindFirstValue(ClaimCorreoVerificado), out var verificado) && verificado;

        // ---------- Reglas ----------

        /// <summary>
        /// Busca (y si es la primera vez, vincula) la cuenta de EYESOFTER de esta cuenta de Google.
        /// No inicia sesión: eso lo hace el controlador con SignInManager.ExternalLoginSignInAsync.
        /// </summary>
        public async Task<ResultadoGoogle> ResolverAsync(ExternalLoginInfo info)
        {
            // 1. Ya vinculada: se reconoce por el identificador de Google ("sub"), no por el correo.
            var vinculada = await _usuarios.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (vinculada is not null)
            {
                if (vinculada.Activo) return new ResultadoGoogle(vinculada);
                await RegistrarRechazoAsync(RechazoGoogle.CuentaDesactivada, vinculada.Email, vinculada.Id);
                return ResultadoGoogle.Rechazado(RechazoGoogle.CuentaDesactivada);
            }

            // 2. Primera vez: por el correo, solo si Google lo verificó.
            var correo = Correo(info.Principal);
            if (correo is null) return await RechazarAsync(RechazoGoogle.SinCorreo, null);
            if (!CorreoVerificado(info.Principal)) return await RechazarAsync(RechazoGoogle.CorreoNoVerificado, correo);

            var usuario = await _usuarios.FindByEmailAsync(correo);
            if (usuario is null) return await RechazarAsync(RechazoGoogle.SinCuenta, correo);
            if (!usuario.Activo) return await RechazarAsync(RechazoGoogle.CuentaDesactivada, correo, usuario.Id);

            return await VincularInternoAsync(usuario, info, correo);
        }

        /// <summary>
        /// Vincular desde "Mi cuenta → Google" con la sesión ya iniciada. Aquí la cuenta de Google puede
        /// tener otro correo (p. ej. la escuela registró el correo del trabajo y el tutor usa Gmail):
        /// la persona ya demostró quién es al entrar con su contraseña (y su código, si es personal).
        /// </summary>
        public async Task<ResultadoGoogle> VincularAsync(ApplicationUser usuario, ExternalLoginInfo info)
        {
            var duenio = await _usuarios.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (duenio is not null)
                return duenio.Id == usuario.Id
                    ? new ResultadoGoogle(usuario)
                    : await RechazarAsync(RechazoGoogle.GoogleDeOtraCuenta, usuario.Email, usuario.Id);
            return await VincularInternoAsync(usuario, info, Correo(info.Principal) ?? "(sin correo)");
        }

        private async Task<ResultadoGoogle> VincularInternoAsync(ApplicationUser usuario, ExternalLoginInfo info, string correoGoogle)
        {
            var logins = await _usuarios.GetLoginsAsync(usuario);
            if (logins.Any(l => l.LoginProvider == info.LoginProvider))
                return await RechazarAsync(RechazoGoogle.OtraCuentaGoogle, correoGoogle, usuario.Id);

            var r = await _usuarios.AddLoginAsync(usuario, new UserLoginInfo(info.LoginProvider, info.ProviderKey, NombreVisible));
            if (!r.Succeeded)
            {
                // Otra petición la vinculó al mismo tiempo: se vuelve a buscar.
                var otra = await _usuarios.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (otra?.Id == usuario.Id) return new ResultadoGoogle(usuario);
                _log.LogWarning("No se pudo vincular Google con la cuenta {Id}: {Errores}", usuario.Id, string.Join("; ", r.Errors.Select(e => e.Code)));
                return await RechazarAsync(otra is null ? RechazoGoogle.OtraCuentaGoogle : RechazoGoogle.GoogleDeOtraCuenta, correoGoogle, usuario.Id);
            }

            _auditoria.Registrar("GOOGLE_VINCULADA", "Usuario", usuario.Id, $"Cuenta de Google {correoGoogle}", usuario: usuario.Email);
            await _db.SaveChangesAsync();
            await AvisarAsync(usuario, "EYESOFTER: vinculaste tu cuenta de Google", HtmlAvisoVinculacion(usuario.NombreCompleto, correoGoogle, _opciones.UrlWeb));
            return new ResultadoGoogle(usuario, RecienVinculada: true);
        }

        /// <summary>¿La cuenta ya entra con Google?</summary>
        public async Task<bool> VinculadaAsync(ApplicationUser usuario) =>
            (await _usuarios.GetLoginsAsync(usuario)).Any(l => l.LoginProvider == Proveedor);

        /// <summary>Quita Google de la cuenta. La contraseña sigue sirviendo (en la web y en la app).</summary>
        public async Task<bool> DesvincularAsync(ApplicationUser usuario)
        {
            var login = (await _usuarios.GetLoginsAsync(usuario)).FirstOrDefault(l => l.LoginProvider == Proveedor);
            if (login is null) return false;
            var r = await _usuarios.RemoveLoginAsync(usuario, login.LoginProvider, login.ProviderKey);
            if (!r.Succeeded) return false;
            _auditoria.Registrar("GOOGLE_DESVINCULADA", "Usuario", usuario.Id, usuario: usuario.Email);
            await _db.SaveChangesAsync();
            await AvisarAsync(usuario, "EYESOFTER: quitaste el inicio de sesión con Google", HtmlAvisoDesvinculacion(usuario.NombreCompleto, _opciones.UrlWeb));
            return true;
        }

        /// <summary>Texto para la persona según el motivo.</summary>
        public static string Mensaje(RechazoGoogle motivo) => motivo switch
        {
            RechazoGoogle.SinCorreo => "Google no compartió el correo de tu cuenta. Vuelve a intentarlo y acepta compartir tu correo.",
            RechazoGoogle.CorreoNoVerificado => "Google no ha verificado el correo de esa cuenta. Verifícalo en Google o entra con tu correo y contraseña.",
            RechazoGoogle.SinCuenta => "Ese correo de Google no tiene cuenta en EYESOFTER. Pide a la Dirección o a Enfermería de la escuela que te den de alta con ese correo, o entra con el correo que te registraron.",
            RechazoGoogle.CuentaDesactivada => "Tu cuenta está desactivada. Comunícate con la Dirección de la escuela.",
            RechazoGoogle.OtraCuentaGoogle => "Tu cuenta de EYESOFTER ya está vinculada con otra cuenta de Google. Entra con esa, o con tu correo y contraseña.",
            RechazoGoogle.GoogleDeOtraCuenta => "Esa cuenta de Google ya está vinculada con otra cuenta de EYESOFTER. Elige otra cuenta de Google.",
            _ => "No se pudo iniciar sesión con Google."
        };

        // ---------- Correos de aviso ----------

        public static string HtmlAvisoVinculacion(string nombre, string correoGoogle, string urlWeb)
        {
            var enc = HtmlEncoder.Default;
            var web = urlWeb.TrimEnd('/');
            return $"<p>Hola, {enc.Encode(nombre)}:</p>" +
                   $"<p>Se vinculó la cuenta de Google <strong>{enc.Encode(correoGoogle)}</strong> con tu cuenta de EYESOFTER. " +
                   "Desde ahora puedes entrar al portal con el botón <em>Iniciar sesión con Google</em>.</p>" +
                   "<p>Tu contraseña sigue funcionando en el portal y en la app.</p>" +
                   "<p><strong>¿No fuiste tú?</strong> Cambia tu contraseña y quita Google en " +
                   $"<a href=\"{enc.Encode(web + "/Cuenta/Google")}\">{enc.Encode(web + "/Cuenta/Google")}</a>, " +
                   "y avisa a la Dirección de la escuela.</p>";
        }

        public static string HtmlAvisoDesvinculacion(string nombre, string urlWeb)
        {
            var enc = HtmlEncoder.Default;
            return $"<p>Hola, {enc.Encode(nombre)}:</p>" +
                   "<p>Se quitó el inicio de sesión con Google de tu cuenta de EYESOFTER. Entra con tu correo y contraseña.</p>" +
                   "<p>Si no fuiste tú, cambia tu contraseña en " +
                   $"<a href=\"{enc.Encode(urlWeb.TrimEnd('/') + "/Cuenta/Olvide")}\">{enc.Encode(urlWeb.TrimEnd('/') + "/Cuenta/Olvide")}</a> y avisa a la Dirección.</p>";
        }

        private async Task AvisarAsync(ApplicationUser usuario, string asunto, string html)
        {
            if (string.IsNullOrWhiteSpace(usuario.Email)) return;
            // Un correo que no sale no impide entrar: queda en el log.
            if (!await _correo.EnviarAsync(usuario.Email, asunto, html))
                _log.LogWarning("No se pudo enviar el aviso \"{Asunto}\" a la cuenta {Id}", asunto, usuario.Id);
        }

        private async Task<ResultadoGoogle> RechazarAsync(RechazoGoogle motivo, string? correo, string? usuarioId = null)
        {
            await RegistrarRechazoAsync(motivo, correo, usuarioId);
            return ResultadoGoogle.Rechazado(motivo);
        }

        private async Task RegistrarRechazoAsync(RechazoGoogle motivo, string? correo, string? usuarioId)
        {
            _auditoria.Registrar("LOGIN_GOOGLE_RECHAZADO", "Usuario", usuarioId, motivo.ToString(), exito: false, usuario: correo);
            try { await _db.SaveChangesAsync(); }
            catch (Exception e) { _log.LogError(e, "No se pudo guardar la bitácora del inicio con Google"); }
        }
    }
}
