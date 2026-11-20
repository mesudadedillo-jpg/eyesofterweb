using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Catálogo de clínicas. Consultar: todos los roles. Crear/editar/eliminar: solo Administrador.
    /// </summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class ClinicasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClinicasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Clinicas
        public async Task<IActionResult> Index()
        {
            var clinicas = await _context.Clinicas
                .Include(c => c.Casos)
                .OrderByDescending(c => c.EsAsociada).ThenBy(c => c.Nombre)
                .ToListAsync();
            return View(clinicas);
        }

        // GET: Clinicas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id is null) return NotFound();

            var clinica = await _context.Clinicas
                .Include(c => c.Casos).ThenInclude(cs => cs.Alumno)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (clinica is null) return NotFound();

            return View(clinica);
        }

        // GET: Clinicas/Create
        public IActionResult Create()
        {
            return View(new Clinica());
        }

        // POST: Clinicas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Tipo,Direccion,Ciudad,CodigoPostal,Latitud,Longitud,Horario,Telefono,Correo,EsAsociada,Activa")] Clinica clinica)
        {
            if (!ModelState.IsValid) return View(clinica);

            _context.Add(clinica);
            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Clínica {clinica.Nombre} registrada.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Clinicas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();
            var clinica = await _context.Clinicas.FindAsync(id);
            if (clinica is null) return NotFound();
            return View(clinica);
        }

        // POST: Clinicas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Tipo,Direccion,Ciudad,CodigoPostal,Latitud,Longitud,Horario,Telefono,Correo,EsAsociada,Activa")] Clinica clinica)
        {
            if (id != clinica.Id) return NotFound();
            if (!ModelState.IsValid) return View(clinica);

            try
            {
                _context.Update(clinica);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Clinicas.AnyAsync(e => e.Id == clinica.Id)) return NotFound();
                throw;
            }
            TempData["Exito"] = "Clínica actualizada.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>W-RFN-12: desactivar en lugar de borrar (conserva el historial). Una clínica inactiva no recibe pases.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarActiva(int id)
        {
            var clinica = await _context.Clinicas.FindAsync(id);
            if (clinica is null) return NotFound();
            clinica.Activa = !clinica.Activa;
            await _context.SaveChangesAsync();
            TempData["Exito"] = $"{clinica.Nombre} quedó {(clinica.Activa ? "activa" : "desactivada: ya no aparece en el directorio ni puede leer pases")}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Clinicas/Delete/5
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id is null) return NotFound();

            var clinica = await _context.Clinicas
                .Include(c => c.Casos)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (clinica is null) return NotFound();

            return View(clinica);
        }

        // POST: Clinicas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var clinica = await _context.Clinicas.Include(c => c.Casos).FirstOrDefaultAsync(c => c.Id == id);
            if (clinica is null) return RedirectToAction(nameof(Index));

            if (clinica.Casos.Any())
            {
                TempData["Error"] = $"No se puede eliminar {clinica.Nombre}: tiene {clinica.Casos.Count} caso(s) canalizados.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Clinicas.Remove(clinica);
            await _context.SaveChangesAsync();
            TempData["Exito"] = "Clínica eliminada.";
            return RedirectToAction(nameof(Index));
        }
    }
}
