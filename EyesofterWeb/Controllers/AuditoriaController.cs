using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Seguridad;
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
        private readonly AlcanceEscuela _alcance;

        public AuditoriaController(ApplicationDbContext db, AlcanceEscuela alcance)
        {
            _db = db;
            _alcance = alcance;
        }

        /// <summary>
        /// Dirección ve los registros de sus escuelas y lo que hizo el personal de sus escuelas.
        /// Los registros sin escuela (del sistema) solo los ve el administrador global.
        /// </summary>
        private IQueryable<RegistroAuditoria> Visibles()
        {
            var q = _db.Auditoria.AsNoTracking().AsQueryable();
            if (_alcance.SinRestriccion) return q;
            var planteles = _alcance.Planteles;
            var personal = _db.UsuarioPlanteles.Where(x => planteles.Contains(x.PlantelId)).Select(x => x.Usuario!.Email);
            // Sin escuela solo se muestran los movimientos de la propia cuenta del personal (accesos,
            // contraseña, dos pasos, accesos denegados): lo de un expediente lleva siempre su escuela,
            // así una persona con dos escuelas no filtra lo de una a la Dirección de la otra.
            return q.Where(a => (a.PlantelId != null && planteles.Contains(a.PlantelId.Value))
                                || (a.PlantelId == null && personal.Contains(a.Usuario)
                                    && (a.Accion.StartsWith("LOGIN") || a.Accion == "LOGOUT" || a.Accion.StartsWith("PASSWORD")
                                        || a.Accion == "RECUPERAR_PASSWORD" || a.Accion.Contains("2FA") || a.Accion == "ACCESO_DENEGADO")));
        }

        public async Task<IActionResult> Index(string? usuario, string? accion, bool soloDenegados = false, int pagina = 1)
        {
            var q = Visibles();
            if (!string.IsNullOrWhiteSpace(usuario)) q = q.Where(a => a.Usuario != null && a.Usuario.Contains(usuario));
            if (!string.IsNullOrWhiteSpace(accion)) q = q.Where(a => a.Accion == accion);
            if (soloDenegados) q = q.Where(a => !a.Exito);
            pagina = Math.Max(1, pagina);
            ViewBag.Usuario = usuario;
            ViewBag.Accion = accion;
            ViewBag.SoloDenegados = soloDenegados;
            ViewBag.Pagina = pagina;
            ViewBag.Acciones = await Visibles().Select(a => a.Accion).Distinct().OrderBy(a => a).ToListAsync();
            return View(await q.OrderByDescending(a => a.Fecha).Skip((pagina - 1) * 50).Take(50).ToListAsync());
        }
    }
}
