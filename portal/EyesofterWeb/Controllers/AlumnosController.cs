using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// CRUD de alumnos.
    /// Consultar: todos los roles. Crear/editar: Administrador y Enfermería. Eliminar: Administrador.
    /// </summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class AlumnosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Escuelas _escuelas;

        public AlumnosController(ApplicationDbContext context, Escuelas escuelas)
        {
            _context = context;
            _escuelas = escuelas;
        }

        // GET: Alumnos
        public async Task<IActionResult> Index(string? buscar, NivelEducativo? nivel, int? grupoId, bool egresados = false)
        {
            var consulta = _context.Alumnos.Include(a => a.Tutor).Where(a => a.Egresado == egresados);

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                consulta = consulta.Where(a => a.Nombre.Contains(buscar) || a.Apellidos.Contains(buscar) || a.Matricula.Contains(buscar));
            }
            if (nivel.HasValue)
            {
                consulta = consulta.Where(a => a.Nivel == nivel.Value);
            }
            if (grupoId == 0)
            {
                consulta = consulta.Where(a => a.GrupoEscolarId == null);
            }
            else if (grupoId.HasValue)
            {
                consulta = consulta.Where(a => a.GrupoEscolarId == grupoId);
            }

            ViewBag.Buscar = buscar;
            ViewBag.Nivel = nivel;
            ViewBag.GrupoId = grupoId;
            ViewBag.Egresados = egresados;
            ViewBag.Grupos = await _context.GruposEscolares.AsNoTracking().Where(g => g.Activo)
                .OrderBy(g => g.Nivel).ThenBy(g => g.Grado).ThenBy(g => g.Letra).ToListAsync();
            return View(await consulta
                .OrderBy(a => a.Nivel).ThenBy(a => a.Grado).ThenBy(a => a.Grupo).ThenBy(a => a.Apellidos)
                .ToListAsync());
        }

        // GET: Alumnos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id is null) return NotFound();

            var alumno = await _context.Alumnos
                .Include(a => a.Tutor)
                .Include(a => a.GrupoEscolar)
                .Include(a => a.Casos).ThenInclude(c => c.Clinica)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (alumno is null) return NotFound();

            return View(alumno);
        }

        // GET: Alumnos/Create
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Create(int? tutorId, int? grupoId)
        {
            await CargarTutoresAsync(tutorId);
            var plantel = await _escuelas.PorDefectoAsync();
            if (plantel is null) return View("~/Views/Home/SinEscuela.cshtml");

            var alumno = new Alumno { TutorId = tutorId ?? 0, PlantelId = plantel.Value };
            // "Alumno nuevo en este grupo": se llena escuela, nivel, grado y letra desde el grupo.
            if (grupoId is int gid && await _context.GruposEscolares.FirstOrDefaultAsync(g => g.Id == gid && g.Activo) is { } grupo)
            {
                GruposEscolares.Sincronizar(alumno, grupo);
                alumno.GrupoEscolar = null;
            }
            await CargarEscuelasAsync(alumno.PlantelId);
            await CargarGruposAsync(alumno.GrupoEscolarId);
            return View(alumno);
        }

        // POST: Alumnos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Create([Bind("Matricula,Nombre,Apellidos,FechaNacimiento,Nivel,Grado,Grupo,UsaLentes,TutorId,PlantelId,GrupoEscolarId")] Alumno alumno)
        {
            await ValidarReglasAsync(alumno);
            if (!ModelState.IsValid)
            {
                await CargarTutoresAsync(alumno.TutorId);
                await CargarEscuelasAsync(alumno.PlantelId);
                await CargarGruposAsync(alumno.GrupoEscolarId);
                return View(alumno);
            }

            _context.Add(alumno);
            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Alumno {alumno.NombreCompleto} registrado.";
            return RedirectToAction(nameof(Details), new { id = alumno.Id });
        }

        // GET: Alumnos/Edit/5
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();
            var alumno = await _context.Alumnos.FindAsync(id);
            if (alumno is null) return NotFound();

            await CargarTutoresAsync(alumno.TutorId);
            await CargarEscuelasAsync(alumno.PlantelId);
            await CargarGruposAsync(alumno.GrupoEscolarId);
            return View(alumno);
        }

        // POST: Alumnos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Matricula,Nombre,Apellidos,FechaNacimiento,Nivel,Grado,Grupo,UsaLentes,TutorId,PlantelId,GrupoEscolarId,Egresado")] Alumno alumno)
        {
            if (id != alumno.Id) return NotFound();
            // El alumno debe existir en las escuelas visibles: no se edita "a ciegas" un id de otra escuela.
            if (!await _context.Alumnos.AnyAsync(a => a.Id == id)) return NotFound();

            await ValidarReglasAsync(alumno);
            if (!ModelState.IsValid)
            {
                await CargarTutoresAsync(alumno.TutorId);
                await CargarEscuelasAsync(alumno.PlantelId);
                await CargarGruposAsync(alumno.GrupoEscolarId);
                return View(alumno);
            }

            try
            {
                _context.Update(alumno);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Alumnos.AnyAsync(e => e.Id == alumno.Id)) return NotFound();
                throw;
            }
            TempData["Exito"] = "Alumno actualizado.";
            return RedirectToAction(nameof(Details), new { id = alumno.Id });
        }

        // GET: Alumnos/Delete/5
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();

            var alumno = await _context.Alumnos
                .Include(a => a.Tutor)
                .Include(a => a.Casos)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (alumno is null) return NotFound();

            return View(alumno);
        }

        // POST: Alumnos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var alumno = await _context.Alumnos.Include(a => a.Casos).FirstOrDefaultAsync(a => a.Id == id);
            if (alumno is null) return RedirectToAction(nameof(Index));

            // Regla de negocio: el historial clínico no se pierde.
            if (alumno.Casos.Any())
            {
                TempData["Error"] = $"No se puede eliminar a {alumno.NombreCompleto}: tiene {alumno.Casos.Count} caso(s) registrados.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Alumnos.Remove(alumno);
            await _context.SaveChangesAsync();
            TempData["Exito"] = "Alumno eliminado.";
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidarReglasAsync(Alumno alumno)
        {
            if (!await _escuelas.PuedeUsarAsync(alumno.PlantelId))
            {
                ModelState.AddModelError(nameof(Alumno.PlantelId), "Elige una de tus escuelas.");
            }
            // La matrícula es única dentro de la escuela. Se busca sin filtro para no chocar con el índice único.
            else if (!string.IsNullOrWhiteSpace(alumno.Matricula) &&
                await _context.Alumnos.IgnoreQueryFilters().AnyAsync(a => a.Matricula == alumno.Matricula && a.PlantelId == alumno.PlantelId && a.Id != alumno.Id))
            {
                ModelState.AddModelError(nameof(Alumno.Matricula), "Ya existe un alumno con esa matrícula en esta escuela.");
            }

            if (alumno.TutorId <= 0)
            {
                // Si el enlace de modelo ya marcó el campo vacío, no se repite el mensaje.
                if (ModelState.GetFieldValidationState(nameof(Alumno.TutorId)) != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
                {
                    ModelState.AddModelError(nameof(Alumno.TutorId), "Selecciona un tutor.");
                }
            }
            else if (!await _context.Tutores.AnyAsync(t => t.Id == alumno.TutorId))
            {
                ModelState.AddModelError(nameof(Alumno.TutorId), "El tutor seleccionado no existe.");
            }

            // Con grupo escolar, nivel / grado / letra los pone el grupo (así cambian solos al pasar de año).
            if (alumno.GrupoEscolarId is int grupoId)
            {
                var grupo = await _context.GruposEscolares.AsNoTracking().FirstOrDefaultAsync(g => g.Id == grupoId);
                if (grupo is null || !grupo.Activo || grupo.PlantelId != alumno.PlantelId)
                {
                    ModelState.AddModelError(nameof(Alumno.GrupoEscolarId), "Elige un grupo activo de la misma escuela.");
                }
                else if (!alumno.Egresado)
                {
                    GruposEscolares.Sincronizar(alumno, grupo);
                    alumno.GrupoEscolar = null; // no se vuelve a insertar el grupo
                    foreach (var campo in new[] { nameof(Alumno.Nivel), nameof(Alumno.Grado), nameof(Alumno.Grupo) })
                        ModelState.Remove(campo);
                }
            }
        }

        private async Task CargarGruposAsync(int? seleccionado)
        {
            var grupos = await _context.GruposEscolares.AsNoTracking().Where(g => g.Activo)
                .OrderBy(g => g.Nivel).ThenBy(g => g.Grado).ThenBy(g => g.Letra)
                .ToListAsync();
            ViewData["GrupoEscolarId"] = new SelectList(
                grupos.Select(g => new { g.Id, Texto = $"{g.Nombre} · {g.CicloEscolar}", g.PlantelId }), "Id", "Texto", seleccionado);
        }

        private async Task CargarEscuelasAsync(int? seleccionado) => ViewData["PlantelId"] = await _escuelas.ListaAsync(seleccionado);

        private async Task CargarTutoresAsync(int? seleccionado)
        {
            var tutores = await _context.Tutores
                .OrderBy(t => t.Apellidos).ThenBy(t => t.Nombre)
                .Select(t => new { t.Id, Texto = t.Nombre + " " + t.Apellidos + " (" + t.Parentesco + ")" })
                .ToListAsync();
            ViewData["TutorId"] = new SelectList(tutores, "Id", "Texto", seleccionado);
        }
    }
}
