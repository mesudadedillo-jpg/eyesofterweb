using System.Text.Encodings.Web;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Controllers
{
    /// <summary>
    /// Cuentas de acceso (solo Dirección). Son las mismas para el portal y la app móvil.
    /// Según el rol se vinculan con un tutor, un alumno o una clínica (C-RC-06, W-RFNN-04).
    /// </summary>
    [Authorize(Roles = Roles.Administrador)]
    public class UsuariosController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly ICorreo _correo;
        private readonly Auditoria _auditoria;

        public UsuariosController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, ICorreo correo, Auditoria auditoria)
        {
            _userManager = userManager;
            _db = db;
            _correo = correo;
            _auditoria = auditoria;
        }

        public async Task<IActionResult> Index(string? buscar)
        {
            var q = _db.Users.Include(u => u.Tutor).Include(u => u.Alumno).Include(u => u.Clinica).AsQueryable();
            if (!string.IsNullOrWhiteSpace(buscar)) q = q.Where(u => u.Email!.Contains(buscar) || u.NombreCompleto.Contains(buscar));
            var usuarios = await q.OrderBy(u => u.Email).ToListAsync();
            var lista = new List<UsuarioListaItem>();
            foreach (var u in usuarios)
            {
                lista.Add(new UsuarioListaItem
                {
                    Id = u.Id, NombreCompleto = u.NombreCompleto, Correo = u.Email ?? string.Empty,
                    Roles = await _userManager.GetRolesAsync(u),
                    Bloqueado = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    Activo = u.Activo,
                    Vinculo = u.Tutor?.NombreCompleto ?? u.Alumno?.NombreCompleto ?? u.Clinica?.Nombre
                });
            }
            ViewBag.Buscar = buscar;
            return View(lista);
        }

        public async Task<IActionResult> Crear(int? tutorId)
        {
            await CargarListasAsync();
            var m = new UsuarioFormViewModel { TutorId = tutorId, Rol = tutorId is null ? Roles.Enfermeria : Roles.Tutor };
            if (tutorId is not null && await _db.Tutores.FindAsync(tutorId) is { } t)
            {
                m.NombreCompleto = t.NombreCompleto;
                m.Correo = t.Correo ?? string.Empty;
            }
            return View("Form", m);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(UsuarioFormViewModel m)
        {
            await ValidarAsync(m);
            if (await _userManager.FindByEmailAsync(m.Correo) is not null) ModelState.AddModelError(nameof(m.Correo), "Ya existe una cuenta con ese correo.");
            if (!ModelState.IsValid) { await CargarListasAsync(); return View("Form", m); }

            var u = new ApplicationUser { UserName = m.Correo.Trim(), Email = m.Correo.Trim(), EmailConfirmed = true, NombreCompleto = m.NombreCompleto.Trim() };
            Vincular(u, m);
            // Sin contraseña: la persona la crea con el enlace (nadie más la conoce).
            var r = await _userManager.CreateAsync(u);
            if (!r.Succeeded)
            {
                foreach (var e in r.Errors) ModelState.AddModelError(string.Empty, e.Description);
                await CargarListasAsync();
                return View("Form", m);
            }
            await _userManager.AddToRoleAsync(u, m.Rol);
            var enlace = await EnlaceContrasenaAsync(u);
            var enviado = await _correo.EnviarAsync(u.Email!, "Tu cuenta de EYESOFTER",
                $"<p>Hola, {HtmlEncoder.Default.Encode(u.NombreCompleto)}:</p><p>La escuela creó tu cuenta de EYESOFTER ({Roles.NombreVisible(m.Rol)}). " +
                $"Crea tu contraseña aquí (el enlace vence en 2 horas):</p><p><a href=\"{HtmlEncoder.Default.Encode(enlace)}\">Crear mi contraseña</a></p>" +
                "<p>Con el mismo correo y contraseña entrarás al portal y a la app.</p>");
            _auditoria.Registrar("CREAR_USUARIO", "Usuario", u.Id, $"{u.Email} ({m.Rol})");
            await _db.SaveChangesAsync();
            if (enviado) TempData["Exito"] = $"Cuenta creada. Enviamos a {u.Email} el enlace para crear su contraseña.";
            else TempData["Enlace"] = enlace; // sin servicio de correo: Dirección entrega el enlace en persona
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Editar(string id)
        {
            var u = await _userManager.FindByIdAsync(id);
            if (u is null) return NotFound();
            await CargarListasAsync();
            return View("Form", new UsuarioFormViewModel
            {
                UsuarioId = u.Id, NombreCompleto = u.NombreCompleto, Correo = u.Email ?? string.Empty,
                Rol = (await _userManager.GetRolesAsync(u)).FirstOrDefault() ?? Roles.Tutor,
                TutorId = u.TutorId, AlumnoId = u.AlumnoId, ClinicaId = u.ClinicaId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(UsuarioFormViewModel m)
        {
            var u = await _userManager.FindByIdAsync(m.UsuarioId ?? "");
            if (u is null) return NotFound();
            await ValidarAsync(m);
            var otro = await _userManager.FindByEmailAsync(m.Correo);
            if (otro is not null && otro.Id != u.Id) ModelState.AddModelError(nameof(m.Correo), "Ya existe otra cuenta con ese correo.");
            var rolesActuales = await _userManager.GetRolesAsync(u);
            if (rolesActuales.Contains(Roles.Administrador) && m.Rol != Roles.Administrador && (await _userManager.GetUsersInRoleAsync(Roles.Administrador)).Count <= 1)
                ModelState.AddModelError(nameof(m.Rol), "No puedes quitar el rol a la última cuenta de Dirección.");
            if (!ModelState.IsValid) { await CargarListasAsync(); return View("Form", m); }

            u.NombreCompleto = m.NombreCompleto.Trim();
            if (!string.Equals(u.Email, m.Correo, StringComparison.OrdinalIgnoreCase))
            {
                await _userManager.SetEmailAsync(u, m.Correo.Trim());
                await _userManager.SetUserNameAsync(u, m.Correo.Trim());
            }
            u.TutorId = u.AlumnoId = u.ClinicaId = null;
            Vincular(u, m);
            await _userManager.UpdateAsync(u);
            if (!rolesActuales.Contains(m.Rol))
            {
                await _userManager.RemoveFromRolesAsync(u, rolesActuales);
                await _userManager.AddToRoleAsync(u, m.Rol);
            }
            // Cierra las sesiones abiertas de esa cuenta para que el cambio aplique de inmediato.
            await _userManager.UpdateSecurityStampAsync(u);
            _auditoria.Registrar("EDITAR_USUARIO", "Usuario", u.Id, $"{u.Email} ({m.Rol})");
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Se actualizó la cuenta de {u.NombreCompleto}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarActivo(string id)
        {
            var u = await _userManager.FindByIdAsync(id);
            if (u is null) return NotFound();
            if (u.Id == _userManager.GetUserId(User)) { TempData["Error"] = "No puedes desactivar tu propia cuenta."; return RedirectToAction(nameof(Index)); }
            u.Activo = !u.Activo;
            await _userManager.UpdateAsync(u);
            await _userManager.UpdateSecurityStampAsync(u);
            if (!u.Activo) await _db.Dispositivos.Where(d => d.UsuarioId == u.Id).ExecuteDeleteAsync();
            _auditoria.Registrar(u.Activo ? "ACTIVAR_USUARIO" : "DESACTIVAR_USUARIO", "Usuario", u.Id, u.Email);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"{u.NombreCompleto} quedó {(u.Activo ? "activa" : "desactivada (ya no puede entrar ni en la web ni en la app)")}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Desbloquear(string id)
        {
            var u = await _userManager.FindByIdAsync(id);
            if (u is null) return NotFound();
            await _userManager.SetLockoutEndDateAsync(u, null);
            await _userManager.ResetAccessFailedCountAsync(u);
            _auditoria.Registrar("DESBLOQUEAR_USUARIO", "Usuario", u.Id, u.Email);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Se desbloqueó la cuenta de {u.Email}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarEnlace(string id)
        {
            var u = await _userManager.FindByIdAsync(id);
            if (u is null) return NotFound();
            var enlace = await EnlaceContrasenaAsync(u);
            var enviado = await _correo.EnviarAsync(u.Email!, "EYESOFTER: crea tu contraseña",
                $"<p>Para crear o cambiar tu contraseña entra aquí (vence en 2 horas):</p><p><a href=\"{HtmlEncoder.Default.Encode(enlace)}\">Crear mi contraseña</a></p>");
            if (enviado) TempData["Exito"] = $"Enviamos el enlace a {u.Email}.";
            else TempData["Enlace"] = enlace;
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> EnlaceContrasenaAsync(ApplicationUser u)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(u);
            return Url.Action("Restablecer", "Cuenta", new { correo = u.Email, token }, Request.Scheme)!;
        }

        private static void Vincular(ApplicationUser u, UsuarioFormViewModel m)
        {
            if (m.Rol == Roles.Tutor) u.TutorId = m.TutorId;
            if (m.Rol == Roles.Alumno) u.AlumnoId = m.AlumnoId;
            if (m.Rol == Roles.Especialista) u.ClinicaId = m.ClinicaId;
        }

        private async Task ValidarAsync(UsuarioFormViewModel m)
        {
            if (!Roles.Todos.Contains(m.Rol)) ModelState.AddModelError(nameof(m.Rol), "El rol seleccionado no existe.");
            if (m.Rol == Roles.Tutor && (m.TutorId is null || !await _db.Tutores.AnyAsync(t => t.Id == m.TutorId)))
                ModelState.AddModelError(nameof(m.TutorId), "Una cuenta de tutor debe vincularse con un tutor registrado.");
            if (m.Rol == Roles.Alumno && (m.AlumnoId is null || !await _db.Alumnos.AnyAsync(a => a.Id == m.AlumnoId)))
                ModelState.AddModelError(nameof(m.AlumnoId), "Una cuenta de alumno debe vincularse con un alumno.");
            if (m.Rol == Roles.Especialista && (m.ClinicaId is null || !await _db.Clinicas.AnyAsync(c => c.Id == m.ClinicaId)))
                ModelState.AddModelError(nameof(m.ClinicaId), "Una cuenta de especialista debe vincularse con su clínica.");
        }

        private async Task CargarListasAsync()
        {
            ViewBag.Roles = new SelectList(Roles.Todos.Select(r => new { Valor = r, Texto = Roles.NombreVisible(r) }), "Valor", "Texto");
            ViewBag.Tutores = new SelectList(await _db.Tutores.OrderBy(t => t.Apellidos).Select(t => new { t.Id, Texto = t.Apellidos + " " + t.Nombre }).ToListAsync(), "Id", "Texto");
            ViewBag.Alumnos = new SelectList(await _db.Alumnos.OrderBy(a => a.Apellidos).Select(a => new { a.Id, Texto = a.Apellidos + " " + a.Nombre + " (" + a.Matricula + ")" }).ToListAsync(), "Id", "Texto");
            ViewBag.Clinicas = new SelectList(await _db.Clinicas.OrderBy(c => c.Nombre).Select(c => new { c.Id, Texto = c.Nombre + (c.EsAsociada ? " ✓" : "") }).ToListAsync(), "Id", "Texto");
        }
    }
}
