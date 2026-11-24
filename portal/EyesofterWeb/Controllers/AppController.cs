using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// "Descarga la app": página pública con el instalador de Android y un QR para abrirla desde
    /// el teléfono. La app de escritorio abre esta misma página (y muestra su QR), así que el enlace
    /// del instalador se cambia en un solo lugar (Eyesofter__UrlApk).
    /// </summary>
    [AllowAnonymous]
    public class AppController : Controller
    {
        private readonly OpcionesEyesofter _opciones;
        private readonly Documentos _documentos;

        public AppController(IOptions<OpcionesEyesofter> opciones, Documentos documentos)
        {
            _opciones = opciones.Value;
            _documentos = documentos;
        }

        private string UrlPagina => $"{_opciones.UrlWeb.TrimEnd('/')}/App";

        public IActionResult Index()
        {
            ViewBag.UrlApk = _opciones.UrlApk;
            ViewBag.UrlPagina = UrlPagina;
            return View();
        }

        /// <summary>QR con la dirección de esta página, para escanearlo desde el teléfono.</summary>
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public IActionResult Qr() => File(_documentos.QrPng(UrlPagina, 12), "image/png");

        /// <summary>Atajo corto para escribir a mano o imprimir: /App/Android lleva directo al instalador.</summary>
        public IActionResult Android() => Redirect(_opciones.UrlApk);
    }
}
