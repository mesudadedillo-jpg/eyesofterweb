using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authentication;
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
        private readonly ILogger<CuentaController> _logger;
        private readonly Documentos _documentos;
        private readonly RecuperacionPassword _recuperacion;

        public CuentaController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
                                Auditoria auditoria, ILogger<CuentaController> logger, Documentos documentos,
                                RecuperacionPassword recuperacion)
        {
            _documentos = documentos;
            _recuperacion = recuperacion;
            _userManager = userManager;
            _signInManager = signInManager;
            _auditoria = auditoria;
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
                // Solo se dice "desactivada" a quien sabe la contraseña: si no, cualquiera podría
                // averiguar qué correos tienen cuenta en la escuela.
                var correcta = await _signInManager.CheckPasswordSignInAsync(usuario, modelo.Password, lockoutOnFailure: true);
                _auditoria.Registrar("LOGIN_CUENTA_DESACTIVADA", "Usuario", usuario.Id, modelo.Correo, exito: false, usuario: usuario.Email);
                await GuardarAuditoriaAsync();
                ModelState.AddModelError(string.Empty, correcta.Succeeded
                    ? "Tu cuenta está desactivada. Comunícate con la Dirección de la escuela."
                    : "Correo o contraseña incorrectos.");
                return View(modelo);
            }

            // No hay "recordarme": la sesión expira a los 15 minutos de inactividad (W-RFNN-01).
            var resultado = await _signInManager.PasswordSignInAsync(modelo.Correo, modelo.Password, isPersistent: false, lockoutOnFailure: true);

            if (resultado.Succeeded)
            {
                _auditoria.Registrar("LOGIN", "Usuario", usuario?.Id, modelo.Correo, usuario: usuario?.Email);
                await GuardarAuditoriaAsync();
                if (!string.IsNullOrEmpty(modelo.ReturnUrl) && Url.IsLocalUrl(modelo.ReturnUrl)) return LocalRedirect(modelo.ReturnUrl);
                return RedirectToAction("Index", "Home");
            }
            // Contraseña correcta, falta el código de la app autenticadora (paso 2).
            if (resultado.RequiresTwoFactor)
                return RedirectToAction(nameof(LoginDosPasos), new { returnUrl = modelo.ReturnUrl });

            _auditoria.Registrar(resultado.IsLockedOut ? "LOGIN_BLOQUEADO" : "LOGIN_FALLIDO", "Usuario", usuario?.Id, modelo.Correo, exito: false, usuario: usuario?.Email);
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

        // ---------- "¿Olvidaste tu contraseña?" con código al correo o al celular (C-RFNN-02, C-RI-02) ----------
        // Las reglas viven en Servicios/RecuperacionPassword.cs (las mismas que la app).

        [AllowAnonymous]
        public IActionResult Olvide() => View(new OlvideViewModel());

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> Olvide(OlvideViewModel modelo)
        {
            if (!ModelState.IsValid) return View(modelo);
            var cuenta = modelo.Cuenta.Trim();
            if (!cuenta.Contains('@') && !RecuperacionPassword.EsTelefono(cuenta))
            {
                ModelState.AddModelError(nameof(modelo.Cuenta), "Escribe tu correo o tu celular de 10 dígitos.");
                return View(modelo);
            }
            await _recuperacion.SolicitarAsync(cuenta, HttpContext.Connection.RemoteIpAddress?.ToString());
            // Mismo resultado exista o no la cuenta: así no se puede averiguar quién está registrado.
            TempData[ClaveCuentaRecuperacion] = cuenta;
            return RedirectToAction(nameof(IngresarCodigo));
        }

        /// <summary>Paso 2: escribir el código y la contraseña nueva. También sirve con un código pedido en la app.</summary>
        [AllowAnonymous]
        public IActionResult IngresarCodigo()
        {
            var cuenta = TempData.Peek(ClaveCuentaRecuperacion) as string ?? "";
            return View(ModeloCodigo(new IngresarCodigoViewModel { Cuenta = cuenta }));
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> IngresarCodigo(IngresarCodigoViewModel modelo)
        {
            if (!ModelState.IsValid) return View(ModeloCodigo(modelo));
            var r = await _recuperacion.RestablecerAsync(modelo.Cuenta, modelo.Codigo, modelo.Password);
            if (!r.Exito)
            {
                if (r.Clave == RecuperacionPassword.ClavePasswordInvalida)
                    foreach (var e in r.Errores ?? new[] { r.Mensaje! }) ModelState.AddModelError(nameof(modelo.Password), e);
                else ModelState.AddModelError(string.Empty, r.Mensaje!);
                return View(ModeloCodigo(modelo));
            }
            TempData.Remove(ClaveCuentaRecuperacion);
            TempData["Exito"] = "Listo. Ya puedes entrar con tu nueva contraseña (también en la app).";
            return RedirectToAction(nameof(Login));
        }

        private const string ClaveCuentaRecuperacion = "RecuperarCuenta";

        private IngresarCodigoViewModel ModeloCodigo(IngresarCodigoViewModel m)
        {
            m.PorCelular = RecuperacionPassword.EsTelefono(m.Cuenta ?? "");
            m.SmsDisponible = _recuperacion.SmsDisponible;
            return m;
        }

        // ---------- Crear la contraseña con el enlace del correo de bienvenida (alta de cuentas) ----------

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
            _auditoria.Registrar("PASSWORD_RESTABLECIDA", "Usuario", usuario.Id, usuario: usuario.Email);
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

        // ---------- Verificación en dos pasos (TOTP) ----------
        // Obligatoria para Dirección y Enfermería (ven datos de salud de muchos menores): una
        // contraseña robada ya no basta. Funciona con Google Authenticator, Microsoft Authenticator,
        // Authy o cualquier app TOTP. El servidor de la app móvil pide el mismo código.

        [AllowAnonymous]
        public async Task<IActionResult> LoginDosPasos(string? returnUrl = null)
        {
            if (await _signInManager.GetTwoFactorAuthenticationUserAsync() is null) return RedirectToAction(nameof(Login));
            return View(new LoginDosPasosViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> LoginDosPasos(LoginDosPasosViewModel modelo)
        {
            var usuario = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (usuario is null) return RedirectToAction(nameof(Login));
            // La cuenta pudo desactivarse entre el paso 1 y el 2.
            if (!usuario.Activo)
            {
                await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
                _auditoria.Registrar("LOGIN_CUENTA_DESACTIVADA", "Usuario", usuario.Id, usuario.Email, exito: false, usuario: usuario.Email);
                await GuardarAuditoriaAsync();
                TempData["Error"] = "Tu cuenta está desactivada. Comunícate con la Dirección de la escuela.";
                return RedirectToAction(nameof(Login));
            }
            if (!ModelState.IsValid) return View(modelo);

            // El código de la app son 6 dígitos; los de respaldo llevan guion (XXXXX-XXXXX) y se conservan.
            var codigo = modelo.EsCodigoDeRespaldo
                ? new string(modelo.Codigo.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray()).ToUpperInvariant()
                : new string(modelo.Codigo.Where(char.IsDigit).ToArray());
            var resultado = modelo.EsCodigoDeRespaldo
                ? await _signInManager.TwoFactorRecoveryCodeSignInAsync(codigo)
                : await _signInManager.TwoFactorAuthenticatorSignInAsync(codigo, isPersistent: false, rememberClient: false);
            if (resultado.Succeeded)
            {
                _auditoria.Registrar(modelo.EsCodigoDeRespaldo ? "LOGIN_CODIGO_RESPALDO" : "LOGIN", "Usuario", usuario.Id, usuario.Email, usuario: usuario.Email);
                await GuardarAuditoriaAsync();
                if (modelo.EsCodigoDeRespaldo)
                    TempData["Error"] = "Usaste un código de respaldo (ya no sirve de nuevo). Si perdiste tu teléfono, vuelve a configurar la verificación en dos pasos.";
                if (!string.IsNullOrEmpty(modelo.ReturnUrl) && Url.IsLocalUrl(modelo.ReturnUrl)) return LocalRedirect(modelo.ReturnUrl);
                return RedirectToAction("Index", "Home");
            }
            _auditoria.Registrar(resultado.IsLockedOut ? "LOGIN_BLOQUEADO" : "LOGIN_2FA_FALLIDO", "Usuario", usuario.Id, usuario.Email, exito: false, usuario: usuario.Email);
            await GuardarAuditoriaAsync();
            ModelState.AddModelError(string.Empty, resultado.IsLockedOut
                ? "Tu cuenta quedó bloqueada 15 minutos por intentos fallidos."
                : "El código no es correcto o ya venció. Escribe el que aparece ahora en tu app.");
            return View(modelo);
        }

        /// <summary>Activar (o ver el estado de) la verificación en dos pasos.</summary>
        [Authorize]
        public async Task<IActionResult> DosPasos()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario is null) return Challenge();
            if (await _userManager.GetTwoFactorEnabledAsync(usuario)) return View("DosPasosActiva", await _userManager.CountRecoveryCodesAsync(usuario));
            return View(await ModeloDosPasosAsync(usuario));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> DosPasos(DosPasosViewModel modelo)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario is null) return Challenge();
            var codigo = new string((modelo.Codigo ?? "").Where(char.IsDigit).ToArray());
            var valido = codigo.Length == 6 && await _userManager.VerifyTwoFactorTokenAsync(usuario, _userManager.Options.Tokens.AuthenticatorTokenProvider, codigo);
            if (!valido)
            {
                ModelState.AddModelError(nameof(modelo.Codigo), "El código no coincide. Revisa que la hora de tu teléfono sea automática y escribe el código que aparece ahora.");
                return View(await ModeloDosPasosAsync(usuario));
            }
            await _userManager.SetTwoFactorEnabledAsync(usuario, true);
            var codigos = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, 10) ?? Enumerable.Empty<string>()).ToList();
            _auditoria.Registrar("ACTIVAR_2FA", "Usuario", usuario.Id, usuario.Email);
            await GuardarAuditoriaAsync();
            await _signInManager.RefreshSignInAsync(usuario);
            return View("CodigosRespaldo", codigos);
        }

        /// <summary>Nuevos códigos de respaldo (los anteriores dejan de servir).</summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NuevosCodigos()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario is null || !await _userManager.GetTwoFactorEnabledAsync(usuario)) return RedirectToAction(nameof(DosPasos));
            var codigos = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, 10) ?? Enumerable.Empty<string>()).ToList();
            _auditoria.Registrar("NUEVOS_CODIGOS_2FA", "Usuario", usuario.Id, usuario.Email);
            await GuardarAuditoriaAsync();
            return View("CodigosRespaldo", codigos);
        }

        private async Task<DosPasosViewModel> ModeloDosPasosAsync(ApplicationUser usuario)
        {
            var clave = await _userManager.GetAuthenticatorKeyAsync(usuario);
            if (string.IsNullOrEmpty(clave))
            {
                await _userManager.ResetAuthenticatorKeyAsync(usuario);
                clave = await _userManager.GetAuthenticatorKeyAsync(usuario);
                // Generar la clave cambia el sello de seguridad: sin esto la sesión actual se cerraría.
                await _signInManager.RefreshSignInAsync(usuario);
            }
            var uri = $"otpauth://totp/{Uri.EscapeDataString("EYESOFTER:" + usuario.Email)}?secret={clave}&issuer=EYESOFTER&digits=6";
            return new DosPasosViewModel
            {
                ClaveLegible = string.Join(" ", Enumerable.Range(0, (clave!.Length + 3) / 4).Select(i => clave.Substring(i * 4, Math.Min(4, clave.Length - i * 4)))).ToLowerInvariant(),
                QrDataUri = "data:image/png;base64," + Convert.ToBase64String(_documentos.QrPng(uri, 6)),
                Obligatoria = User.IsInRole(Roles.Administrador) || User.IsInRole(Roles.Enfermeria)
            };
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
