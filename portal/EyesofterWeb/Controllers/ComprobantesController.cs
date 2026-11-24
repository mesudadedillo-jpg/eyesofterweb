using EyesofterWeb.Data;
using EyesofterWeb.Helpers;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>Validación de recetas y comprobantes de lentes (W-RFN-09, W-RFNN-02). Solo personal escolar.</summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class ComprobantesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly Expedientes _expedientes;
        private readonly Notificador _notificador;
        private readonly Auditoria _auditoria;

        public ComprobantesController(ApplicationDbContext db, Expedientes expedientes, Notificador notificador, Auditoria auditoria)
        {
            _db = db;
            _expedientes = expedientes;
            _notificador = notificador;
            _auditoria = auditoria;
        }

        public record Fila(Guid Id, string Folio, int CasoId, string Alumno, string GradoGrupo, TipoComprobante Tipo, DateTime SubidoEn, EstadoComprobante Estado, string? Motivo);

        public async Task<IActionResult> Index(EstadoComprobante estado = EstadoComprobante.Pendiente)
        {
            ViewBag.Estado = estado;
            ViewBag.Pendientes = await _db.Comprobantes.CountAsync(c => c.Estado == EstadoComprobante.Pendiente);
            var lista = await _db.Comprobantes.AsNoTracking().Where(c => c.Estado == estado)
                .OrderBy(c => c.SubidoEn)
                .Select(c => new Fila(c.Id, c.Caso!.Folio, c.CasoId, c.Caso.Alumno!.Nombre + " " + c.Caso.Alumno.Apellidos,
                    c.Caso.Alumno.Grado + "° " + c.Caso.Alumno.Grupo, c.Tipo, c.SubidoEn, c.Estado, c.MotivoRechazo))
                .Take(200).ToListAsync();
            return View(lista);
        }

        public async Task<IActionResult> Revisar(Guid id)
        {
            var fila = await _db.Comprobantes.AsNoTracking().Where(c => c.Id == id)
                .Select(c => new { Fila = new Fila(c.Id, c.Caso!.Folio, c.CasoId, c.Caso.Alumno!.Nombre + " " + c.Caso.Alumno.Apellidos,
                    c.Caso.Alumno.Grado + "° " + c.Caso.Alumno.Grupo, c.Tipo, c.SubidoEn, c.Estado, c.MotivoRechazo), c.ContentType, c.TamanoBytes })
                .FirstOrDefaultAsync();
            if (fila is null) return NotFound();
            ViewBag.ContentType = fila.ContentType;
            ViewBag.Tamano = fila.TamanoBytes;
            return View(fila.Fila);
        }

        /// <summary>El archivo descifrado. Se registra quién lo abrió (C-RC-07).</summary>
        public async Task<IActionResult> Archivo(Guid id)
        {
            var c = await _db.Comprobantes.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return NotFound();
            _auditoria.Registrar("VER_COMPROBANTE", "Caso", c.CasoId, c.Id.ToString());
            await _db.SaveChangesAsync();
            Response.Headers.CacheControl = "no-store";
            return File(c.Contenido, c.ContentType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aprobar(Guid id)
        {
            var c = await _db.Comprobantes.Include(x => x.Caso).ThenInclude(x => x!.Alumno).FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return NotFound();
            if (c.Estado != EstadoComprobante.Pendiente) { TempData["Error"] = "Este comprobante ya fue revisado."; return RedirectToAction(nameof(Index)); }
            c.Estado = EstadoComprobante.Aprobado;
            c.RevisadoPor = User.Identity?.Name;
            c.RevisadoEn = DateTime.UtcNow;
            _expedientes.Evento(c.CasoId, TipoEvento.Comprobante, $"{c.Tipo.Texto()} validada por Enfermería.", User.Identity?.Name);
            if (c.Caso!.Estado == EstadoCaso.Abierto) c.Caso.Estado = EstadoCaso.EnSeguimiento;
            await _notificador.ATutorDelAlumnoAsync(c.Caso.AlumnoId, "COMPROBANTE_REVISADO", "Comprobante aceptado",
                $"Enfermería aceptó el comprobante de {c.Caso.Alumno!.Nombre}. ¡Gracias!",
                new Dictionary<string, string> { ["ruta"] = $"tamizaje/{c.Caso.TamizajeId}", ["comprobanteId"] = c.Id.ToString() });
            _auditoria.Registrar("APROBAR_COMPROBANTE", "Caso", c.CasoId, c.Id.ToString());
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Comprobante aprobado. Ya puedes registrar la atención o cerrar el caso.";
            return RedirectToAction("Details", "Casos", new { id = c.CasoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rechazar(Guid id, string? motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 5)
            {
                TempData["Error"] = "Escribe el motivo del rechazo para que el tutor sepa qué corregir.";
                return RedirectToAction(nameof(Revisar), new { id });
            }
            var c = await _db.Comprobantes.Include(x => x.Caso).ThenInclude(x => x!.Alumno).FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return NotFound();
            if (c.Estado != EstadoComprobante.Pendiente) { TempData["Error"] = "Este comprobante ya fue revisado."; return RedirectToAction(nameof(Index)); }
            c.Estado = EstadoComprobante.Rechazado;
            c.MotivoRechazo = motivo.Trim().Length > 300 ? motivo.Trim()[..300] : motivo.Trim();
            c.RevisadoPor = User.Identity?.Name;
            c.RevisadoEn = DateTime.UtcNow;
            _expedientes.Evento(c.CasoId, TipoEvento.Comprobante, $"{c.Tipo.Texto()} rechazada: {c.MotivoRechazo}. Se pidió un nuevo envío.", User.Identity?.Name);
            // W-RFNN-02: se pide al tutor que lo envíe de nuevo.
            await _notificador.ATutorDelAlumnoAsync(c.Caso!.AlumnoId, "COMPROBANTE_REVISADO", "Comprobante rechazado",
                $"Enfermería no pudo aceptar el comprobante de {c.Caso.Alumno!.Nombre}: {c.MotivoRechazo}. Por favor sube uno nuevo.",
                new Dictionary<string, string> { ["ruta"] = $"tamizaje/{c.Caso.TamizajeId}", ["comprobanteId"] = c.Id.ToString() });
            _auditoria.Registrar("RECHAZAR_COMPROBANTE", "Caso", c.CasoId, c.MotivoRechazo);
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Comprobante rechazado. Se pidió al tutor un nuevo envío.";
            return RedirectToAction(nameof(Index));
        }
    }
}
