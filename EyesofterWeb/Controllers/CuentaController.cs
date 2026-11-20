using System.Text.Encodings.Web;
using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Inicio y cierre de sesión, recuperación y cambio de contraseña.
    /// No hay registro público: las cuentas las crea Dirección (datos de menores, C-RC-06).
    /// </summary>
    public class CuentaController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly Auditoria _auditoria;
        private readonly ICorreo _correo;
        private readonly ILogger<CuentaController> _logger;

        public CuentaController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
                                Auditoria auditoria, ICorreo correo, ILogger<CuentaController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _auditoria = auditoria;
            _correo = correo;
            _logger = logger;
        }

        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null, bool expirada = false)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            if (expirada) ViewBag.Aviso = "Tu sesión se cerró por inactividad. Vuelve a entrar.";
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> Login(LoginViewModel modelo)
        {
            if (!ModelState.IsValid) return View(modelo);

            var usuario = await _userManager.FindByEmailAsync(modelo.Correo);
            if (usuario is not null && !usuario.Activo)
            {
                _auditoria.Registrar("LOGIN_CUENTA_DESACTIVADA", "Usuario", usuario.Id, modelo.Correo, exito: false);
                await GuardarAuditoriaAsync();
                ModelState.AddModelError(string.Empty, "Tu cuenta está desactivada. Comunícate con la Dirección de la escuela.");
                return View(modelo);
            }

            // No hay "recordarme": la sesión expira a los 15 minutos de inactividad (W-RFNN-01).
            var resultado = await _signInManager.PasswordSignInAsync(modelo.Correo, modelo.Password, isPersistent: false, lockoutOnFailure: true);

            if (resultado.Succeeded)
            {
                _auditoria.Registrar("LOGIN", "Usuario", usuario?.Id, modelo.Correo);
                await GuardarAuditoriaAsync();
                if (!string.IsNullOrEmpty(modelo.ReturnUrl) && Url.IsLocalUrl(modelo.ReturnUrl)) return LocalRedirect(modelo.ReturnUrl);
                return RedirectToAction("Index", "Home");
            }

            _auditoria.Registrar(resultado.IsLockedOut ? "LOGIN_BLOQUEADO" : "LOGIN_FALLIDO", "Usuario", usuario?.Id, modelo.Correo, exito: false);
            await GuardarAuditoriaAsync();
            ModelState.AddModelError(string.Empty, resultado.IsLockedOut
                ? "Tu cuenta quedó bloqueada 15 minutos por 3 intentos fallidos. Puedes esperar o recuperar tu contraseña."
                : "Correo o contraseña incorrectos.");
            return View(modelo);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            _auditoria.Registrar("LOGOUT", "Usuario", _userManager.GetUserId(User));
            await GuardarAuditoriaAsync();
            await _signInManager.SignOutAsync();
            TempData["Exito"] = "Cerraste sesión correctamente.";
            return RedirectToAction("Index", "Home");
        }

        // ---------- Recuperación de contraseña por correo (C-RFNN-02, C-RI-02) ----------

        [AllowAnonymous]
        public IActionResult Olvide() => View(new OlvideViewModel());

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> Olvide(OlvideViewModel modelo)
        {
            if (!ModelState.IsValid) return View(modelo);

            var usuario = await _userManager.FindByEmailAsync(modelo.Correo);
            if (usuario is { Activo: true })
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
                var enlace = Url.Action(nameof(Restablecer), "Cuenta", new { correo = usuario.Email, token }, Request.Scheme)!;
                await _correo.EnviarAsync(usuario.Email!, "EYESOFTER: restablece tu contraseña",
                    $"<p>Hola, {HtmlEncoder.Default.Encode(usuario.NombreCompleto)}:</p>" +
                    $"<p>Para crear una contraseña nueva entra a este enlace (vence en 2 horas):</p>" +
                    $"<p><a href=\"{HtmlEncoder.Default.Encode(enlace)}\">Restablecer mi contraseña</a></p>" +
                    "<p>Si no lo pediste, ignora este correo: tu contraseña no cambia.</p>");
                _auditoria.Registrar("RECUPERAR_PASSWORD", "Usuario", usuario.Id, modelo.Correo);
                await GuardarAuditoriaAsync();
            }
            // Mismo mensaje exista o no la cuenta: así no se puede averiguar quién está registrado.
            TempData["Exito"] = "Si el correo está registrado, te enviamos un enlace para restablecer la contraseña.";
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        public IActionResult Restablecer(string? correo, string? token)
        {
            if (string.IsNullOrEmpty(correo) || string.IsNullOrEmpty(token)) return RedirectToAction(nameof(Olvide));
            return View(new RestablecerViewModel { Correo = correo, Token = token });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> Restablecer(RestablecerViewModel modelo)
        {
            if (!ModelState.IsValid) return View(modelo);
            var usuario = await _userManager.FindByEmailAsync(modelo.Correo);
            if (usuario is null)
            {
                TempData["Error"] = "El enlace no es válido o ya venció. Pide uno nuevo.";
                return RedirectToAction(nameof(Olvide));
            }
            var r = await _userManager.ResetPasswordAsync(usuario, modelo.Token, modelo.Password);
            if (!r.Succeeded)
            {
                foreach (var e in r.Errors) ModelState.AddModelError(string.Empty, e.Code == "InvalidToken" ? "El enlace no es válido o ya venció. Pide uno nuevo." : e.Description);
                return View(modelo);
            }
            await _userManager.SetLockoutEndDateAsync(usuario, null);
            await _userManager.ResetAccessFailedCountAsync(usuario);
            _auditoria.Registrar("PASSWORD_RESTABLECIDA", "Usuario", usuario.Id);
            await GuardarAuditoriaAsync();
            TempData["Exito"] = "Listo. Ya puedes entrar con tu nueva contraseña (también en la app).";
            return RedirectToAction(nameof(Login));
        }

        [Authorize]
        public IActionResult CambiarPassword() => View(new CambiarPasswordViewModel());

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarPassword(CambiarPasswordViewModel modelo)
        {
            if (!ModelState.IsValid) return View(modelo);
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario is null) return Challenge();
            var r = await _userManager.ChangePasswordAsync(usuario, modelo.Actual, modelo.Nueva);
            if (!r.Succeeded)
            {
                foreach (var e in r.Errors) ModelState.AddModelError(string.Empty, e.Code == "PasswordMismatch" ? "La contraseña actual no es correcta." : e.Description);
                return View(modelo);
            }
            await _signInManager.RefreshSignInAsync(usuario);
            _auditoria.Registrar("PASSWORD_CAMBIADA", "Usuario", usuario.Id);
            await GuardarAuditoriaAsync();
            TempData["Exito"] = "Tu contraseña se cambió.";
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public IActionResult AccesoDenegado(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        private async Task GuardarAuditoriaAsync()
        {
            try { await HttpContext.RequestServices.GetRequiredService<EyesofterWeb.Data.ApplicationDbContext>().SaveChangesAsync(); }
            catch (Exception e) { _logger.LogError(e, "No se pudo guardar la bitácora"); }
        }
    }
}
