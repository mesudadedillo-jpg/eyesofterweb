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
        public async Task<IActionResult> Index(string? buscar, NivelEducativo? nivel)
        {
            var consulta = _context.Alumnos.Include(a => a.Tutor).AsQueryable();

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                consulta = consulta.Where(a => a.Nombre.Contains(buscar) || a.Apellidos.Contains(buscar) || a.Matricula.Contains(buscar));
            }
            if (nivel.HasValue)
            {
                consulta = consulta.Where(a => a.Nivel == nivel.Value);
            }

            ViewBag.Buscar = buscar;
            ViewBag.Nivel = nivel;
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
                .Include(a => a.Casos).ThenInclude(c => c.Clinica)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (alumno is null) return NotFound();

            return View(alumno);
        }

        // GET: Alumnos/Create
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Create(int? tutorId)
        {
            await CargarTutoresAsync(tutorId);
            var plantel = await _escuelas.PorDefectoAsync();
            if (plantel is null) return View("~/Views/Home/SinEscuela.cshtml");
            await CargarEscuelasAsync(plantel);
            return View(new Alumno { TutorId = tutorId ?? 0, PlantelId = plantel.Value });
        }

        // POST: Alumnos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Create([Bind("Matricula,Nombre,Apellidos,FechaNacimiento,Nivel,Grado,Grupo,UsaLentes,TutorId,PlantelId")] Alumno alumno)
        {
            await ValidarReglasAsync(alumno);
            if (!ModelState.IsValid)
            {
                await CargarTutoresAsync(alumno.TutorId);
                await CargarEscuelasAsync(alumno.PlantelId);
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
            return View(alumno);
        }

        // POST: Alumnos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Matricula,Nombre,Apellidos,FechaNacimiento,Nivel,Grado,Grupo,UsaLentes,TutorId,PlantelId")] Alumno alumno)
        {
            if (id != alumno.Id) return NotFound();
            // El alumno debe existir en las escuelas visibles: no se edita "a ciegas" un id de otra escuela.
            if (!await _context.Alumnos.AnyAsync(a => a.Id == id)) return NotFound();

            await ValidarReglasAsync(alumno);
            if (!ModelState.IsValid)
            {
                await CargarTutoresAsync(alumno.TutorId);
                await CargarEscuelasAsync(alumno.PlantelId);
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
