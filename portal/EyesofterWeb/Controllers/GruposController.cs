using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Grupos escolares: registrar un grupo, inscribir a sus alumnos y pasarlo de año
    /// (un grupo o toda la escuela) sin editar a cada alumno.
    /// Consultar, crear e inscribir: Dirección y Enfermería. Pasar de año y eliminar: Dirección.
    /// </summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class GruposController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly Escuelas _escuelas;
        private readonly GruposEscolares _grupos;
        private readonly Auditoria _auditoria;

        public GruposController(ApplicationDbContext db, Escuelas escuelas, GruposEscolares grupos, Auditoria auditoria)
        {
            _db = db;
            _escuelas = escuelas;
            _grupos = grupos;
            _auditoria = auditoria;
        }

        public record FilaGrupo(GrupoEscolar Grupo, int Alumnos);

        // GET: Grupos
        public async Task<IActionResult> Index(int? plantelId, bool historial = false)
        {
            var escuelas = await _escuelas.VisiblesAsync();
            if (escuelas.Count == 0) return View("~/Views/Home/SinEscuela.cshtml");
            var plantel = plantelId is int p && escuelas.Any(e => e.Id == p) ? p : escuelas[0].Id;

            var filas = await _db.GruposEscolares.AsNoTracking()
                .Where(g => g.PlantelId == plantel && g.Activo != historial)
                .OrderBy(g => g.Nivel).ThenBy(g => g.Grado).ThenBy(g => g.Letra).ThenByDescending(g => g.CicloEscolar)
                .Select(g => new FilaGrupo(g, g.Alumnos.Count(a => historial || !a.Egresado)))
                .ToListAsync();

            ViewBag.Escuelas = escuelas;
            ViewBag.PlantelId = plantel;
            ViewBag.Historial = historial;
            ViewBag.SinGrupo = await _db.Alumnos.CountAsync(a => a.PlantelId == plantel && a.GrupoEscolarId == null && !a.Egresado);
            return View(filas);
        }

        // GET: Grupos/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var grupo = await _db.GruposEscolares.AsNoTracking()
                .Include(g => g.Plantel)
                .Include(g => g.Alumnos.OrderBy(a => a.Apellidos).ThenBy(a => a.Nombre)).ThenInclude(a => a.Tutor)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (grupo is null) return NotFound();

            // Candidatos para inscribir: alumnos de la misma escuela que no están en este grupo.
            ViewBag.Candidatos = grupo.Activo
                ? await _db.Alumnos.AsNoTracking()
                    .Where(a => a.PlantelId == grupo.PlantelId && !a.Egresado && a.GrupoEscolarId != grupo.Id)
                    .OrderBy(a => a.GrupoEscolarId != null).ThenBy(a => a.Nivel).ThenBy(a => a.Grado).ThenBy(a => a.Grupo).ThenBy(a => a.Apellidos)
                    .ToListAsync()
                : new List<Alumno>();
            return View(grupo);
        }

        // GET: Grupos/Create
        public async Task<IActionResult> Create()
        {
            var plantel = await _escuelas.PorDefectoAsync();
            if (plantel is null) return View("~/Views/Home/SinEscuela.cshtml");
            ViewData["PlantelId"] = await _escuelas.ListaAsync(plantel);
            return View("Form", new GrupoEscolar { PlantelId = plantel.Value });
        }

        // POST: Grupos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PlantelId,Nivel,Grado,Letra,CicloEscolar,Docente")] GrupoEscolar grupo)
        {
            grupo.Letra = (grupo.Letra ?? "").Trim().ToUpperInvariant();
            if (!await _escuelas.PuedeUsarAsync(grupo.PlantelId))
                ModelState.AddModelError(nameof(GrupoEscolar.PlantelId), "Elige una de tus escuelas.");
            else if (await _grupos.BuscarDuplicadoAsync(grupo.PlantelId, grupo.Nivel, grupo.Grado, grupo.Letra) is not null)
                ModelState.AddModelError(nameof(GrupoEscolar.Letra), $"Ya existe el grupo {grupo.Nombre} en esta escuela.");

            if (!ModelState.IsValid)
            {
                ViewData["PlantelId"] = await _escuelas.ListaAsync(grupo.PlantelId);
                return View("Form", grupo);
            }

            grupo.Activo = true;
            _db.GruposEscolares.Add(grupo);
            await _db.SaveChangesAsync();
            _auditoria.Registrar("CREAR_GRUPO", "Grupo", grupo.Id, grupo.Nombre, plantelId: grupo.PlantelId);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Grupo {grupo.Nombre} registrado. Ahora inscribe a sus alumnos.";
            return RedirectToAction(nameof(Details), new { id = grupo.Id });
        }

        // GET: Grupos/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var grupo = await _db.GruposEscolares.FindAsync(id);
            if (grupo is null) return NotFound();
            return View("Form", grupo);
        }

        // POST: Grupos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nivel,Grado,Letra,CicloEscolar,Docente")] GrupoEscolar datos)
        {
            var grupo = await _db.GruposEscolares.FirstOrDefaultAsync(g => g.Id == id);
            if (grupo is null) return NotFound();
            datos.PlantelId = grupo.PlantelId;
            datos.Letra = (datos.Letra ?? "").Trim().ToUpperInvariant();

            if (grupo.Activo && await _grupos.BuscarDuplicadoAsync(grupo.PlantelId, datos.Nivel, datos.Grado, datos.Letra, grupo.Id) is not null)
                ModelState.AddModelError(nameof(GrupoEscolar.Letra), $"Ya existe el grupo {datos.Nombre} en esta escuela.");
            if (!ModelState.IsValid)
            {
                datos.Activo = grupo.Activo;
                return View("Form", datos);
            }

            var antes = grupo.Nombre;
            grupo.Nivel = datos.Nivel;
            grupo.Grado = datos.Grado;
            grupo.Letra = datos.Letra;
            grupo.CicloEscolar = datos.CicloEscolar;
            grupo.Docente = datos.Docente;
            if (grupo.Activo) await _grupos.SincronizarAlumnosAsync(grupo);
            _auditoria.Registrar("EDITAR_GRUPO", "Grupo", grupo.Id, antes == grupo.Nombre ? grupo.Nombre : $"{antes} → {grupo.Nombre}", plantelId: grupo.PlantelId);
            await _db.SaveChangesAsync();
            TempData["Exito"] = "Grupo actualizado; sus alumnos se actualizaron con él.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Grupos/Inscribir/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Inscribir(int id, int[] alumnoIds)
        {
            var grupo = await _db.GruposEscolares.FirstOrDefaultAsync(g => g.Id == id);
            if (grupo is null) return NotFound();
            if (!grupo.Activo)
            {
                TempData["Error"] = "Este grupo ya egresó; no se pueden inscribir alumnos.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (alumnoIds.Length == 0)
            {
                TempData["Error"] = "Marca al menos un alumno.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var n = await _grupos.InscribirAsync(grupo, alumnoIds);
            _auditoria.Registrar("INSCRIBIR_GRUPO", "Grupo", grupo.Id, $"{n} alumno(s) a {grupo.Nombre}", plantelId: grupo.PlantelId);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"{n} alumno(s) inscrito(s) en {grupo.Nombre}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Grupos/Quitar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Quitar(int id, int alumnoId)
        {
            var alumno = await _db.Alumnos.FirstOrDefaultAsync(a => a.Id == alumnoId && a.GrupoEscolarId == id);
            if (alumno is null) return NotFound();
            alumno.GrupoEscolarId = null;
            _auditoria.Registrar("QUITAR_DE_GRUPO", "Alumno", alumno.Id, $"Grupo {id}");
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"{alumno.NombreCompleto} ya no está en el grupo (conserva su grado y letra).";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Grupos/Promover/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Promover(int id)
        {
            var grupo = await _db.GruposEscolares.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
            if (grupo is null) return NotFound();

            var r = await _grupos.PromoverAsync(id);
            if (!r.Exito)
            {
                TempData["Error"] = string.Join(" ", r.Avisos);
                return RedirectToAction(nameof(Details), new { id });
            }

            var detalle = r.GruposEgresados > 0 ? $"{grupo.Nombre} egresó ({r.AlumnosEgresados} alumnos)" : $"{grupo.Nombre} pasó de año ({r.AlumnosMovidos} alumnos)";
            _auditoria.Registrar("PASAR_DE_ANIO_GRUPO", "Grupo", id, detalle, plantelId: grupo.PlantelId);
            await _db.SaveChangesAsync();
            TempData["Exito"] = r.GruposEgresados > 0
                ? $"El grupo {grupo.Nombre} egresó: {r.AlumnosEgresados} alumno(s) quedaron como egresados. Su historial se conserva."
                : $"Listo: el grupo pasó de año con sus {r.AlumnosMovidos} alumno(s).";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Grupos/PasarDeAnio — vista previa de lo que pasará en toda la escuela
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> PasarDeAnio(int? plantelId, NivelEducativo? nivel)
        {
            var escuelas = await _escuelas.VisiblesAsync();
            if (escuelas.Count == 0) return View("~/Views/Home/SinEscuela.cshtml");
            var plantel = plantelId is int p && escuelas.Any(e => e.Id == p) ? p : escuelas[0].Id;

            var filas = await _db.GruposEscolares.AsNoTracking()
                .Where(g => g.PlantelId == plantel && g.Activo && (nivel == null || g.Nivel == nivel))
                .OrderBy(g => g.Nivel).ThenByDescending(g => g.Grado).ThenBy(g => g.Letra)
                .Select(g => new FilaGrupo(g, g.Alumnos.Count(a => !a.Egresado)))
                .ToListAsync();

            ViewBag.Escuelas = escuelas;
            ViewBag.PlantelId = plantel;
            ViewBag.Nivel = nivel;
            return View(filas);
        }

        // POST: Grupos/PasarDeAnio
        [HttpPost, ActionName("PasarDeAnio")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> PasarDeAnioConfirmado(int plantelId, NivelEducativo? nivel, string? confirmacion)
        {
            if (!await _escuelas.PuedeUsarAsync(plantelId)) return NotFound();
            if (!string.Equals(confirmacion?.Trim(), "PASAR DE AÑO", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Escribe PASAR DE AÑO para confirmar.";
                return RedirectToAction(nameof(PasarDeAnio), new { plantelId, nivel });
            }

            var r = await _grupos.PromoverEscuelaAsync(plantelId, nivel);
            if (!r.Exito)
            {
                TempData["Error"] = "No se cambió ningún grupo. " + string.Join(" ", r.Avisos);
                return RedirectToAction(nameof(PasarDeAnio), new { plantelId, nivel });
            }

            _auditoria.Registrar("PASAR_DE_ANIO_ESCUELA", "Plantel", plantelId,
                $"{r.GruposPromovidos} grupos subieron ({r.AlumnosMovidos} alumnos); {r.GruposEgresados} egresaron ({r.AlumnosEgresados} alumnos)" + (nivel is null ? "" : $" · {nivel}"),
                plantelId: plantelId);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Ciclo cerrado: {r.GruposPromovidos} grupo(s) pasaron de año con {r.AlumnosMovidos} alumno(s) y {r.GruposEgresados} grupo(s) egresaron ({r.AlumnosEgresados} alumno(s)).";
            return RedirectToAction(nameof(Index), new { plantelId });
        }

        // GET: Grupos/Delete/5
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Delete(int id)
        {
            var grupo = await _db.GruposEscolares.AsNoTracking().Include(g => g.Alumnos).FirstOrDefaultAsync(g => g.Id == id);
            return grupo is null ? NotFound() : View(grupo);
        }

        // POST: Grupos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var grupo = await _db.GruposEscolares.Include(g => g.Alumnos).FirstOrDefaultAsync(g => g.Id == id);
            if (grupo is null) return RedirectToAction(nameof(Index));

            // Los alumnos no se borran: quedan sin grupo y con su grado y letra actuales.
            foreach (var a in grupo.Alumnos) a.GrupoEscolarId = null;
            _db.GruposEscolares.Remove(grupo);
            _auditoria.Registrar("ELIMINAR_GRUPO", "Grupo", id, $"{grupo.Nombre} ({grupo.Alumnos.Count} alumnos quedaron sin grupo)", plantelId: grupo.PlantelId);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Grupo {grupo.Nombre} eliminado. Sus alumnos siguen registrados, sin grupo.";
            return RedirectToAction(nameof(Index), new { plantelId = grupo.PlantelId });
        }
    }
}
