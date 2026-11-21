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
    /// Convenios de cada escuela con clínicas: el beneficio (descuento) para sus alumnos. Se
    /// muestra al tutor en el directorio y en el pase, y al especialista al validar el QR.
    /// No se comparte ningún dato del alumno ni del tutor con la clínica por tener convenio:
    /// la clínica solo ve el caso cuando la familia la elige y llega con su pase.
    /// Consultar: Dirección y Enfermería. Crear y editar: Dirección. Cada quien, de SUS escuelas.
    /// </summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class ConveniosController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly Escuelas _escuelas;
        private readonly Auditoria _auditoria;

        public ConveniosController(ApplicationDbContext db, Escuelas escuelas, Auditoria auditoria)
        {
            _db = db;
            _escuelas = escuelas;
            _auditoria = auditoria;
        }

        public async Task<IActionResult> Index() =>
            View(await _db.Convenios.AsNoTracking().Include(c => c.Plantel).Include(c => c.Clinica)
                .OrderBy(c => c.Plantel!.Nombre).ThenBy(c => c.Clinica!.Nombre).ToListAsync());

        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Create(int? clinicaId)
        {
            var plantel = await _escuelas.PorDefectoAsync();
            if (plantel is null) return View("~/Views/Home/SinEscuela.cshtml");
            await CargarListasAsync(plantel, clinicaId);
            return View("Form", new Convenio { PlantelId = plantel.Value, ClinicaId = clinicaId ?? 0, Activo = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Create([Bind("PlantelId,ClinicaId,Descuento,Beneficio,VigenteHasta,Activo")] Convenio c)
        {
            await ValidarAsync(c);
            if (!ModelState.IsValid) { await CargarListasAsync(c.PlantelId, c.ClinicaId); return View("Form", c); }
            c.Beneficio = c.Beneficio.Trim();
            c.VigenteHasta = c.VigenteHasta?.Date;
            _db.Convenios.Add(c);
            await _db.SaveChangesAsync();
            _auditoria.Registrar("CREAR_CONVENIO", "Convenio", c.Id, $"Clínica {c.ClinicaId}: {c.Beneficio}", plantelId: c.PlantelId);
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Convenio registrado. Las familias lo verán en el directorio de clínicas y en el pase.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.Convenios.Include(x => x.Plantel).Include(x => x.Clinica).FirstOrDefaultAsync(x => x.Id == id);
            return c is null ? NotFound() : View("Form", c);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Descuento,Beneficio,VigenteHasta,Activo")] Convenio datos)
        {
            // Filtrado por escuela: no se edita un convenio de otra escuela. Escuela y clínica no cambian.
            var c = await _db.Convenios.Include(x => x.Plantel).Include(x => x.Clinica).FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return NotFound();
            datos.PlantelId = c.PlantelId;
            datos.ClinicaId = c.ClinicaId;
            datos.Plantel = c.Plantel;
            datos.Clinica = c.Clinica;
            if (!ModelState.IsValid) return View("Form", datos);
            c.Descuento = datos.Descuento;
            c.Beneficio = datos.Beneficio.Trim();
            c.VigenteHasta = datos.VigenteHasta?.Date;
            c.Activo = datos.Activo;
            _auditoria.Registrar("EDITAR_CONVENIO", "Convenio", c.Id, $"{c.Clinica?.Nombre}: {c.Beneficio} ({(c.Activo ? "activo" : "inactivo")})", plantelId: c.PlantelId);
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Convenio actualizado.";
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidarAsync(Convenio c)
        {
            if (!await _escuelas.PuedeUsarAsync(c.PlantelId)) ModelState.AddModelError(nameof(Convenio.PlantelId), "Elige una de tus escuelas.");
            if (!await _db.Clinicas.AnyAsync(x => x.Id == c.ClinicaId && x.Activa)) ModelState.AddModelError(nameof(Convenio.ClinicaId), "Elige una clínica activa del directorio.");
            // Sin filtro: el índice único es (escuela, clínica) y la escuela ya se validó arriba.
            else if (await _db.Convenios.IgnoreQueryFilters().AnyAsync(x => x.PlantelId == c.PlantelId && x.ClinicaId == c.ClinicaId))
                ModelState.AddModelError(nameof(Convenio.ClinicaId), "Esta escuela ya tiene un convenio con esa clínica. Edítalo en la lista.");
        }

        private async Task CargarListasAsync(int? plantel, int? clinica)
        {
            ViewData["PlantelId"] = await _escuelas.ListaAsync(plantel);
            ViewData["ClinicaId"] = new SelectList(await _db.Clinicas.Where(x => x.Activa).OrderBy(x => x.Nombre)
                .Select(x => new { x.Id, Texto = x.Nombre + " · " + x.Ciudad }).ToListAsync(), "Id", "Texto", clinica);
        }
    }
}
