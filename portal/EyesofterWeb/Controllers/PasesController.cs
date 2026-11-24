using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Lectura del pase médico por el especialista de una clínica asociada (W-RFN-11, W-RI-02).
    /// W-RFNN-04: solo el rol Especialista, y el servicio exige que su clínica sea asociada y activa.
    /// </summary>
    [Authorize(Roles = Roles.Especialista)]
    public class PasesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _usuarios;
        private readonly Expedientes _expedientes;
        private readonly Auditoria _auditoria;

        public PasesController(ApplicationDbContext db, UserManager<ApplicationUser> usuarios, Expedientes expedientes, Auditoria auditoria)
        {
            _db = db;
            _usuarios = usuarios;
            _expedientes = expedientes;
            _auditoria = auditoria;
        }

        public async Task<IActionResult> Escanear()
        {
            var yo = await _usuarios.GetUserAsync(User);
            var clinica = yo?.ClinicaId is null ? null : await _db.Clinicas.FindAsync(yo.ClinicaId);
            ViewBag.Clinica = clinica;
            ViewBag.Habilitado = clinica is { EsAsociada: true, Activa: true };
            return View();
        }

        /// <summary>A esta dirección lleva el QR. También recibe el código escrito a mano.</summary>
        [EnableRateLimiting("acceso")]
        public async Task<IActionResult> Validar(string? codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return RedirectToAction(nameof(Escanear));
            var yo = await _usuarios.GetUserAsync(User);
            var r = await _expedientes.ValidarPaseAsync(codigo, yo!);
            if (!r.Valido)
            {
                // W-RFNN-03: se rechaza con un mensaje claro y se deja constancia del intento.
                await _auditoria.DenegadoAsync("Pase", Expedientes.NormalizarCodigo(codigo), r.Mensaje);
                ViewBag.Mensaje = r.Mensaje;
                return View("Invalido");
            }
            _auditoria.Registrar("VALIDAR_PASE", "Caso", r.Pase!.CasoId, r.Pase.Clinica?.Nombre);
            await _db.SaveChangesAsync();
            ViewBag.Pase = r.Pase;
            ViewBag.Convenio = await _expedientes.ConvenioVigenteAsync(r.Pase.Caso!.AlumnoId, r.Pase.ClinicaId);
            return View(new AtencionQrViewModel { Codigo = r.Pase.Codigo });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Atender(AtencionQrViewModel m)
        {
            var yo = await _usuarios.GetUserAsync(User);
            var r = await _expedientes.ValidarPaseAsync(m.Codigo, yo!);
            if (!r.Valido)
            {
                await _auditoria.DenegadoAsync("Pase", m.Codigo, r.Mensaje);
                ViewBag.Mensaje = r.Mensaje;
                return View("Invalido");
            }
            var caso = r.Pase!.Caso!;
            if (m.Fecha.Date > DateTime.Today) ModelState.AddModelError(nameof(m.Fecha), "La fecha no puede ser futura.");
            if (m.Fecha.Date < caso.FechaDeteccion) ModelState.AddModelError(nameof(m.Fecha), "La atención no puede ser anterior al tamizaje.");
            if (!ModelState.IsValid)
            {
                ViewBag.Pase = r.Pase;
                return View("Validar", m);
            }
            await _expedientes.RegistrarAtencionAsync(caso, new Atencion
            {
                ClinicaId = r.Pase.ClinicaId, Fecha = m.Fecha.Date, Valoracion = m.Valoracion.Trim(), Indicaciones = m.Indicaciones?.Trim(),
                CorreccionOptica = m.CorreccionOptica, Origen = OrigenAtencion.PaseQr
            }, User.Identity?.Name, r.Pase);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Atención de {caso.Alumno!.NombreCompleto} registrada. La escuela y la familia ya fueron notificadas.";
            return RedirectToAction(nameof(Escanear));
        }
    }
}
