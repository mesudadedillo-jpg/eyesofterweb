using System.ComponentModel.DataAnnotations;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>Avisos a las familias y seguimiento de quién leyó su resultado (C-RFN-07, C-RFN-08).</summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class AvisosController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly Notificador _notificador;
        private readonly Auditoria _auditoria;

        public AvisosController(ApplicationDbContext db, Notificador notificador, Auditoria auditoria)
        {
            _db = db;
            _notificador = notificador;
            _auditoria = auditoria;
        }

        public class AvisoForm
        {
            [Required(ErrorMessage = "El título es obligatorio."), StringLength(80)]
            [Display(Name = "Título")]
            public string Titulo { get; set; } = string.Empty;

            [Required(ErrorMessage = "El mensaje es obligatorio."), StringLength(500, MinimumLength = 5)]
            [DataType(DataType.MultilineText)]
            public string Mensaje { get; set; } = string.Empty;

            /// <summary>"tutores" = todos; "grupo" = tutores de un grado y grupo.</summary>
            public string Para { get; set; } = "tutores";
            public NivelEducativo? Nivel { get; set; }
            public int? Grado { get; set; }
            public string? Grupo { get; set; }
        }

        public IActionResult Index() => View(new AvisoForm());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(AvisoForm f)
        {
            if (f.Para == "grupo" && (f.Nivel is null || f.Grado is null || string.IsNullOrEmpty(f.Grupo)))
                ModelState.AddModelError(string.Empty, "Indica nivel, grado y grupo.");
            if (!ModelState.IsValid) return View(f);

            var alumnos = _db.Alumnos.AsQueryable();
            if (f.Para == "grupo") alumnos = alumnos.Where(a => a.Nivel == f.Nivel && a.Grado == f.Grado && a.Grupo == f.Grupo);
            var tutores = await alumnos.Select(a => a.TutorId).Distinct().ToListAsync();
            var cuentas = await _db.Users.Where(u => u.TutorId != null && tutores.Contains(u.TutorId.Value) && u.Activo).Select(u => u.Id).ToListAsync();
            foreach (var id in cuentas) _notificador.AUsuario(id, "AVISO", f.Titulo.Trim(), f.Mensaje.Trim());
            _auditoria.Registrar("ENVIAR_AVISO", "Aviso", null, $"{f.Titulo} → {cuentas.Count} cuentas");
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Aviso enviado a {cuentas.Count} tutores. Les llega a la app y queda registro de quién lo leyó.";
            return RedirectToAction(nameof(Seguimiento));
        }

        public record FilaSeguimiento(Guid Id, string Tipo, string Titulo, string Tutor, string? Alumno, DateTime CreadaEn, DateTime? LeidaEn, int Recordatorios, bool Entregada);

        public async Task<IActionResult> Seguimiento(bool soloSinLeer = true, string tipo = "RESULTADO_TAMIZAJE")
        {
            var tutores = _db.Users.Where(u => u.TutorId != null);
            var filas = await _db.Notificaciones.AsNoTracking()
                .Where(n => n.Tipo == tipo && (!soloSinLeer || n.LeidaEn == null))
                .Join(tutores, n => n.UsuarioId, u => u.Id, (n, u) => new { n, u })
                .OrderByDescending(x => x.n.CreadaEn).Take(300)
                .Select(x => new FilaSeguimiento(x.n.Id, x.n.Tipo, x.n.Titulo, x.u.NombreCompleto, null, x.n.CreadaEn, x.n.LeidaEn, x.n.Recordatorios, x.n.Entregas.Any(e => e.Exito)))
                .ToListAsync();
            ViewBag.SoloSinLeer = soloSinLeer;
            ViewBag.Tipo = tipo;
            return View(filas);
        }
    }
}
