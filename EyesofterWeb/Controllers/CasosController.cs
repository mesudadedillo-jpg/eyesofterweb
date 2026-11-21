using EyesofterWeb.Data;
using EyesofterWeb.Helpers;
using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Expedientes de seguimiento visual (W-RFN-01 a W-RFN-04, W-RFN-10).
    /// Solo personal escolar: Dirección y Enfermería. Reabrir y eliminar: solo Dirección.
    /// </summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class CasosController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly Expedientes _expedientes;
        private readonly Notificador _notificador;
        private readonly Auditoria _auditoria;
        private readonly Documentos _documentos;

        public CasosController(ApplicationDbContext db, Expedientes expedientes, Notificador notificador, Auditoria auditoria, Documentos documentos)
        {
            _db = db;
            _expedientes = expedientes;
            _notificador = notificador;
            _auditoria = auditoria;
            _documentos = documentos;
        }

        private string? Usuario => User.Identity?.Name;

        // ---------- Panel filtrable (W-RFN-03) y exportación (W-RI-03) ----------

        public async Task<IActionResult> Index([FromQuery] FiltroCasos filtro, string? formato)
        {
            var casos = await Filtrar(filtro).ToListAsync();
            if (formato is "excel" or "pdf")
            {
                // Una lista de muchos alumnos que sale del sistema: queda en la bitácora quién y cuándo.
                _auditoria.Registrar("EXPORTAR_CASOS", "Caso", null, $"{formato}: {casos.Count} caso(s). {filtro.Descripcion()}");
                await _db.SaveChangesAsync();
            }
            if (formato == "excel")
                return File(_documentos.ExcelCasos(casos), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"casos-{DateTime.Today:yyyyMMdd}.xlsx");
            if (formato == "pdf")
                return File(_documentos.PdfCasos(casos, filtro.Descripcion()), "application/pdf", $"casos-{DateTime.Today:yyyyMMdd}.pdf");

            ViewBag.Filtro = filtro;
            ViewBag.Campanas = await _db.Campanas.OrderByDescending(c => c.FechaInicio).ToListAsync();
            ViewBag.Abiertos = await _db.Casos.CountAsync(c => c.Estado != EstadoCaso.Cerrado);
            ViewBag.Resueltos = await _db.Casos.CountAsync(c => c.Estado == EstadoCaso.Cerrado);
            return View(casos);
        }

        private IQueryable<Caso> Filtrar(FiltroCasos f)
        {
            var q = _db.Casos.AsNoTracking().Include(c => c.Alumno).Include(c => c.Clinica).AsQueryable();
            if (!string.IsNullOrWhiteSpace(f.Buscar))
            {
                var b = f.Buscar.Trim();
                q = q.Where(c => c.Folio.Contains(b) || c.Alumno!.Nombre.Contains(b) || c.Alumno!.Apellidos.Contains(b) || c.Alumno!.Matricula.Contains(b));
            }
            if (f.Vista == "abiertos") q = q.Where(c => c.Estado != EstadoCaso.Cerrado);
            if (f.Vista == "resueltos") q = q.Where(c => c.Estado == EstadoCaso.Cerrado);
            if (f.Estado.HasValue) q = q.Where(c => c.Estado == f.Estado);
            if (f.Semaforo.HasValue) q = q.Where(c => c.Semaforo == f.Semaforo);
            if (f.Nivel.HasValue) q = q.Where(c => c.Alumno!.Nivel == f.Nivel);
            if (f.Grado.HasValue) q = q.Where(c => c.Alumno!.Grado == f.Grado);
            if (!string.IsNullOrWhiteSpace(f.Grupo)) q = q.Where(c => c.Alumno!.Grupo == f.Grupo.ToUpper());
            if (f.CampanaId.HasValue) q = q.Where(c => c.Tamizaje != null && c.Tamizaje.CampanaId == f.CampanaId);
            return q.OrderBy(c => c.Estado == EstadoCaso.Cerrado).ThenByDescending(c => c.Semaforo).ThenByDescending(c => c.FechaDeteccion);
        }

        // ---------- Expediente ----------

        public async Task<IActionResult> Details(int? id)
        {
            if (id is null) return NotFound();
            var caso = await _db.Casos
                .Include(c => c.Alumno).ThenInclude(a => a!.Tutor)
                .Include(c => c.Clinica)
                .Include(c => c.Tamizaje)
                .Include(c => c.Eventos)
                .Include(c => c.Pases).ThenInclude(p => p.Clinica)
                .Include(c => c.Atenciones).ThenInclude(a => a.Clinica)
                .Include(c => c.Citas).ThenInclude(c => c.Clinica)
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (caso is null) return NotFound();

            // C-RC-07: queda registrado quién consultó el expediente.
            _auditoria.Registrar("VER_EXPEDIENTE", "Caso", caso.Id, caso.Folio);
            await _db.SaveChangesAsync();

            var (puede, motivo) = await _expedientes.PuedeCerrarAsync(caso.Id);
            var vigente = caso.Pases.FirstOrDefault(p => p.EstaVigente);
            return View(new CasoDetalleViewModel
            {
                Caso = caso,
                Comprobantes = await ComprobantesDe(caso.Id),
                PaseVigente = vigente,
                UrlPase = vigente is null ? null : _documentos.UrlPase(vigente.Codigo),
                PuedeCerrar = puede,
                MotivoNoCierre = motivo
            });
        }

        private Task<List<ComprobanteResumen>> ComprobantesDe(int casoId) =>
            _db.Comprobantes.AsNoTracking().Where(c => c.CasoId == casoId).OrderByDescending(c => c.SubidoEn)
                .Select(c => new ComprobanteResumen(c.Id, c.Tipo, c.ContentType, c.TamanoBytes, c.SubidoEn, c.Estado, c.RevisadoPor, c.RevisadoEn, c.MotivoRechazo))
                .ToListAsync();

        // ---------- Alta manual (el escritorio crea los casos solo, W-RFN-01) ----------

        public async Task<IActionResult> Create(int? alumnoId)
        {
            await CargarListasAsync(alumnoId, null);
            return View(new Caso { AlumnoId = alumnoId ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AlumnoId,ClinicaId,Semaforo,FechaDeteccion,AgudezaOjoDerecho,AgudezaOjoIzquierdo,Observaciones")] Caso caso, int? campanaId)
        {
            if (caso.AlumnoId <= 0 || !await _db.Alumnos.AnyAsync(a => a.Id == caso.AlumnoId))
            {
                if (ModelState.GetFieldValidationState(nameof(Caso.AlumnoId)) != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
                    ModelState.AddModelError(nameof(Caso.AlumnoId), "Selecciona un alumno válido.");
            }
            // Regla de negocio: un alumno solo puede tener un caso activo a la vez.
            else if (await _db.Casos.AnyAsync(c => c.AlumnoId == caso.AlumnoId && c.Estado != EstadoCaso.Cerrado))
            {
                ModelState.AddModelError(nameof(Caso.AlumnoId), "Este alumno ya tiene un caso activo. Dale seguimiento a ese caso o ciérralo primero.");
            }
            if (caso.Semaforo == Semaforo.Verde)
                ModelState.AddModelError(nameof(Caso.Semaforo), "Un resultado verde no abre expediente: no hay indicios que seguir.");
            await ValidarClinicaAsync(caso.ClinicaId);
            // La campaña debe ser de la escuela del alumno (Campanas viene filtrado por escuela).
            if (campanaId is not null && caso.AlumnoId > 0)
            {
                var plantelAlumno = await _db.Alumnos.Where(a => a.Id == caso.AlumnoId).Select(a => (int?)a.PlantelId).FirstOrDefaultAsync();
                if (!await _db.Campanas.AnyAsync(c => c.Id == campanaId && c.PlantelId == plantelAlumno))
                    ModelState.AddModelError(string.Empty, "La campaña elegida no es de la escuela del alumno.");
            }

            if (!ModelState.IsValid)
            {
                await CargarListasAsync(caso.AlumnoId, caso.ClinicaId);
                return View(caso);
            }

            await using var transaccion = await _db.Database.BeginTransactionAsync();
            caso.Folio = await _expedientes.GenerarFolioAsync();
            caso.Estado = caso.ClinicaId.HasValue ? EstadoCaso.EnSeguimiento : EstadoCaso.Abierto;
            caso.RegistradoPor = Usuario;
            // Todo caso tiene su tamizaje: así cuenta para los indicadores y la app lo muestra.
            caso.Tamizaje = new Tamizaje
            {
                Folio = "WEB-" + caso.Folio, AlumnoId = caso.AlumnoId, CampanaId = campanaId, Fecha = caso.FechaDeteccion.Date,
                Semaforo = caso.Semaforo, AgudezaOjoDerecho = caso.AgudezaOjoDerecho, AgudezaOjoIzquierdo = caso.AgudezaOjoIzquierdo,
                Observaciones = caso.Observaciones, Origen = "web"
            };
            caso.Eventos.Add(new EventoCaso
            {
                Tipo = TipoEvento.Tamizaje, Fecha = caso.FechaDeteccion.Date, RegistradoPor = Usuario,
                Descripcion = $"Tamizaje Snellen: OD {caso.AgudezaOjoDerecho}, OI {caso.AgudezaOjoIzquierdo}. Semáforo {caso.Semaforo}."
            });
            _db.Casos.Add(caso);
            await _db.SaveChangesAsync();

            // W-RFN-02: alerta al tutor (llega por la app y queda en la línea de tiempo).
            var alumno = await _db.Alumnos.FindAsync(caso.AlumnoId);
            var avisados = await _notificador.ATutorDelAlumnoAsync(caso.AlumnoId, "RESULTADO_TAMIZAJE", "Resultado del tamizaje visual",
                $"Ya está disponible el resultado del tamizaje visual de {alumno!.Nombre}. Tócala para verlo.",
                new Dictionary<string, string> { ["ruta"] = $"tamizaje/{caso.TamizajeId}", ["tamizajeId"] = caso.TamizajeId.ToString()!, ["alumno"] = alumno.NombreCompleto, ["casoId"] = caso.Id.ToString() });
            _expedientes.Evento(caso.Id, TipoEvento.AvisoTutor,
                avisados > 0 ? "Se notificó el resultado al tutor por la app y el portal." : "El tutor no tiene cuenta: avísale por teléfono.", "sistema");
            _auditoria.Registrar("CREAR_CASO", "Caso", caso.Id, caso.Folio);
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();

            TempData["Exito"] = $"Caso {caso.Folio} creado." + (avisados > 0 ? " Se avisó al tutor." : " El tutor no tiene cuenta en el sistema: avísale por teléfono.");
            return RedirectToAction(nameof(Details), new { id = caso.Id });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();
            var caso = await _db.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == id);
            if (caso is null) return NotFound();
            if (caso.Estado == EstadoCaso.Cerrado)
            {
                TempData["Error"] = "Un caso cerrado no se puede editar. La Dirección puede reabrirlo.";
                return RedirectToAction(nameof(Details), new { id });
            }
            await CargarListasAsync(caso.AlumnoId, caso.ClinicaId);
            return View(caso);
        }

        /// <summary>W-RFN-04: actualizar el estado del caso.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ClinicaId,Semaforo,Estado,FechaDeteccion,AgudezaOjoDerecho,AgudezaOjoIzquierdo,Observaciones")] Caso datos)
        {
            if (id != datos.Id) return NotFound();
            var caso = await _db.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == id);
            if (caso is null) return NotFound();
            if (caso.Estado == EstadoCaso.Cerrado)
            {
                TempData["Error"] = "Un caso cerrado no se puede editar.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (datos.Estado == EstadoCaso.Cerrado)
                ModelState.AddModelError(nameof(Caso.Estado), "Para cerrar un caso usa el botón \"Cerrar caso\" (pide el resultado).");
            await ValidarClinicaAsync(datos.ClinicaId);

            if (!ModelState.IsValid)
            {
                datos.Folio = caso.Folio;
                datos.AlumnoId = caso.AlumnoId;
                datos.Alumno = caso.Alumno;
                await CargarListasAsync(caso.AlumnoId, datos.ClinicaId);
                return View(datos);
            }

            if (caso.Semaforo != datos.Semaforo)
                _expedientes.Evento(caso.Id, TipoEvento.Seguimiento, $"Semáforo cambió de {caso.Semaforo} a {datos.Semaforo}.", Usuario);
            if (caso.Estado != datos.Estado)
            {
                _expedientes.Evento(caso.Id, TipoEvento.Seguimiento, $"Estado cambió de {caso.Estado.Texto()} a {datos.Estado.Texto()}.", Usuario);
                await AvisarCambioAsync(caso, $"El caso de {caso.Alumno!.Nombre} ahora está: {datos.Estado.Texto()}.");
            }
            caso.ClinicaId = datos.ClinicaId;
            caso.Semaforo = datos.Semaforo;
            caso.Estado = datos.Estado;
            caso.FechaDeteccion = datos.FechaDeteccion.Date;
            caso.AgudezaOjoDerecho = datos.AgudezaOjoDerecho;
            caso.AgudezaOjoIzquierdo = datos.AgudezaOjoIzquierdo;
            caso.Observaciones = datos.Observaciones;
            _auditoria.Registrar("EDITAR_CASO", "Caso", caso.Id, caso.Folio);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Caso {caso.Folio} actualizado.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- Clínica de destino: pase con QR o resumen (C-RFN-04 a 06) ----------

        public async Task<IActionResult> Destino(int id)
        {
            var caso = await _db.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == id);
            if (caso is null) return NotFound();
            await CargarClinicasAsync(caso.ClinicaId);
            return View(new DestinoViewModel { CasoId = caso.Id, Folio = caso.Folio, Alumno = caso.Alumno!.NombreCompleto, ClinicaId = caso.ClinicaId, NombreManual = caso.ClinicaDestinoManual });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Destino(DestinoViewModel modelo)
        {
            var caso = await _db.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == modelo.CasoId);
            if (caso is null) return NotFound();
            if (caso.Estado == EstadoCaso.Cerrado) return RedirectToAction(nameof(Details), new { id = caso.Id });
            if (modelo.ClinicaId is null && string.IsNullOrWhiteSpace(modelo.NombreManual))
                ModelState.AddModelError(string.Empty, "Elige una clínica del directorio o escribe el nombre de la clínica.");
            if (!ModelState.IsValid)
            {
                await CargarClinicasAsync(modelo.ClinicaId);
                return View(modelo);
            }
            var pase = await _expedientes.AsignarDestinoAsync(caso, modelo.ClinicaId, modelo.NombreManual, Usuario);
            await _db.SaveChangesAsync();
            await AvisarCambioAsync(caso, pase is null
                ? $"Se registró la clínica de destino de {caso.Alumno!.Nombre}. Presenta el resumen del tamizaje en la consulta."
                : $"Ya está listo el pase médico de {caso.Alumno!.Nombre}. Muéstralo en la clínica.");
            await _db.SaveChangesAsync();
            TempData["Exito"] = pase is null ? "Destino guardado: la clínica no es asociada, se entrega el resumen del tamizaje." : "Destino guardado y pase médico con QR generado.";
            return RedirectToAction(nameof(Details), new { id = caso.Id });
        }

        public async Task<IActionResult> PasePdf(int id)
        {
            var pase = await _db.Pases.Include(p => p.Clinica).Include(p => p.Caso).ThenInclude(c => c!.Alumno)
                .Where(p => p.CasoId == id && p.Estado == EstadoPase.Vigente).OrderByDescending(p => p.CreadoEn).FirstOrDefaultAsync();
            if (pase is null) return NotFound();
            var convenio = await _expedientes.ConvenioVigenteAsync(pase.Caso!.AlumnoId, pase.ClinicaId);
            return File(_documentos.PdfPase(pase, pase.Caso!, convenio), "application/pdf", $"pase-{pase.Caso!.Folio}.pdf");
        }

        public async Task<IActionResult> ResumenPdf(int id)
        {
            var caso = await _db.Casos.Include(c => c.Alumno).Include(c => c.Clinica).FirstOrDefaultAsync(c => c.Id == id);
            if (caso is null) return NotFound();
            return File(_documentos.PdfResumen(caso), "application/pdf", $"resumen-{caso.Folio}.pdf");
        }

        public async Task<IActionResult> QrPase(int id)
        {
            var pase = await _db.Pases.Where(p => p.CasoId == id && p.Estado == EstadoPase.Vigente).OrderByDescending(p => p.CreadoEn).FirstOrDefaultAsync();
            if (pase is null) return NotFound();
            return File(_documentos.QrPng(_documentos.UrlPase(pase.Codigo)), "image/png");
        }

        // ---------- Atención en clínica NO asociada a partir del comprobante validado (W-RFN-10) ----------

        public async Task<IActionResult> AtencionManual(int id)
        {
            var caso = await _db.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == id);
            if (caso is null) return NotFound();
            if (!await CargarComprobantesValidadosAsync(id))
            {
                TempData["Error"] = "Primero valida el comprobante o la receta que subió el tutor.";
                return RedirectToAction(nameof(Details), new { id });
            }
            await CargarClinicasAsync(caso.ClinicaId);
            return View(new AtencionManualViewModel { CasoId = id, Folio = caso.Folio, Alumno = caso.Alumno!.NombreCompleto, ClinicaId = caso.ClinicaId, ClinicaNombre = caso.ClinicaDestinoManual });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtencionManual(AtencionManualViewModel m)
        {
            var caso = await _db.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == m.CasoId);
            if (caso is null) return NotFound();
            if (m.ComprobanteId is null || !await _db.Comprobantes.AnyAsync(c => c.Id == m.ComprobanteId && c.CasoId == caso.Id && c.Estado == EstadoComprobante.Aprobado))
                ModelState.AddModelError(nameof(m.ComprobanteId), "Selecciona un comprobante validado de este caso.");
            if (m.ClinicaId is null && string.IsNullOrWhiteSpace(m.ClinicaNombre))
                ModelState.AddModelError(nameof(m.ClinicaNombre), "Indica la clínica donde se atendió.");
            if (m.Fecha.Date > DateTime.Today) ModelState.AddModelError(nameof(m.Fecha), "La fecha no puede ser futura.");
            if (m.Fecha.Date < caso.FechaDeteccion) ModelState.AddModelError(nameof(m.Fecha), "La atención no puede ser anterior a la detección.");
            if (!ModelState.IsValid)
            {
                await CargarComprobantesValidadosAsync(caso.Id);
                await CargarClinicasAsync(m.ClinicaId);
                m.Folio = caso.Folio;
                m.Alumno = caso.Alumno!.NombreCompleto;
                return View(m);
            }
            await _expedientes.RegistrarAtencionAsync(caso, new Atencion
            {
                ClinicaId = m.ClinicaId, ClinicaNombre = m.ClinicaId is null ? m.ClinicaNombre!.Trim() : null, Fecha = m.Fecha.Date,
                Valoracion = m.Valoracion.Trim(), Indicaciones = m.Indicaciones?.Trim(), CorreccionOptica = m.CorreccionOptica,
                Origen = OrigenAtencion.Manual, ComprobanteId = m.ComprobanteId
            }, Usuario);
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Atención registrada.";
            return RedirectToAction(nameof(Details), new { id = caso.Id });
        }

        // ---------- Citas solicitadas por el tutor ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EstadoCita(int id, EstadoCita estado)
        {
            var cita = await _db.Citas.Include(c => c.Caso).ThenInclude(c => c!.Alumno).Include(c => c.Clinica).FirstOrDefaultAsync(c => c.Id == id);
            if (cita is null) return NotFound();
            cita.Estado = estado;
            if (estado == Models.EstadoCita.Confirmada)
                _expedientes.Evento(cita.CasoId, TipoEvento.Cita, $"Cita confirmada en {cita.Clinica?.Nombre ?? "la clínica"} para el {cita.FechaPreferida:dd/MM/yyyy}{(cita.Horario is null ? "" : " " + cita.Horario)}.", Usuario);
            await AvisarCambioAsync(cita.Caso!, $"Tu solicitud de cita para {cita.Caso!.Alumno!.Nombre} ahora está: {estado}.");
            _auditoria.Registrar("ESTADO_CITA", "Caso", cita.CasoId, estado.ToString());
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Cita actualizada y se avisó al tutor.";
            return RedirectToAction(nameof(Details), new { id = cita.CasoId });
        }

        // ---------- Cierre (W-RFN-04) ----------

        public async Task<IActionResult> Cerrar(int? id)
        {
            if (id is null) return NotFound();
            var caso = await _db.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == id);
            if (caso is null) return NotFound();
            if (caso.Estado == EstadoCaso.Cerrado) return RedirectToAction(nameof(Details), new { id });
            var (puede, motivo) = await _expedientes.PuedeCerrarAsync(caso.Id);
            if (!puede)
            {
                TempData["Error"] = motivo;
                return RedirectToAction(nameof(Details), new { id });
            }
            return View(new CerrarCasoViewModel { CasoId = caso.Id, Folio = caso.Folio, Alumno = caso.Alumno?.NombreCompleto ?? string.Empty });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cerrar(CerrarCasoViewModel modelo)
        {
            var caso = await _db.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == modelo.CasoId);
            if (caso is null) return NotFound();
            var (puede, motivo) = await _expedientes.PuedeCerrarAsync(caso.Id);
            if (!puede) ModelState.AddModelError(string.Empty, motivo!);
            if (!ModelState.IsValid)
            {
                modelo.Folio = caso.Folio;
                modelo.Alumno = caso.Alumno?.NombreCompleto ?? string.Empty;
                return View(modelo);
            }
            caso.Estado = EstadoCaso.Cerrado;
            caso.FechaCierre = DateTime.Today < caso.FechaDeteccion ? caso.FechaDeteccion : DateTime.Today;
            _expedientes.Evento(caso.Id, TipoEvento.Cierre, modelo.Comentario, Usuario, caso.FechaCierre);
            await AvisarCambioAsync(caso, $"El caso de {caso.Alumno!.Nombre} se cerró. ¡Gracias por darle seguimiento!");
            _auditoria.Registrar("CERRAR_CASO", "Caso", caso.Id, caso.Folio);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Caso {caso.Folio} cerrado.";
            return RedirectToAction(nameof(Details), new { id = caso.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Reabrir(int id)
        {
            var caso = await _db.Casos.FindAsync(id);
            if (caso is null) return NotFound();
            if (caso.Estado != EstadoCaso.Cerrado) return RedirectToAction(nameof(Details), new { id });
            if (await _db.Casos.AnyAsync(c => c.AlumnoId == caso.AlumnoId && c.Id != caso.Id && c.Estado != EstadoCaso.Cerrado))
            {
                TempData["Error"] = "No se puede reabrir: el alumno ya tiene otro caso activo.";
                return RedirectToAction(nameof(Details), new { id });
            }
            caso.Estado = EstadoCaso.EnSeguimiento;
            caso.FechaCierre = null;
            _expedientes.Evento(caso.Id, TipoEvento.Reapertura, "La Dirección reabrió el caso.", Usuario);
            _auditoria.Registrar("REABRIR_CASO", "Caso", caso.Id, caso.Folio);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Caso {caso.Folio} reabierto.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();
            var caso = await _db.Casos.Include(c => c.Alumno).Include(c => c.Clinica).Include(c => c.Eventos).FirstOrDefaultAsync(m => m.Id == id);
            return caso is null ? NotFound() : View(caso);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var caso = await _db.Casos.FindAsync(id);
            if (caso is not null)
            {
                _db.Casos.Remove(caso);
                _auditoria.Registrar("ELIMINAR_CASO", "Caso", caso.Id, caso.Folio);
                await _db.SaveChangesAsync();
                TempData["Exito"] = $"Caso {caso.Folio} eliminado junto con su línea de tiempo.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ------------------------------------------------------------------

        /// <summary>C-RC-02: el tutor ve el cambio en la app en segundos (la app consulta la bandeja).</summary>
        private async Task AvisarCambioAsync(Caso caso, string mensaje) =>
            await _notificador.ATutorDelAlumnoAsync(caso.AlumnoId, "CASO_ACTUALIZADO", "Actualización del caso", mensaje,
                new Dictionary<string, string> { ["ruta"] = $"tamizaje/{caso.TamizajeId}", ["casoId"] = caso.Id.ToString() });

        private async Task ValidarClinicaAsync(int? clinicaId)
        {
            if (clinicaId.HasValue && !await _db.Clinicas.AnyAsync(c => c.Id == clinicaId.Value && c.Activa))
                ModelState.AddModelError(nameof(Caso.ClinicaId), "La clínica seleccionada no existe o está desactivada.");
        }

        private async Task CargarListasAsync(int? alumnoId, int? clinicaId)
        {
            var alumnos = await _db.Alumnos.OrderBy(a => a.Apellidos).ThenBy(a => a.Nombre)
                .Select(a => new { a.Id, Texto = a.Apellidos + " " + a.Nombre + " (" + a.Matricula + ")" }).ToListAsync();
            ViewData["AlumnoId"] = new SelectList(alumnos, "Id", "Texto", alumnoId);
            ViewBag.Campanas = new SelectList(await _db.Campanas.OrderByDescending(c => c.FechaInicio)
                .Select(c => new { c.Id, Texto = c.Nombre + " (" + c.CicloEscolar + ")" }).ToListAsync(), "Id", "Texto");
            await CargarClinicasAsync(clinicaId);
        }

        private async Task CargarClinicasAsync(int? clinicaId)
        {
            var clinicas = await _db.Clinicas.Where(c => c.Activa).OrderByDescending(c => c.EsAsociada).ThenBy(c => c.Nombre)
                .Select(c => new { c.Id, Texto = c.Nombre + (c.EsAsociada ? " ✓ asociada" : "") }).ToListAsync();
            ViewData["ClinicaId"] = new SelectList(clinicas, "Id", "Texto", clinicaId);
        }

        private async Task<bool> CargarComprobantesValidadosAsync(int casoId)
        {
            var lista = await _db.Comprobantes.Where(c => c.CasoId == casoId && c.Estado == EstadoComprobante.Aprobado)
                .OrderByDescending(c => c.SubidoEn).Select(c => new { c.Id, c.Tipo, c.SubidoEn }).ToListAsync();
            ViewBag.Comprobantes = new SelectList(lista.Select(c => new { c.Id, Texto = $"{c.Tipo.Texto()} del {c.SubidoEn.ToLocalTime():dd/MM/yyyy}" }), "Id", "Texto");
            return lista.Count > 0;
        }
    }
}
