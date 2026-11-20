using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EyesofterWeb.Seguridad
{
    /// <summary>
    /// C-RC-08: antes de tratar datos de menores, el tutor debe aceptar el aviso de privacidad
    /// vigente. Mientras no lo haga, cualquier página lo manda a aceptarlo (salvo la propia
    /// página del aviso, los términos, el cierre de sesión y la de errores).
    /// </summary>
    public class FiltroConsentimiento : IAsyncActionFilter
    {
        private static readonly HashSet<string> Libres = new(StringComparer.OrdinalIgnoreCase) { "Cuenta", "Privacidad", "Soporte" };

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _usuarios;
        private readonly OpcionesEyesofter _opciones;

        public FiltroConsentimiento(ApplicationDbContext db, UserManager<ApplicationUser> usuarios, IOptions<OpcionesEyesofter> opciones)
        {
            _db = db;
            _usuarios = usuarios;
            _opciones = opciones.Value;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate siguiente)
        {
            var controlador = ctx.RouteData.Values["controller"]?.ToString() ?? string.Empty;
            if (ctx.HttpContext.User.IsInRole(Roles.Tutor) && !Libres.Contains(controlador))
            {
                var id = _usuarios.GetUserId(ctx.HttpContext.User);
                var acepto = await _db.Consentimientos.AnyAsync(c => c.UsuarioId == id && c.VersionAviso == _opciones.VersionAvisoPrivacidad);
                if (!acepto)
                {
                    ctx.Result = new RedirectToActionResult("Aceptar", "Privacidad", new { returnUrl = ctx.HttpContext.Request.Path.Value });
                    return;
                }
            }
            await siguiente();
        }
    }
}
