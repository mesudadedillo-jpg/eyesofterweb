using System.Text.Json;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>Bandeja del usuario en la web (la misma que ve en la app).</summary>
    [Authorize]
    public class NotificacionesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _usuarios;

        public NotificacionesController(ApplicationDbContext db, UserManager<ApplicationUser> usuarios)
        {
            _db = db;
            _usuarios = usuarios;
        }

        public async Task<IActionResult> Index()
        {
            var id = _usuarios.GetUserId(User);
            return View(await _db.Notificaciones.AsNoTracking().Where(n => n.UsuarioId == id).OrderByDescending(n => n.CreadaEn).Take(100).ToListAsync());
        }

        /// <summary>Confirmación de lectura (C-RFN-07) y salto a la pantalla de la notificación.</summary>
        public async Task<IActionResult> Abrir(Guid id)
        {
            var uid = _usuarios.GetUserId(User);
            var n = await _db.Notificaciones.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == uid);
            if (n is null) return NotFound();
            n.LeidaEn ??= DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var datos = JsonSerializer.Deserialize<Dictionary<string, string>>(n.Datos) ?? new();
            var ruta = datos.GetValueOrDefault("ruta") ?? "";
            if (ruta.StartsWith("tamizaje/") && User.IsInRole(Roles.Tutor) && int.TryParse(ruta["tamizaje/".Length..], out var t))
                return RedirectToAction("Reporte", "MisHijos", new { id = t });
            if (datos.TryGetValue("casoId", out var casoId) && (User.IsInRole(Roles.Administrador) || User.IsInRole(Roles.Enfermeria)))
                return RedirectToAction("Details", "Casos", new { id = casoId });
            if (ruta == "comprobantes" && (User.IsInRole(Roles.Administrador) || User.IsInRole(Roles.Enfermeria)))
                return RedirectToAction("Index", "Comprobantes");
            if (ruta == "seguimiento") return RedirectToAction("Seguimiento", "Avisos");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarTodas()
        {
            var uid = _usuarios.GetUserId(User);
            await _db.Notificaciones.Where(n => n.UsuarioId == uid && n.LeidaEn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.LeidaEn, DateTime.UtcNow));
            return RedirectToAction(nameof(Index));
        }
    }
}
