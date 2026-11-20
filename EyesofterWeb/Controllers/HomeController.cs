using System.Diagnostics;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly Indicadores _indicadores;
        private readonly Documentos _documentos;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext db, Indicadores indicadores, Documentos documentos, ILogger<HomeController> logger)
        {
            _db = db;
            _indicadores = indicadores;
            _documentos = documentos;
            _logger = logger;
        }

        /// <summary>
        /// Página de inicio según el rol. Para Dirección y Enfermería es el dashboard institucional
        /// con los casos abiertos a un clic (W-RC-02).
        /// </summary>
        public async Task<IActionResult> Index(int? campanaId)
        {
            if (User.Identity?.IsAuthenticated != true) return View("Bienvenida");
            if (User.IsInRole(Roles.Tutor)) return RedirectToAction("Index", "MisHijos");
            if (User.IsInRole(Roles.Especialista)) return RedirectToAction("Escanear", "Pases");
            if (User.IsInRole(Roles.Alumno)) return View("Alumno");
            if (!User.IsInRole(Roles.Administrador) && !User.IsInRole(Roles.Enfermeria)) return View("SinRol");

            try
            {
                var tablero = await _indicadores.CalcularAsync(campanaId);
                ViewBag.CasosUrgentes = await _db.Casos.AsNoTracking().Include(c => c.Alumno)
                    .Where(c => c.Estado != EstadoCaso.Cerrado)
                    .OrderByDescending(c => c.Semaforo).ThenBy(c => c.FechaDeteccion)
                    .Take(8).ToListAsync();
                return View(tablero);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo leer la base de datos.");
                return View("BaseNoLista");
            }
        }

        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> TableroPdf(int? campanaId)
        {
            var t = await _indicadores.CalcularAsync(campanaId);
            return File(_documentos.PdfTablero(t), "application/pdf", $"dashboard-{DateTime.Today:yyyyMMdd}.pdf");
        }

        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public async Task<IActionResult> TableroExcel(int? campanaId)
        {
            var t = await _indicadores.CalcularAsync(campanaId);
            return File(_documentos.ExcelTablero(t), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"dashboard-{DateTime.Today:yyyyMMdd}.xlsx");
        }

        /// <summary>Lo llama el aviso de inactividad para renovar la sesión.</summary>
        [Authorize]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Mantener() => NoContent();

        [Authorize(Roles = Roles.AdminOEnfermeria)]
        public IActionResult Permisos() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        /// <summary>Páginas de error amables para 404, 403, 429...</summary>
        public IActionResult Estado(int id)
        {
            ViewBag.Codigo = id;
            return View();
        }
    }
}
