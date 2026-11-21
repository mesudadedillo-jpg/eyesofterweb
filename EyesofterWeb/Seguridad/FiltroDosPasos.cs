using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace EyesofterWeb.Seguridad
{
    /// <summary>
    /// Dirección y Enfermería no ven ningún dato hasta activar la verificación en dos pasos:
    /// cualquier página las manda a configurarla (salvo su cuenta, el aviso y la ayuda).
    /// </summary>
    public class FiltroDosPasos : IActionFilter
    {
        private static readonly HashSet<string> Libres = new(StringComparer.OrdinalIgnoreCase) { "Cuenta", "Privacidad", "Soporte" };
        private readonly OpcionesEyesofter _opciones;

        public FiltroDosPasos(IOptions<OpcionesEyesofter> opciones) => _opciones = opciones.Value;

        public void OnActionExecuting(ActionExecutingContext ctx)
        {
            if (!_opciones.Exigir2FAPersonal) return;
            var u = ctx.HttpContext.User;
            if (!(u.IsInRole(Roles.Administrador) || u.IsInRole(Roles.Enfermeria))) return;
            if (u.HasClaim(AlcanceEscuela.ClaimDosPasos, "true")) return;
            var controlador = ctx.RouteData.Values["controller"]?.ToString() ?? string.Empty;
            if (Libres.Contains(controlador)) return;
            ctx.Result = new RedirectToActionResult("DosPasos", "Cuenta", null);
        }

        public void OnActionExecuted(ActionExecutedContext ctx) { }
    }
}
