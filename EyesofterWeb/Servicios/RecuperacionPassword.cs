using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Servicios
{
    /// <summary>Resultado de cambiar la contraseña con el código. <see cref="Clave"/> es la misma que devuelve la API de la app.</summary>
    public record ResultadoRecuperacion(bool Exito, string? Clave = null, string? Mensaje = null, IReadOnlyList<string>? Errores = null)
    {
        public static readonly ResultadoRecuperacion Correcto = new(true);
    }

    /// <summary>
    /// "¿Olvidaste tu contraseña?" con un código de 6 dígitos al correo o, si hay proveedor de SMS,
    /// al celular (C-RFNN-02, C-RI-02). Las reglas son las MISMAS en el servidor de la app
    /// (eyesofterandroid: seguridad/RecuperacionPassword.kt) y la tabla codigos_recuperacion es
    /// compartida: un código pedido en la app sirve en el portal y al revés. Si cambias una regla
    /// aquí, cámbiala allá.
    ///
    ///  - El código vence en 10 minutos y se aceptan 5 intentos; pedir otro anula el anterior.
    ///  - Máximo un código por minuto y 5 por hora por cuenta (no se puede llenar de correos/SMS a nadie).
    ///  - Se guarda solo la huella HMAC con la llave de datos, nunca el código.
    ///  - Nunca se dice si la cuenta existe: la respuesta es la misma.
    ///  - Cambiar la contraseña NO quita la verificación en dos pasos: el personal sigue necesitando su app autenticadora.
    /// </summary>
    public class RecuperacionPassword
    {
        public const int MinutosVigencia = 10;
        public const int IntentosMax = 5;
        public const int SegundosEntreCodigos = 60;
        public const int CodigosPorHora = 5;
        private const string Proposito = "recuperacion";

        public const string ClaveCodigoIncorrecto = "CODIGO_INCORRECTO";
        public const string ClaveCodigoVencido = "CODIGO_VENCIDO";
        public const string ClavePasswordInvalida = "PASSWORD_INVALIDA";

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _usuarios;
        private readonly CifradoDatos _cifrado;
        private readonly ICorreo _correo;
        private readonly ISms _sms;
        private readonly Auditoria _auditoria;
        private readonly ILogger<RecuperacionPassword> _log;

        public RecuperacionPassword(ApplicationDbContext db, UserManager<ApplicationUser> usuarios, CifradoDatos cifrado, ICorreo correo,
                                    ISms sms, Auditoria auditoria, ILogger<RecuperacionPassword> log)
        {
            _db = db;
            _usuarios = usuarios;
            _cifrado = cifrado;
            _correo = correo;
            _sms = sms;
            _auditoria = auditoria;
            _log = log;
        }

        /// <summary>¿Hay proveedor de SMS? Si no, a quien escribe su celular se le manda el código a su correo.</summary>
        public bool SmsDisponible => _sms.Disponible;

        /// <summary>Lo que no es correo y tiene al menos 10 dígitos se toma como celular.</summary>
        public static bool EsTelefono(string cuenta) => !cuenta.Contains('@') && cuenta.Count(char.IsDigit) >= 10;

        /// <summary>Los últimos 10 dígitos ("+52 775 123 4567" → "7751234567").</summary>
        public static string? Telefono10(string cuenta)
        {
            var d = new string(cuenta.Where(char.IsDigit).ToArray());
            return d.Length >= 10 ? d[^10..] : null;
        }

        /// <summary>
        /// Cuenta activa por correo o por celular (el del tutor que representa o el de la cuenta).
        /// Si el celular es de varias cuentas no se elige ninguna: hay que usar el correo.
        /// </summary>
        public async Task<ApplicationUser?> BuscarAsync(string cuenta)
        {
            cuenta = (cuenta ?? "").Trim();
            if (cuenta.Length == 0) return null;
            if (!EsTelefono(cuenta))
            {
                var u = await _usuarios.FindByEmailAsync(cuenta);
                return u is { Activo: true } ? u : null;
            }
            var tel = Telefono10(cuenta)!;
            // SQL directo: no aplica el filtro por escuela (aquí nadie ha iniciado sesión).
            var ids = await _db.Database.SqlQuery<string>($"""
                select distinct u.id as "Value" from asp_net_users u left join tutores t on t.id = u.tutor_id
                where u.activo and (right(regexp_replace(coalesce(u.phone_number, ''), '\D', '', 'g'), 10) = {tel}
                                 or right(regexp_replace(coalesce(t.telefono, ''), '\D', '', 'g'), 10) = {tel})
                """).ToListAsync();
            if (ids.Count != 1)
            {
                if (ids.Count > 1) _log.LogInformation("Recuperación por celular: el número es de {N} cuentas; se pide usar el correo", ids.Count);
                return null;
            }
            return await _usuarios.FindByIdAsync(ids[0]);
        }

        /// <summary>Manda un código nuevo si la cuenta existe y no rebasó el límite. No dice nada hacia fuera.</summary>
        public async Task SolicitarAsync(string cuenta, string? ip)
        {
            var usuario = await BuscarAsync(cuenta);
            if (usuario is null) return;

            var ahora = DateTime.UtcNow;
            var recientes = await _db.CodigosRecuperacion
                .Where(c => c.UsuarioId == usuario.Id && c.CreadoEn > ahora.AddHours(-1))
                .Select(c => c.CreadoEn).ToListAsync();
            if (recientes.Count >= CodigosPorHora || recientes.Any(f => f > ahora.AddSeconds(-SegundosEntreCodigos)))
            {
                _log.LogInformation("Recuperación: límite de códigos para la cuenta {Id}", usuario.Id);
                return;
            }

            var porSms = EsTelefono(cuenta) && _sms.Disponible;
            if (!porSms && string.IsNullOrWhiteSpace(usuario.Email)) return;

            // Pedir otro código anula el anterior.
            await _db.CodigosRecuperacion.Where(c => c.UsuarioId == usuario.Id && c.UsadoEn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsadoEn, ahora));

            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _db.CodigosRecuperacion.Add(new CodigoRecuperacion
            {
                UsuarioId = usuario.Id,
                CodigoHash = Huella(usuario.Id, codigo),
                Canal = porSms ? "sms" : "correo",
                CreadoEn = ahora,
                VenceEn = ahora.AddMinutes(MinutosVigencia),
                Ip = ip?[..Math.Min(ip.Length, 45)]
            });
            _auditoria.Registrar("RECUPERAR_PASSWORD", "Usuario", usuario.Id, porSms ? "Código por SMS" : "Código por correo", usuario: usuario.Email);
            await _db.SaveChangesAsync();

            var enviado = porSms
                ? await _sms.EnviarAsync(cuenta, TextoSms(codigo))
                : await _correo.EnviarAsync(usuario.Email!, "EYESOFTER: tu código para cambiar la contraseña", HtmlCorreo(usuario.NombreCompleto, codigo));
            if (!enviado) _log.LogWarning("No se pudo enviar el código de recuperación a la cuenta {Id} por {Canal}", usuario.Id, porSms ? "SMS" : "correo");
        }

        /// <summary>Cambia la contraseña si el código es el vigente de esa cuenta.</summary>
        public async Task<ResultadoRecuperacion> RestablecerAsync(string cuenta, string codigo, string nueva)
        {
            var usuario = await BuscarAsync(cuenta);
            if (usuario is null) return Vencido();

            // Primero la contraseña: si no cumple las reglas, el código no se gasta.
            var errores = new List<string>();
            foreach (var v in _usuarios.PasswordValidators)
            {
                var r = await v.ValidateAsync(_usuarios, usuario, nueva);
                if (!r.Succeeded) errores.AddRange(r.Errors.Select(e => e.Description));
            }
            if (errores.Count > 0) return new(false, ClavePasswordInvalida, string.Join(" ", errores), errores);

            var ahora = DateTime.UtcNow;
            var vigente = await _db.CodigosRecuperacion.AsNoTracking()
                .Where(c => c.UsuarioId == usuario.Id && c.UsadoEn == null && c.VenceEn > ahora)
                .OrderByDescending(c => c.CreadoEn).FirstOrDefaultAsync();
            if (vigente is null) return Vencido();

            // El intento se cuenta ANTES de comparar y solo si quedan: dos peticiones a la vez no dan intentos de más.
            var contado = await _db.CodigosRecuperacion.Where(c => c.Id == vigente.Id && c.Intentos < IntentosMax)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Intentos, c => c.Intentos + 1));
            if (contado == 0) return new(false, ClaveCodigoVencido, "Ese código ya no sirve por demasiados intentos. Pide uno nuevo.");

            var limpio = new string((codigo ?? "").Where(char.IsDigit).ToArray());
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Huella(usuario.Id, limpio)), Encoding.ASCII.GetBytes(vigente.CodigoHash)))
            {
                var quedan = IntentosMax - (vigente.Intentos + 1);
                _auditoria.Registrar("RECUPERAR_CODIGO_FALLIDO", "Usuario", usuario.Id, exito: false, usuario: usuario.Email);
                await _db.SaveChangesAsync();
                return new(false, ClaveCodigoIncorrecto, quedan > 0
                    ? $"El código no es correcto. Te {(quedan == 1 ? "queda 1 intento" : $"quedan {quedan} intentos")}."
                    : "El código no es correcto y ya no sirve. Pide uno nuevo.");
            }

            // Se marca como usado en una sola operación: el mismo código no cambia la contraseña dos veces.
            var reclamado = await _db.CodigosRecuperacion.Where(c => c.Id == vigente.Id && c.UsadoEn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsadoEn, ahora));
            if (reclamado == 0) return Vencido();

            var token = await _usuarios.GeneratePasswordResetTokenAsync(usuario);
            var cambio = await _usuarios.ResetPasswordAsync(usuario, token, nueva);
            if (!cambio.Succeeded)
            {
                await _db.CodigosRecuperacion.Where(c => c.Id == vigente.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.UsadoEn, (DateTime?)null));
                var lista = cambio.Errors.Select(e => e.Description).ToList();
                return new(false, ClavePasswordInvalida, string.Join(" ", lista), lista);
            }
            // ResetPassword cambia el sello de seguridad: se cierran las sesiones abiertas en la web y en la app.
            await _usuarios.SetLockoutEndDateAsync(usuario, null);
            await _usuarios.ResetAccessFailedCountAsync(usuario);
            _auditoria.Registrar("PASSWORD_RESTABLECIDA", "Usuario", usuario.Id, "Con código de recuperación", usuario: usuario.Email);
            await _db.SaveChangesAsync();
            return ResultadoRecuperacion.Correcto;
        }

        private static ResultadoRecuperacion Vencido() =>
            new(false, ClaveCodigoVencido, "El código no es válido o ya venció. Pide uno nuevo.");

        /// <summary>Huella que se guarda. Ktor: CifradoDatos.firma("recuperacion", "$usuarioId:$codigo").</summary>
        private string Huella(string usuarioId, string codigo) => _cifrado.Firma(Proposito, $"{usuarioId}:{codigo}");

        public static string TextoSms(string codigo) =>
            $"EYESOFTER: tu código para cambiar tu contraseña es {codigo}. Vence en {MinutosVigencia} min. No lo compartas con nadie.";

        public static string HtmlCorreo(string nombre, string codigo) =>
            $"<p>Hola, {HtmlEncoder.Default.Encode(nombre)}:</p>" +
            "<p>Tu código para crear una contraseña nueva en EYESOFTER es:</p>" +
            $"<p style=\"font-size:28px;font-weight:bold;letter-spacing:6px\">{codigo}</p>" +
            $"<p>Escríbelo en el portal o en la app. Vence en {MinutosVigencia} minutos.</p>" +
            "<p>Si no lo pediste, ignora este correo: tu contraseña no cambia. Nadie de la escuela te pedirá este código.</p>";
    }
}
