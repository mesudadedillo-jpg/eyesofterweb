using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Aviso de privacidad y términos de uso, accesibles siempre (C-RS-07), y consentimiento
    /// expreso del tutor antes de tratar datos de menores (C-RC-08).
    /// </summary>
    public class PrivacidadController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _usuarios;
        private readonly Auditoria _auditoria;
        private readonly OpcionesEyesofter _opciones;

        public PrivacidadController(ApplicationDbContext db, UserManager<ApplicationUser> usuarios, Auditoria auditoria, IOptions<OpcionesEyesofter> opciones)
        {
            _db = db;
            _usuarios = usuarios;
            _auditoria = auditoria;
            _opciones = opciones.Value;
        }

        [AllowAnonymous]
        public IActionResult Aviso()
        {
            ViewBag.Version = _opciones.VersionAvisoPrivacidad;
            return View();
        }

        [AllowAnonymous]
        public IActionResult Terminos() => View();

        [Authorize(Roles = Roles.Tutor)]
        public IActionResult Aceptar(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Version = _opciones.VersionAvisoPrivacidad;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Tutor)]
        public async Task<IActionResult> Aceptar(bool acepto, string? returnUrl)
        {
            if (!acepto)
            {
                TempData["Error"] = "Para consultar la información de tus hijos necesitamos tu consentimiento.";
                return RedirectToAction(nameof(Aceptar), new { returnUrl });
            }
            var id = _usuarios.GetUserId(User)!;
            if (!await _db.Consentimientos.AnyAsync(c => c.UsuarioId == id && c.VersionAviso == _opciones.VersionAvisoPrivacidad))
            {
                _db.Consentimientos.Add(new Consentimiento
                {
                    UsuarioId = id, VersionAviso = _opciones.VersionAvisoPrivacidad, Medio = "web",
                    Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
                });
                _auditoria.Registrar("CONSENTIMIENTO", "Usuario", id, $"Aviso {_opciones.VersionAvisoPrivacidad}");
                await _db.SaveChangesAsync();
            }
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "MisHijos");
        }
    }
}
