using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Expedientes de seguimiento visual.
    /// Consultar: todos. Crear/editar/cerrar: Administrador y Enfermería. Reabrir y eliminar: Administrador.
    /// </summary>
    [Authorize]
    public class CasosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CasosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Casos
        public async Task<IActionResult> Index(string? buscar, EstadoCaso? estado, Semaforo? semaforo)
        {
            var consulta = _context.Casos
                .Include(c => c.Alumno)
                .Include(c => c.Clinica)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                consulta = consulta.Where(c => c.Folio.Contains(buscar)
                    || c.Alumno!.Nombre.Contains(buscar)
                    || c.Alumno!.Apellidos.Contains(buscar));
            }
            if (estado.HasValue) consulta = consulta.Where(c => c.Estado == estado.Value);
            if (semaforo.HasValue) consulta = consulta.Where(c => c.Semaforo == semaforo.Value);

            ViewBag.Buscar = buscar;
            ViewBag.Estado = estado;
            ViewBag.Semaforo = semaforo;
            return View(await consulta.OrderByDescending(c => c.FechaDeteccion).ThenByDescending(c => c.Id).ToListAsync());
        }

        // GET: Casos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id is null) return NotFound();

            var caso = await _context.Casos
                .Include(c => c.Alumno).ThenInclude(a => a!.Tutor)
                .Include(c => c.Clinica)
                .Include(c => c.Eventos)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (caso is null) return NotFound();

            return View(caso);
        }

        // GET: Casos/Create
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Create(int? alumnoId)
        {
            await CargarListasAsync(alumnoId, null);
            return View(new Caso { AlumnoId = alumnoId ?? 0 });
        }

        // POST: Casos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Create([Bind("AlumnoId,ClinicaId,Semaforo,FechaDeteccion,AgudezaOjoDerecho,AgudezaOjoIzquierdo,Observaciones")] Caso caso)
        {
            if (caso.AlumnoId <= 0 || !await _context.Alumnos.AnyAsync(a => a.Id == caso.AlumnoId))
            {
                // Si el enlace de modelo ya marcó el campo vacío, no se repite el mensaje.
                if (ModelState.GetFieldValidationState(nameof(Caso.AlumnoId)) != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
                {
                    ModelState.AddModelError(nameof(Caso.AlumnoId), "Selecciona un alumno válido.");
                }
            }
            // Regla de negocio: un alumno solo puede tener un caso activo a la vez.
            else if (await _context.Casos.AnyAsync(c => c.AlumnoId == caso.AlumnoId && c.Estado != EstadoCaso.Cerrado))
            {
                ModelState.AddModelError(nameof(Caso.AlumnoId), "Este alumno ya tiene un caso activo. Dale seguimiento a ese caso o ciérralo primero.");
            }
            await ValidarClinicaAsync(caso.ClinicaId);

            if (!ModelState.IsValid)
            {
                await CargarListasAsync(caso.AlumnoId, caso.ClinicaId);
                return View(caso);
            }

            caso.Folio = await GenerarFolioAsync();
            caso.Estado = caso.ClinicaId.HasValue ? EstadoCaso.EnSeguimiento : EstadoCaso.Abierto;
            caso.RegistradoPor = User.Identity?.Name;

            // El tamizaje inicial queda como primer evento de la línea de tiempo.
            caso.Eventos.Add(new EventoCaso
            {
                Tipo = TipoEvento.Tamizaje,
                Fecha = caso.FechaDeteccion,
                Descripcion = $"Tamizaje Snellen: OD {caso.AgudezaOjoDerecho}, OI {caso.AgudezaOjoIzquierdo}. Semáforo {caso.Semaforo}.",
                RegistradoPor = User.Identity?.Name
            });

            _context.Add(caso);
            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Caso {caso.Folio} creado.";
            return RedirectToAction(nameof(Details), new { id = caso.Id });
        }

        // GET: Casos/Edit/5
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();
            var caso = await _context.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == id);
            if (caso is null) return NotFound();

            if (caso.Estado == EstadoCaso.Cerrado)
            {
                TempData["Error"] = "Un caso cerrado no se puede editar. Un Administrador puede reabrirlo.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await CargarListasAsync(caso.AlumnoId, caso.ClinicaId);
            return View(caso);
        }

        // POST: Casos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ClinicaId,Semaforo,Estado,FechaDeteccion,AgudezaOjoDerecho,AgudezaOjoIzquierdo,Observaciones")] Caso datos)
        {
            if (id != datos.Id) return NotFound();

            var caso = await _context.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == id);
            if (caso is null) return NotFound();

            if (caso.Estado == EstadoCaso.Cerrado)
            {
                TempData["Error"] = "Un caso cerrado no se puede editar.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (datos.Estado == EstadoCaso.Cerrado)
            {
                ModelState.AddModelError(nameof(Caso.Estado), "Para cerrar un caso usa el botón \"Cerrar caso\" (pide el resultado).");
            }
            await ValidarClinicaAsync(datos.ClinicaId);

            if (!ModelState.IsValid)
            {
                datos.Folio = caso.Folio;
                datos.AlumnoId = caso.AlumnoId;
                datos.Alumno = caso.Alumno;
                await CargarListasAsync(caso.AlumnoId, datos.ClinicaId);
                return View(datos);
            }

            // Si cambia el semáforo, queda registrado en la línea de tiempo.
            if (caso.Semaforo != datos.Semaforo)
            {
                _context.EventosCaso.Add(new EventoCaso
                {
                    CasoId = caso.Id,
                    Tipo = TipoEvento.Seguimiento,
                    Fecha = DateTime.Today,
                    Descripcion = $"Semáforo cambió de {caso.Semaforo} a {datos.Semaforo}.",
                    RegistradoPor = User.Identity?.Name
                });
            }

            caso.ClinicaId = datos.ClinicaId;
            caso.Semaforo = datos.Semaforo;
            caso.Estado = datos.Estado;
            caso.FechaDeteccion = datos.FechaDeteccion;
            caso.AgudezaOjoDerecho = datos.AgudezaOjoDerecho;
            caso.AgudezaOjoIzquierdo = datos.AgudezaOjoIzquierdo;
            caso.Observaciones = datos.Observaciones;

            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Caso {caso.Folio} actualizado.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Casos/Cerrar/5
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Cerrar(int? id)
        {
            if (id is null) return NotFound();
            var caso = await _context.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == id);
            if (caso is null) return NotFound();
            if (caso.Estado == EstadoCaso.Cerrado)
            {
                TempData["Error"] = "El caso ya está cerrado.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(new CerrarCasoViewModel
            {
                CasoId = caso.Id,
                Folio = caso.Folio,
                Alumno = caso.Alumno?.NombreCompleto ?? string.Empty
            });
        }

        // POST: Casos/Cerrar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Cerrar(CerrarCasoViewModel modelo)
        {
            var caso = await _context.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == modelo.CasoId);
            if (caso is null) return NotFound();

            if (!ModelState.IsValid)
            {
                modelo.Folio = caso.Folio;
                modelo.Alumno = caso.Alumno?.NombreCompleto ?? string.Empty;
                return View(modelo);
            }

            caso.Estado = EstadoCaso.Cerrado;
            caso.FechaCierre = DateTime.Today < caso.FechaDeteccion ? caso.FechaDeteccion : DateTime.Today;
            _context.EventosCaso.Add(new EventoCaso
            {
                CasoId = caso.Id,
                Tipo = TipoEvento.Cierre,
                Fecha = caso.FechaCierre.Value,
                Descripcion = modelo.Comentario,
                RegistradoPor = User.Identity?.Name
            });

            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Caso {caso.Folio} cerrado.";
            return RedirectToAction(nameof(Details), new { id = caso.Id });
        }

        // POST: Casos/Reabrir/5  (solo Administrador)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Reabrir(int id)
        {
            var caso = await _context.Casos.FindAsync(id);
            if (caso is null) return NotFound();
            if (caso.Estado != EstadoCaso.Cerrado) return RedirectToAction(nameof(Details), new { id });

            if (await _context.Casos.AnyAsync(c => c.AlumnoId == caso.AlumnoId && c.Id != caso.Id && c.Estado != EstadoCaso.Cerrado))
            {
                TempData["Error"] = "No se puede reabrir: el alumno ya tiene otro caso activo.";
                return RedirectToAction(nameof(Details), new { id });
            }

            caso.Estado = EstadoCaso.EnSeguimiento;
            caso.FechaCierre = null;
            _context.EventosCaso.Add(new EventoCaso
            {
                CasoId = caso.Id,
                Tipo = TipoEvento.Reapertura,
                Fecha = DateTime.Today,
                Descripcion = "El Administrador reabrió el caso.",
                RegistradoPor = User.Identity?.Name
            });
            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Caso {caso.Folio} reabierto.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Casos/Delete/5  (solo Administrador)
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();

            var caso = await _context.Casos
                .Include(c => c.Alumno)
                .Include(c => c.Clinica)
                .Include(c => c.Eventos)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (caso is null) return NotFound();

            return View(caso);
        }

        // POST: Casos/Delete/5  (solo Administrador; borra en cascada sus eventos)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var caso = await _context.Casos.FindAsync(id);
            if (caso is not null)
            {
                _context.Casos.Remove(caso);
                await _context.SaveChangesAsync();
                TempData["Exito"] = $"Caso {caso.Folio} eliminado junto con su línea de tiempo.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidarClinicaAsync(int? clinicaId)
        {
            if (clinicaId.HasValue && !await _context.Clinicas.AnyAsync(c => c.Id == clinicaId.Value))
            {
                ModelState.AddModelError(nameof(Caso.ClinicaId), "La clínica seleccionada no existe.");
            }
        }

        private async Task<string> GenerarFolioAsync()
        {
            var anio = DateTime.Today.Year;
            var prefijo = $"EYS-{anio}-";
            var ultimos = await _context.Casos
                .Where(c => c.Folio.StartsWith(prefijo))
                .Select(c => c.Folio)
                .ToListAsync();

            var siguiente = 1;
            foreach (var folio in ultimos)
            {
                if (int.TryParse(folio.Substring(prefijo.Length), out var n) && n >= siguiente)
                {
                    siguiente = n + 1;
                }
            }
            return $"{prefijo}{siguiente:D4}";
        }

        private async Task CargarListasAsync(int? alumnoId, int? clinicaId)
        {
            var alumnos = await _context.Alumnos
                .OrderBy(a => a.Apellidos).ThenBy(a => a.Nombre)
                .Select(a => new { a.Id, Texto = a.Apellidos + " " + a.Nombre + " (" + a.Matricula + ")" })
                .ToListAsync();
            ViewData["AlumnoId"] = new SelectList(alumnos, "Id", "Texto", alumnoId);

            var clinicas = await _context.Clinicas
                .OrderByDescending(c => c.EsAsociada).ThenBy(c => c.Nombre)
                .Select(c => new { c.Id, c.Nombre })
                .ToListAsync();
            ViewData["ClinicaId"] = new SelectList(clinicas, "Id", "Nombre", clinicaId);
        }
    }
}
