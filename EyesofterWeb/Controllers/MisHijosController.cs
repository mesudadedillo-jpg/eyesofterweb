using EyesofterWeb.Data;
using EyesofterWeb.Helpers;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Portal del tutor (W-RFN-06 a W-RFN-08). C-RC-06: cada acción comprueba que el alumno
    /// esté a cargo del tutor de la sesión; si no, responde 404 y lo registra (C-RFNN-06).
    /// </summary>
    [Authorize(Roles = Roles.Tutor)]
    public class MisHijosController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _usuarios;
        private readonly Expedientes _expedientes;
        private readonly Notificador _notificador;
        private readonly Auditoria _auditoria;
        private readonly Documentos _documentos;

        public MisHijosController(ApplicationDbContext db, UserManager<ApplicationUser> usuarios, Expedientes expedientes,
            Notificador notificador, Auditoria auditoria, Documentos documentos)
        {
            _db = db;
            _usuarios = usuarios;
            _expedientes = expedientes;
            _notificador = notificador;
            _auditoria = auditoria;
            _documentos = documentos;
        }

        private async Task<int?> TutorIdAsync() => (await _usuarios.GetUserAsync(User))?.TutorId;

        public async Task<IActionResult> Index()
        {
            var tutorId = await TutorIdAsync();
            var alumnos = tutorId is null ? new List<Alumno>() : await _db.Alumnos.AsNoTracking()
                .Where(a => a.TutorId == tutorId)
                .Include(a => a.Tamizajes)
                .Include(a => a.Casos)
                .OrderBy(a => a.Nombre).ToListAsync();
            ViewBag.SinVinculo = tutorId is null;
            return View(alumnos);
        }

        /// <summary>Carga el tamizaje solo si es de un hijo del tutor; si no, deja constancia y devuelve null.</summary>
        private async Task<Tamizaje?> TamizajePropioAsync(int tamizajeId)
        {
            var tutorId = await TutorIdAsync();
            var t = await _db.Tamizajes.Include(x => x.Alumno).FirstOrDefaultAsync(x => x.Id == tamizajeId);
            if (t is null) return null;
            if (tutorId is null || t.Alumno!.TutorId != tutorId)
            {
                await _auditoria.DenegadoAsync("Tamizaje", tamizajeId, "Un tutor intentó ver el resultado de un alumno que no está a su cargo.");
                return null;
            }
            return t;
        }

        private Task<Caso?> CasoDeAsync(int tamizajeId) =>
            _db.Casos.Include(c => c.Alumno).Include(c => c.Clinica)
                .Include(c => c.Pases).ThenInclude(p => p.Clinica)
                .Include(c => c.Atenciones).ThenInclude(a => a.Clinica)
                .Include(c => c.Citas)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.TamizajeId == tamizajeId);

        public async Task<IActionResult> Reporte(int id)
        {
            var t = await TamizajePropioAsync(id);
            if (t is null) return NotFound();
            var caso = await CasoDeAsync(id);
            _auditoria.Registrar("VER_REPORTE_TUTOR", "Tamizaje", id, t.Alumno!.NombreCompleto);
            await _db.SaveChangesAsync();
            ViewBag.Caso = caso;
            ViewBag.Comprobantes = caso is null ? new List<Comprobante>() : await _db.Comprobantes.AsNoTracking().Where(c => c.CasoId == caso.Id)
                .OrderByDescending(c => c.SubidoEn)
                .Select(c => new Comprobante { Id = c.Id, Tipo = c.Tipo, SubidoEn = c.SubidoEn, Estado = c.Estado, MotivoRechazo = c.MotivoRechazo })
                .ToListAsync();
            ViewBag.Clinicas = await _db.Clinicas.AsNoTracking().Where(c => c.Activa).OrderByDescending(c => c.EsAsociada).ThenBy(c => c.Nombre).ToListAsync();
            return View(t);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Destino(int id, int? clinicaId, string? nombreManual)
        {
            var t = await TamizajePropioAsync(id);
            var caso = t is null ? null : await CasoDeAsync(id);
            if (caso is null) return NotFound();
            if (caso.Estado == EstadoCaso.Cerrado) return RedirectToAction(nameof(Reporte), new { id });
            if (clinicaId is null && string.IsNullOrWhiteSpace(nombreManual))
            {
                TempData["Error"] = "Elige una clínica del directorio o escribe el nombre de la clínica.";
                return RedirectToAction(nameof(Reporte), new { id });
            }
            var pase = await _expedientes.AsignarDestinoAsync(caso, clinicaId, nombreManual?.Trim(), User.Identity?.Name);
            await _notificador.ARolAsync(Roles.Enfermeria, "CASO_ACTUALIZADO", "Clínica de destino elegida",
                $"El tutor de {caso.Alumno!.NombreCompleto} indicó a dónde acudirá.", new Dictionary<string, string> { ["ruta"] = "seguimiento", ["casoId"] = caso.Id.ToString() });
            await _db.SaveChangesAsync();
            TempData["Exito"] = pase is null
                ? "Guardamos la clínica. Como no está asociada al programa, lleva el resumen del tamizaje a la consulta."
                : "Listo. Ya tienes el pase médico con QR: muéstralo en la clínica (desde el teléfono o impreso).";
            return RedirectToAction(nameof(Reporte), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cita(int id, int clinicaId, DateTime fechaPreferida, string? horario, string? comentario)
        {
            var t = await TamizajePropioAsync(id);
            var caso = t is null ? null : await CasoDeAsync(id);
            if (caso is null) return NotFound();
            var clinica = await _db.Clinicas.FirstOrDefaultAsync(c => c.Id == clinicaId && c.Activa);
            if (clinica is null || fechaPreferida.Date < DateTime.Today)
            {
                TempData["Error"] = "Elige una clínica y una fecha a partir de hoy.";
                return RedirectToAction(nameof(Reporte), new { id });
            }
            _db.Citas.Add(new Cita
            {
                CasoId = caso.Id, ClinicaId = clinica.Id, FechaPreferida = fechaPreferida.Date, Horario = horario?.Trim() is { Length: > 0 } h ? h[..Math.Min(h.Length, 20)] : null,
                Comentario = comentario?.Trim() is { Length: > 0 } c ? c[..Math.Min(c.Length, 300)] : null, SolicitadaPorId = _usuarios.GetUserId(User)!
            });
            _expedientes.Evento(caso.Id, TipoEvento.Cita, $"El tutor solicitó cita en {clinica.Nombre} para el {fechaPreferida:dd/MM/yyyy}.", User.Identity?.Name);
            await _notificador.ARolAsync(Roles.Enfermeria, "CITA", "Nueva solicitud de cita",
                $"Cita solicitada para {caso.Alumno!.NombreCompleto} en {clinica.Nombre}.", new Dictionary<string, string> { ["ruta"] = "seguimiento", ["casoId"] = caso.Id.ToString() });
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Solicitud enviada. Llama a {clinica.Nombre} al {clinica.Telefono} si necesitas confirmarla antes.";
            return RedirectToAction(nameof(Reporte), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(TipoArchivo.MaximoBytes + 512 * 1024)]
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> Comprobante(int id, TipoComprobante tipo, IFormFile? archivo)
        {
            var t = await TamizajePropioAsync(id);
            var caso = t is null ? null : await CasoDeAsync(id);
            if (caso is null) return NotFound();
            if (archivo is null || archivo.Length == 0) { TempData["Error"] = "Elige la foto o el PDF."; return RedirectToAction(nameof(Reporte), new { id }); }
            if (archivo.Length > TipoArchivo.MaximoBytes) { TempData["Error"] = "El archivo pesa más de 10 MB. Toma la foto con menor resolución."; return RedirectToAction(nameof(Reporte), new { id }); }
            using var ms = new MemoryStream();
            await archivo.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var tipoReal = TipoArchivo.Detectar(bytes);
            if (tipoReal is null) { TempData["Error"] = "Solo se aceptan fotos (JPG, PNG, WEBP) o PDF."; return RedirectToAction(nameof(Reporte), new { id }); }

            var comprobante = new Comprobante { CasoId = caso.Id, SubidoPorId = _usuarios.GetUserId(User)!, Tipo = tipo, ContentType = tipoReal, TamanoBytes = bytes.Length, Contenido = bytes };
            _db.Comprobantes.Add(comprobante);
            _expedientes.Evento(caso.Id, TipoEvento.Comprobante, $"El tutor subió {(tipo == TipoComprobante.Receta ? "la receta" : "el comprobante de lentes")} desde el portal.", User.Identity?.Name);
            await _notificador.ARolAsync(Roles.Enfermeria, "COMPROBANTE_RECIBIDO", "Nuevo comprobante por revisar",
                $"El tutor de {caso.Alumno!.NombreCompleto} subió {(tipo == TipoComprobante.Receta ? "una receta" : "un comprobante de lentes")}.",
                new Dictionary<string, string> { ["ruta"] = "comprobantes", ["comprobanteId"] = comprobante.Id.ToString() });
            _auditoria.Registrar("SUBIR_COMPROBANTE", "Caso", caso.Id, comprobante.Id.ToString());
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Listo. Enfermería revisará tu comprobante y te avisaremos.";
            return RedirectToAction(nameof(Reporte), new { id });
        }

        public async Task<IActionResult> PasePdf(int id)
        {
            var t = await TamizajePropioAsync(id);
            var caso = t is null ? null : await CasoDeAsync(id);
            var pase = caso?.Pases.Where(p => p.EstaVigente).OrderByDescending(p => p.CreadoEn).FirstOrDefault();
            if (pase is null) return NotFound();
            return File(_documentos.PdfPase(pase, caso!), "application/pdf", $"pase-{caso!.Folio}.pdf");
        }

        public async Task<IActionResult> QrPase(int id)
        {
            var t = await TamizajePropioAsync(id);
            var caso = t is null ? null : await CasoDeAsync(id);
            var pase = caso?.Pases.Where(p => p.EstaVigente).OrderByDescending(p => p.CreadoEn).FirstOrDefault();
            if (pase is null) return NotFound();
            Response.Headers.CacheControl = "no-store";
            return File(_documentos.QrPng(_documentos.UrlPase(pase.Codigo)), "image/png");
        }

        public async Task<IActionResult> ResumenPdf(int id)
        {
            var t = await TamizajePropioAsync(id);
            var caso = t is null ? null : await CasoDeAsync(id);
            if (caso is null) return NotFound();
            return File(_documentos.PdfResumen(caso), "application/pdf", $"resumen-{caso.Folio}.pdf");
        }

        /// <summary>Directorio de clínicas: asociadas primero (M-RFN-07 también en la web).</summary>
        public async Task<IActionResult> Directorio(string? buscar)
        {
            var q = _db.Clinicas.AsNoTracking().Where(c => c.Activa);
            if (!string.IsNullOrWhiteSpace(buscar))
                q = q.Where(c => c.Nombre.Contains(buscar) || c.Ciudad.Contains(buscar) || c.Direccion.Contains(buscar) || (c.CodigoPostal != null && c.CodigoPostal.Contains(buscar)));
            ViewBag.Buscar = buscar;
            return View(await q.OrderByDescending(c => c.EsAsociada).ThenBy(c => c.Nombre).ToListAsync());
        }
    }
}
