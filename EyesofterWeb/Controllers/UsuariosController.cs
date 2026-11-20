using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>Administración de usuarios y asignación de roles. Solo Administrador.</summary>
    [Authorize(Roles = Roles.Administrador)]
    public class UsuariosController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UsuariosController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // GET: /Usuarios
        public async Task<IActionResult> Index()
        {
            var usuarios = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
            var lista = new List<UsuarioListaItem>();
            foreach (var u in usuarios)
            {
                lista.Add(new UsuarioListaItem
                {
                    Id = u.Id,
                    NombreCompleto = u.NombreCompleto,
                    Correo = u.Email ?? string.Empty,
                    Roles = await _userManager.GetRolesAsync(u),
                    Bloqueado = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow
                });
            }
            return View(lista);
        }

        // GET: /Usuarios/EditarRol/{id}
        public async Task<IActionResult> EditarRol(string? id)
        {
            if (id is null) return NotFound();
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario is null) return NotFound();

            var roles = await _userManager.GetRolesAsync(usuario);
            ViewBag.Roles = new SelectList(Roles.Todos);
            return View(new EditarRolViewModel
            {
                UsuarioId = usuario.Id,
                NombreCompleto = usuario.NombreCompleto,
                Correo = usuario.Email ?? string.Empty,
                Rol = roles.FirstOrDefault() ?? Roles.Docente
            });
        }

        // POST: /Usuarios/EditarRol
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarRol(EditarRolViewModel modelo)
        {
            var usuario = await _userManager.FindByIdAsync(modelo.UsuarioId);
            if (usuario is null) return NotFound();

            if (!Roles.Todos.Contains(modelo.Rol))
            {
                ModelState.AddModelError(nameof(modelo.Rol), "El rol seleccionado no existe.");
            }

            // Regla de negocio: siempre debe quedar al menos un Administrador.
            var rolesActuales = await _userManager.GetRolesAsync(usuario);
            if (rolesActuales.Contains(Roles.Administrador) && modelo.Rol != Roles.Administrador)
            {
                var admins = await _userManager.GetUsersInRoleAsync(Roles.Administrador);
                if (admins.Count <= 1)
                {
                    ModelState.AddModelError(nameof(modelo.Rol), "No puedes quitar el rol al último Administrador.");
                }
            }

            if (!ModelState.IsValid)
            {
                modelo.NombreCompleto = usuario.NombreCompleto;
                modelo.Correo = usuario.Email ?? string.Empty;
                ViewBag.Roles = new SelectList(Roles.Todos);
                return View(modelo);
            }

            await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
            await _userManager.AddToRoleAsync(usuario, modelo.Rol);
            // Invalida la cookie del usuario para que el nuevo rol se aplique en su próxima petición.
            await _userManager.UpdateSecurityStampAsync(usuario);

            TempData["Exito"] = $"{usuario.NombreCompleto} ahora tiene el rol {modelo.Rol}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Usuarios/Desbloquear/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Desbloquear(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario is null) return NotFound();
            await _userManager.SetLockoutEndDateAsync(usuario, null);
            await _userManager.ResetAccessFailedCountAsync(usuario);
            TempData["Exito"] = $"Se desbloqueó la cuenta de {usuario.Email}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
