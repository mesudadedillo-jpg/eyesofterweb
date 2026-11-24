using EyesofterWeb.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EyesofterWeb.Models;

namespace EyesofterWeb.ViewComponents
{
    /// <summary>Globito con las notificaciones sin leer del usuario (en la barra superior).</summary>
    public class ContadorNotificacionesViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _usuarios;

        public ContadorNotificacionesViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> usuarios)
        {
            _db = db;
            _usuarios = usuarios;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var id = _usuarios.GetUserId(UserClaimsPrincipal);
            var n = id is null ? 0 : await _db.Notificaciones.CountAsync(x => x.UsuarioId == id && x.LeidaEn == null);
            return View(n);
        }
    }
}
