using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// CRUD de tutores. Son datos personales de contacto, por eso el Docente
    /// NO tiene acceso a ninguna acción de este controlador.
    /// </summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class TutoresController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Escuelas _escuelas;

        public TutoresController(ApplicationDbContext context, Escuelas escuelas)
        {
            _context = context;
            _escuelas = escuelas;
        }

        // GET: Tutores
        public async Task<IActionResult> Index(string? buscar)
        {
            var consulta = _context.Tutores.Include(t => t.Alumnos).AsQueryable();
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                consulta = consulta.Where(t => t.Nombre.Contains(buscar) || t.Apellidos.Contains(buscar) || t.Telefono.Contains(buscar));
            }
            ViewBag.Buscar = buscar;
            return View(await consulta.OrderBy(t => t.Apellidos).ThenBy(t => t.Nombre).ToListAsync());
        }

        // GET: Tutores/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id is null) return NotFound();

            var tutor = await _context.Tutores
                .Include(t => t.Alumnos)
                .Include(t => t.Usuarios)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (tutor is null) return NotFound();

            return View(tutor);
        }

        // GET: Tutores/Create
        public async Task<IActionResult> Create()
        {
            var plantel = await _escuelas.PorDefectoAsync();
            if (plantel is null) return View("~/Views/Home/SinEscuela.cshtml");
            ViewData["PlantelId"] = await _escuelas.ListaAsync(plantel);
            return View(new Tutor { PlantelId = plantel.Value });
        }

        // POST: Tutores/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Apellidos,Telefono,Correo,Parentesco,PlantelId")] Tutor tutor)
        {
            if (!await _escuelas.PuedeUsarAsync(tutor.PlantelId)) ModelState.AddModelError(nameof(Tutor.PlantelId), "Elige una de tus escuelas.");
            if (!ModelState.IsValid) { ViewData["PlantelId"] = await _escuelas.ListaAsync(tutor.PlantelId); return View(tutor); }

            _context.Add(tutor);
            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Tutor {tutor.NombreCompleto} registrado.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tutores/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();
            var tutor = await _context.Tutores.FindAsync(id);
            if (tutor is null) return NotFound();
            return View(tutor);
        }

        // POST: Tutores/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Apellidos,Telefono,Correo,Parentesco")] Tutor tutor)
        {
            if (id != tutor.Id) return NotFound();
            if (!ModelState.IsValid) return View(tutor);

            // Se carga con el filtro por escuela: no se edita "a ciegas" un tutor de otra escuela.
            // La escuela que lo registró no cambia al editar.
            var actual = await _context.Tutores.FirstOrDefaultAsync(t => t.Id == id);
            if (actual is null) return NotFound();
            actual.Nombre = tutor.Nombre;
            actual.Apellidos = tutor.Apellidos;
            actual.Telefono = tutor.Telefono;
            actual.Correo = tutor.Correo;
            actual.Parentesco = tutor.Parentesco;
            await _context.SaveChangesAsync();
            TempData["Exito"] = "Tutor actualizado.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tutores/Delete/5  (solo Administrador)
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();

            var tutor = await _context.Tutores
                .Include(t => t.Alumnos)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (tutor is null) return NotFound();

            return View(tutor);
        }

        // POST: Tutores/Delete/5  (solo Administrador)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tutor = await _context.Tutores.Include(t => t.Alumnos).FirstOrDefaultAsync(t => t.Id == id);
            if (tutor is null) return RedirectToAction(nameof(Index));

            // Regla de negocio: no se elimina un tutor con alumnos a su cargo.
            if (tutor.Alumnos.Any())
            {
                TempData["Error"] = $"No se puede eliminar a {tutor.NombreCompleto}: tiene {tutor.Alumnos.Count} alumno(s) a su cargo. Reasígnalos primero.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Tutores.Remove(tutor);
            await _context.SaveChangesAsync();
            TempData["Exito"] = "Tutor eliminado.";
            return RedirectToAction(nameof(Index));
        }
    }
}
