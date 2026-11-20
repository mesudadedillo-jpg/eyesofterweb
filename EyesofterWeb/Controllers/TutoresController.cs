using EyesofterWeb.Data;
using EyesofterWeb.Models;
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

        public TutoresController(ApplicationDbContext context)
        {
            _context = context;
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
                .FirstOrDefaultAsync(t => t.Id == id);
            if (tutor is null) return NotFound();

            return View(tutor);
        }

        // GET: Tutores/Create
        public IActionResult Create()
        {
            return View(new Tutor());
        }

        // POST: Tutores/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Apellidos,Telefono,Correo,Parentesco")] Tutor tutor)
        {
            if (!ModelState.IsValid) return View(tutor);

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

            try
            {
                _context.Update(tutor);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Tutores.AnyAsync(e => e.Id == tutor.Id)) return NotFound();
                throw;
            }
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
