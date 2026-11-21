using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Alta de escuelas (C-RE-01). Solo el administrador global: así sumar una escuela nueva
    /// es crear el registro aquí y asignarle su Dirección en Usuarios, sin tocar código.
    /// Una escuela no se borra (conserva el historial); se desactiva.
    /// </summary>
    [Authorize(Policy = Roles.PoliticaGlobal)]
    public class PlantelesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly Auditoria _auditoria;

        public PlantelesController(ApplicationDbContext db, Auditoria auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        public record FilaPlantel(Plantel Plantel, int Alumnos, int Personal);

        public async Task<IActionResult> Index()
        {
            var filas = await _db.Planteles.AsNoTracking().OrderBy(p => p.Nombre)
                .Select(p => new FilaPlantel(p,
                    _db.Alumnos.Count(a => a.PlantelId == p.Id),
                    _db.UsuarioPlanteles.Count(u => u.PlantelId == p.Id)))
                .ToListAsync();
            return View(filas);
        }

        public IActionResult Create() => View("Form", new Plantel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Clave,Activo")] Plantel p)
        {
            await ValidarAsync(p);
            if (!ModelState.IsValid) return View("Form", p);
            p.Nombre = p.Nombre.Trim();
            _db.Planteles.Add(p);
            await _db.SaveChangesAsync();
            _auditoria.Registrar("CREAR_PLANTEL", "Plantel", p.Id, p.Nombre, plantelId: p.Id);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Escuela «{p.Nombre}» creada. Ahora asígnale su Dirección en Usuarios.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var p = await _db.Planteles.FindAsync(id);
            return p is null ? NotFound() : View("Form", p);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Clave,Activo")] Plantel datos)
        {
            var p = await _db.Planteles.FindAsync(id);
            if (p is null) return NotFound();
            await ValidarAsync(datos);
            if (!ModelState.IsValid) return View("Form", datos);
            p.Nombre = datos.Nombre.Trim();
            p.Clave = datos.Clave;
            p.Activo = datos.Activo;
            _auditoria.Registrar("EDITAR_PLANTEL", "Plantel", p.Id, $"{p.Nombre} ({(p.Activo ? "activa" : "inactiva")})", plantelId: p.Id);
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Escuela actualizada.";
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidarAsync(Plantel p)
        {
            p.Clave = string.IsNullOrWhiteSpace(p.Clave) ? null : p.Clave.Trim().ToUpperInvariant();
            if (p.Clave is not null && await _db.Planteles.AnyAsync(x => x.Clave == p.Clave && x.Id != p.Id))
                ModelState.AddModelError(nameof(Plantel.Clave), "Ya hay una escuela con esa clave.");
        }
    }
}
