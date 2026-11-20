using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Registro, inicio y cierre de sesión con ASP.NET Core Identity (cuentas individuales).
    /// </summary>
    public class CuentaController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<CuentaController> _logger;

        public CuentaController(UserManager<ApplicationUser> userManager,
                                SignInManager<ApplicationUser> signInManager,
                                ILogger<CuentaController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        // GET: /Cuenta/Registro
        [AllowAnonymous]
        public IActionResult Registro()
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            return View(new RegistroViewModel());
        }

        // POST: /Cuenta/Registro
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(RegistroViewModel modelo)
        {
            if (!ModelState.IsValid) return View(modelo);

            var usuario = new ApplicationUser
            {
                UserName = modelo.Correo,
                Email = modelo.Correo,
                NombreCompleto = modelo.NombreCompleto
            };

            var resultado = await _userManager.CreateAsync(usuario, modelo.Password);
            if (resultado.Succeeded)
            {
                // Mínimo privilegio: toda cuenta nueva entra como Docente (solo consulta).
                // El Administrador puede cambiarle el rol desde "Usuarios".
                await _userManager.AddToRoleAsync(usuario, Roles.Docente);
                _logger.LogInformation("Nueva cuenta registrada: {Correo}", modelo.Correo);

                await _signInManager.SignInAsync(usuario, isPersistent: false);
                TempData["Exito"] = $"¡Bienvenido(a), {usuario.NombreCompleto}! Tu cuenta se creó con el rol Docente.";
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in resultado.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(modelo);
        }

        // GET: /Cuenta/Login
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: /Cuenta/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel modelo)
        {
            if (!ModelState.IsValid) return View(modelo);

            var resultado = await _signInManager.PasswordSignInAsync(
                modelo.Correo, modelo.Password, modelo.Recordarme, lockoutOnFailure: true);

            if (resultado.Succeeded)
            {
                _logger.LogInformation("Inicio de sesión: {Correo}", modelo.Correo);
                if (!string.IsNullOrEmpty(modelo.ReturnUrl) && Url.IsLocalUrl(modelo.ReturnUrl))
                {
                    return LocalRedirect(modelo.ReturnUrl);
                }
                return RedirectToAction("Index", "Home");
            }

            if (resultado.IsLockedOut)
            {
                _logger.LogWarning("Cuenta bloqueada por intentos fallidos: {Correo}", modelo.Correo);
                ModelState.AddModelError(string.Empty,
                    "Tu cuenta quedó bloqueada por 5 minutos después de 3 intentos fallidos.");
                return View(modelo);
            }

            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View(modelo);
        }

        // POST: /Cuenta/Logout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("Sesión cerrada.");
            TempData["Exito"] = "Cerraste sesión correctamente.";
            return RedirectToAction("Index", "Home");
        }

        // GET: /Cuenta/AccesoDenegado  (a dónde manda Identity cuando el rol no tiene permiso)
        [AllowAnonymous]
        public IActionResult AccesoDenegado(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
    }
}
