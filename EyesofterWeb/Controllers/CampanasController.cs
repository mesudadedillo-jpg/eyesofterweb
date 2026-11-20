using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>Campañas de tamizaje por ciclo escolar (W-RFN-16). Los tamizajes del escritorio caen en la campaña vigente.</summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class CampanasController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CampanasController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index() =>
            View(await _db.Campanas.AsNoTracking().Include(c => c.Tamizajes).OrderByDescending(c => c.FechaInicio).ToListAsync());

        public IActionResult Create() => View("Form", new Campana { CicloEscolar = $"{DateTime.Today.Year}-{DateTime.Today.Year + 1}" });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,CicloEscolar,FechaInicio,FechaFin")] Campana c)
        {
            Validar(c);
            if (!ModelState.IsValid) return View("Form", c);
            c.FechaInicio = c.FechaInicio.Date;
            c.FechaFin = c.FechaFin?.Date;
            _db.Campanas.Add(c);
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Campaña creada.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.Campanas.FindAsync(id);
            return c is null ? NotFound() : View("Form", c);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,CicloEscolar,FechaInicio,FechaFin")] Campana datos)
        {
            var c = await _db.Campanas.FindAsync(id);
            if (c is null) return NotFound();
            Validar(datos);
            if (!ModelState.IsValid) return View("Form", datos);
            c.Nombre = datos.Nombre;
            c.CicloEscolar = datos.CicloEscolar;
            c.FechaInicio = datos.FechaInicio.Date;
            c.FechaFin = datos.FechaFin?.Date;
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Campaña actualizada.";
            return RedirectToAction(nameof(Index));
        }

        private void Validar(Campana c)
        {
            if (c.FechaFin.HasValue && c.FechaFin < c.FechaInicio) ModelState.AddModelError(nameof(c.FechaFin), "La fecha de fin no puede ser anterior al inicio.");
        }
    }
}
