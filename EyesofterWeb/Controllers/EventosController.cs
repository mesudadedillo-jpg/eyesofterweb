using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Eventos de la línea de tiempo de un caso. Se consultan desde el detalle del caso.
    /// Agregar: Administrador y Enfermería. Editar y eliminar: solo Administrador.
    /// </summary>
    [Authorize(Roles = Roles.AdminOEnfermeria)]
    public class EventosController : Controller
    {
        // Tipos que se generan automáticamente (no se capturan a mano).
        private static readonly TipoEvento[] TiposAutomaticos = { TipoEvento.Cierre, TipoEvento.Reapertura };

        private readonly ApplicationDbContext _context;

        public EventosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Eventos/Create?casoId=5
        public async Task<IActionResult> Create(int casoId)
        {
            var caso = await _context.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == casoId);
            if (caso is null) return NotFound();
            if (caso.Estado == EstadoCaso.Cerrado)
            {
                TempData["Error"] = "No se pueden agregar eventos a un caso cerrado.";
                return RedirectToAction("Details", "Casos", new { id = casoId });
            }

            CargarDatos(caso, null);
            return View(new EventoCaso { CasoId = casoId, Fecha = DateTime.Today });
        }

        // POST: Eventos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CasoId,Tipo,Fecha,Descripcion")] EventoCaso evento)
        {
            var caso = await _context.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == evento.CasoId);
            if (caso is null) return NotFound();

            ValidarReglas(caso, evento);
            if (!ModelState.IsValid)
            {
                CargarDatos(caso, evento.Tipo);
                return View(evento);
            }

            evento.RegistradoPor = User.Identity?.Name;
            _context.Add(evento);

            // Al canalizar o agendar cita, el caso pasa a "En seguimiento".
            if (caso.Estado == EstadoCaso.Abierto && (evento.Tipo == TipoEvento.Cita || evento.Tipo == TipoEvento.AvisoTutor))
            {
                caso.Estado = EstadoCaso.EnSeguimiento;
            }

            await _context.SaveChangesAsync();
            TempData["Exito"] = "Evento agregado a la línea de tiempo.";
            return RedirectToAction("Details", "Casos", new { id = evento.CasoId });
        }

        // GET: Eventos/Edit/5  (solo Administrador)
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null) return NotFound();
            var evento = await _context.EventosCaso.Include(e => e.Caso).ThenInclude(c => c!.Alumno).FirstOrDefaultAsync(e => e.Id == id);
            if (evento is null || evento.Caso is null) return NotFound();

            CargarDatos(evento.Caso, evento.Tipo);
            return View(evento);
        }

        // POST: Eventos/Edit/5  (solo Administrador)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CasoId,Tipo,Fecha,Descripcion")] EventoCaso datos)
        {
            if (id != datos.Id) return NotFound();
            var evento = await _context.EventosCaso.FindAsync(id);
            if (evento is null) return NotFound();
            var caso = await _context.Casos.Include(c => c.Alumno).FirstOrDefaultAsync(c => c.Id == evento.CasoId);
            if (caso is null) return NotFound();

            // Los eventos automáticos conservan su tipo.
            if (TiposAutomaticos.Contains(evento.Tipo)) datos.Tipo = evento.Tipo;
            else ValidarReglas(caso, datos, permitirCasoCerrado: true);

            if (!ModelState.IsValid)
            {
                CargarDatos(caso, datos.Tipo);
                return View(datos);
            }

            evento.Tipo = datos.Tipo;
            evento.Fecha = datos.Fecha;
            evento.Descripcion = datos.Descripcion;
            await _context.SaveChangesAsync();
            TempData["Exito"] = "Evento actualizado.";
            return RedirectToAction("Details", "Casos", new { id = evento.CasoId });
        }

        // POST: Eventos/Delete/5  (solo Administrador)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Administrador)]
        public async Task<IActionResult> Delete(int id)
        {
            var evento = await _context.EventosCaso.FindAsync(id);
            if (evento is null) return NotFound();

            if (evento.Tipo == TipoEvento.Tamizaje &&
                await _context.EventosCaso.CountAsync(e => e.CasoId == evento.CasoId && e.Tipo == TipoEvento.Tamizaje) <= 1)
            {
                TempData["Error"] = "No se puede eliminar el único tamizaje del caso: es el origen del expediente.";
                return RedirectToAction("Details", "Casos", new { id = evento.CasoId });
            }

            _context.EventosCaso.Remove(evento);
            await _context.SaveChangesAsync();
            TempData["Exito"] = "Evento eliminado.";
            return RedirectToAction("Details", "Casos", new { id = evento.CasoId });
        }

        private void ValidarReglas(Caso caso, EventoCaso evento, bool permitirCasoCerrado = false)
        {
            if (!permitirCasoCerrado && caso.Estado == EstadoCaso.Cerrado)
            {
                ModelState.AddModelError(string.Empty, "No se pueden agregar eventos a un caso cerrado.");
            }
            if (TiposAutomaticos.Contains(evento.Tipo))
            {
                ModelState.AddModelError(nameof(EventoCaso.Tipo), "El cierre y la reapertura se registran con sus botones, no a mano.");
            }
            if (evento.Fecha.Date < caso.FechaDeteccion.Date)
            {
                ModelState.AddModelError(nameof(EventoCaso.Fecha), "La fecha no puede ser anterior a la detección del caso.");
            }
            // Solo una cita puede programarse a futuro.
            if (evento.Tipo != TipoEvento.Cita && evento.Fecha.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(EventoCaso.Fecha), "Solo las citas pueden tener fecha futura.");
            }
        }

        private void CargarDatos(Caso caso, TipoEvento? seleccionado)
        {
            ViewBag.Caso = caso;
            var tipos = Enum.GetValues<TipoEvento>()
                .Where(t => !TiposAutomaticos.Contains(t) || t == seleccionado)
                .Select(t => new SelectListItem
                {
                    Value = t.ToString(),
                    Text = t switch
                    {
                        TipoEvento.AvisoTutor => "Aviso al tutor",
                        TipoEvento.Comprobante => "Comprobante de atención",
                        _ => t.ToString()
                    },
                    Selected = t == seleccionado
                });
            ViewBag.Tipos = tipos.ToList();
        }
    }
}
