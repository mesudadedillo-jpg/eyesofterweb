using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>Ayuda, guía para clínicas (C-RS-08) y canal para reportar fallas o dudas durante el piloto (C-RS-04).</summary>
    public class SoporteController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _usuarios;
        private readonly Notificador _notificador;

        public SoporteController(ApplicationDbContext db, UserManager<ApplicationUser> usuarios, Notificador notificador)
        {
            _db = db;
            _usuarios = usuarios;
            _notificador = notificador;
        }

        /// <summary>/Soporte lleva a la ayuda (antes respondía 404).</summary>
        [AllowAnonymous] public IActionResult Index() => RedirectToAction(nameof(Ayuda));
        [AllowAnonymous] public IActionResult Ayuda() => View();
        [AllowAnonymous] public IActionResult GuiaClinicas() => View();

        [AllowAnonymous]
        public IActionResult Reportar() => View(new ReporteFalla());

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> Reportar([Bind("Tipo,Mensaje,Correo")] ReporteFalla r)
        {
            if (r.Tipo is not ("Falla" or "Duda")) ModelState.AddModelError(nameof(r.Tipo), "Elige falla o duda.");
            if (!ModelState.IsValid) return View(r);
            r.UsuarioId = _usuarios.GetUserId(User);
            r.Correo ??= User.Identity?.IsAuthenticated == true ? (await _usuarios.GetUserAsync(User))?.Email : null;
            r.Plataforma = "web";
            _db.ReportesFalla.Add(r);
            await _notificador.AAdministracionDelSistemaAsync("AVISO", $"Nuevo reporte: {r.Tipo}", r.Mensaje.Length > 120 ? r.Mensaje[..120] + "…" : r.Mensaje);
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Gracias. Recibimos tu reporte y lo atenderemos durante el piloto.";
            return RedirectToAction("Index", "Home");
        }

        [Authorize(Policy = Roles.PoliticaGlobal)]
        public async Task<IActionResult> Reportes(EstadoReporte? estado) =>
            View(await _db.ReportesFalla.AsNoTracking().Where(x => estado == null || x.Estado == estado).OrderByDescending(x => x.CreadoEn).Take(200).ToListAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Roles.PoliticaGlobal)]
        public async Task<IActionResult> Atender(int id, EstadoReporte estado, string? respuesta)
        {
            var r = await _db.ReportesFalla.FindAsync(id);
            if (r is null) return NotFound();
            r.Estado = estado;
            r.Respuesta = respuesta?.Trim();
            if (r.UsuarioId is not null && !string.IsNullOrWhiteSpace(respuesta))
                _notificador.AUsuario(r.UsuarioId, "AVISO", "Respuesta a tu reporte", respuesta.Trim());
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Reportes));
        }
    }
}
