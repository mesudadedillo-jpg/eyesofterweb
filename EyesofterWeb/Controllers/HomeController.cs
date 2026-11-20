using System.Diagnostics;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Página pública. Si hay sesión iniciada muestra el tablero.
        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return View("Bienvenida");
            }

            var modelo = new TableroViewModel();
            try
            {
                modelo.Tutores = await _context.Tutores.CountAsync();
                modelo.Alumnos = await _context.Alumnos.CountAsync();
                modelo.Clinicas = await _context.Clinicas.CountAsync();
                modelo.CasosAbiertos = await _context.Casos.CountAsync(c => c.Estado != EstadoCaso.Cerrado);
                modelo.CasosRojos = await _context.Casos.CountAsync(c => c.Estado != EstadoCaso.Cerrado && c.Semaforo == Semaforo.Rojo);
                modelo.CasosCerrados = await _context.Casos.CountAsync(c => c.Estado == EstadoCaso.Cerrado);
                modelo.UltimosCasos = await _context.Casos
                    .Include(c => c.Alumno)
                    .OrderByDescending(c => c.FechaDeteccion)
                    .Take(5)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo leer la base de datos.");
                modelo.BaseDeDatosLista = false;
            }

            return View(modelo);
        }

        // Matriz de permisos por rol (útil para explicar y probar la autorización).
        [Authorize]
        public IActionResult Permisos()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
