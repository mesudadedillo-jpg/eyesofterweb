using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>Consulta de la bitácora de auditoría (C-RC-07). Solo Dirección; no se puede editar ni borrar.</summary>
    [Authorize(Roles = Roles.Administrador)]
    public class AuditoriaController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AuditoriaController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? usuario, string? accion, bool soloDenegados = false, int pagina = 1)
        {
            var q = _db.Auditoria.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(usuario)) q = q.Where(a => a.Usuario != null && a.Usuario.Contains(usuario));
            if (!string.IsNullOrWhiteSpace(accion)) q = q.Where(a => a.Accion == accion);
            if (soloDenegados) q = q.Where(a => !a.Exito);
            pagina = Math.Max(1, pagina);
            ViewBag.Usuario = usuario;
            ViewBag.Accion = accion;
            ViewBag.SoloDenegados = soloDenegados;
            ViewBag.Pagina = pagina;
            ViewBag.Acciones = await _db.Auditoria.Select(a => a.Accion).Distinct().OrderBy(a => a).ToListAsync();
            return View(await q.OrderByDescending(a => a.Fecha).Skip((pagina - 1) * 50).Take(50).ToListAsync());
        }
    }
}
