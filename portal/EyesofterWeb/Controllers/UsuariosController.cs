using System.Text.Encodings.Web;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Models.ViewModels;
using EyesofterWeb.Seguridad;
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
    ///
    /// Multiescuela (C-RE-01): Dirección solo ve y administra las cuentas de SUS escuelas:
    /// personal asignado a ellas, tutores y alumnos de ellas, y especialistas de clínicas con
    /// convenio con ellas. El administrador global ve todas y es el único que da esa marca.
    /// </summary>
    [Authorize(Roles = Roles.Administrador)]
    public class UsuariosController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly ICorreo _correo;
        private readonly Auditoria _auditoria;
        private readonly AlcanceEscuela _alcance;
        private readonly Escuelas _escuelas;

        public UsuariosController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, ICorreo correo, Auditoria auditoria,
            AlcanceEscuela alcance, Escuelas escuelas)
        {
            _userManager = userManager;
            _db = db;
            _correo = correo;
            _auditoria = auditoria;
            _alcance = alcance;
            _escuelas = escuelas;
        }

        private static bool EsRolDePersonal(string rol) => rol is Roles.Administrador or Roles.Enfermeria;

        /// <summary>Cuentas que puede ver y administrar quien hace la petición.</summary>
        private IQueryable<ApplicationUser> Visibles()
        {
            var q = _db.Users.AsQueryable();
            if (_alcance.SinRestriccion) return q;
            var planteles = _alcance.Planteles;
            // Estas tres consultas ya vienen filtradas por escuela (filtros globales).
            var tutores = _db.Tutores.Select(t => t.Id);
            var alumnos = _db.Alumnos.Select(a => a.Id);
            var clinicas = _db.Convenios.Select(c => c.ClinicaId);
            return q.Where(u => u.Planteles.Any(p => planteles.Contains(p.PlantelId))
                                || (u.TutorId != null && tutores.Contains(u.TutorId.Value))
                                || (u.AlumnoId != null && alumnos.Contains(u.AlumnoId.Value))
                                || (u.ClinicaId != null && clinicas.Contains(u.ClinicaId.Value)));
        }

        /// <summary>La cuenta, solo si es visible; un administrador global solo lo toca otro administrador global.</summary>
        private async Task<ApplicationUser?> VisibleAsync(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var u = await Visibles().Include(x => x.Planteles).FirstOrDefaultAsync(x => x.Id == id);
            if (u is null || (u.AdministradorGlobal && !_alcance.EsGlobal)) return null;
            return u;
        }

        /// <summary>
        /// ¿La cuenta es SOLO de las escuelas de quien administra? Si también pertenece a otra escuela
        /// (personal en dos escuelas, tutor con hijos en otra, especialista de una clínica con convenio
        /// con otra), cambiar su correo, contraseña o dos pasos le daría acceso a datos de esa otra
        /// escuela: eso solo lo hace el administrador global.
        /// </summary>
        private async Task<bool> EsSoloDeMisEscuelasAsync(ApplicationUser u)
        {
            if (_alcance.SinRestriccion) return true;
            var mias = _alcance.Planteles;
            if (u.Planteles.Any(p => !mias.Contains(p.PlantelId))) return false;
            if (u.TutorId is int tutor)
            {
                if (await _db.Alumnos.IgnoreQueryFilters().AnyAsync(a => a.TutorId == tutor && !mias.Contains(a.PlantelId))) return false;
                if (await _db.Tutores.IgnoreQueryFilters().AnyAsync(t => t.Id == tutor && !mias.Contains(t.PlantelId))) return false;
            }
            if (u.AlumnoId is int alumno && await _db.Alumnos.IgnoreQueryFilters().AnyAsync(a => a.Id == alumno && !mias.Contains(a.PlantelId))) return false;
            if (u.ClinicaId is int clinica && await _db.Convenios.IgnoreQueryFilters().AnyAsync(c => c.ClinicaId == clinica && !mias.Contains(c.PlantelId))) return false;
            return true;
        }

        /// <summary>La cuenta, solo si es visible Y es solo de mis escuelas (para modificarla).</summary>
        private async Task<ApplicationUser?> AdministrableAsync(string? id)
        {
            var u = await VisibleAsync(id);
            if (u is null) return null;
            if (!await EsSoloDeMisEscuelasAsync(u))
            {
                TempData["Error"] = $"{u.Email} también pertenece a otra escuela. Por seguridad, solo el administrador global puede modificar esa cuenta.";
                return null;
            }
            return u;
        }

        public async Task<IActionResult> Index(string? buscar)
        {
            var q = Visibles().Include(u => u.Tutor).Include(u => u.Alumno).Include(u => u.Clinica).Include(u => u.Planteles).ThenInclude(p => p.Plantel).AsQueryable();
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
                    DosPasos = u.TwoFactorEnabled,
                    Activo = u.Activo,
                    Vinculo = u.Tutor?.NombreCompleto ?? u.Alumno?.NombreCompleto ?? u.Clinica?.Nombre,
                    Escuelas = u.AdministradorGlobal ? "Todas (administrador global)"
                        : u.Planteles.Count == 0 ? null : string.Join(", ", u.Planteles.Select(p => p.Plantel?.Nombre).OrderBy(n => n))
                });
            }
            ViewBag.Buscar = buscar;
            return View(lista);
        }

        public async Task<IActionResult> Crear(int? tutorId)
        {
            await CargarListasAsync();
            var m = new UsuarioFormViewModel { TutorId = tutorId, Rol = tutorId is null ? Roles.Enfermeria : Roles.Tutor };
            if (await _escuelas.PorDefectoAsync() is int plantel) m.PlantelIds.Add(plantel);
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
            await AsignarEscuelasAsync(u, m);
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
            _auditoria.Registrar("CREAR_USUARIO", "Usuario", u.Id, $"{u.Email} ({m.Rol})", plantelId: m.PlantelIds.Count == 1 ? m.PlantelIds[0] : null);
            await _db.SaveChangesAsync();
            if (enviado) TempData["Exito"] = $"Cuenta creada. Enviamos a {u.Email} el enlace para crear su contraseña.";
            else TempData["Enlace"] = enlace; // sin servicio de correo: Dirección entrega el enlace en persona
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Editar(string id)
        {
            var u = await AdministrableAsync(id);
            if (u is null) return TempData.ContainsKey("Error") ? RedirectToAction(nameof(Index)) : NotFound();
            await CargarListasAsync();
            return View("Form", new UsuarioFormViewModel
            {
                UsuarioId = u.Id, NombreCompleto = u.NombreCompleto, Correo = u.Email ?? string.Empty,
                Rol = (await _userManager.GetRolesAsync(u)).FirstOrDefault() ?? Roles.Tutor,
                TutorId = u.TutorId, AlumnoId = u.AlumnoId, ClinicaId = u.ClinicaId,
                PlantelIds = u.Planteles.Select(p => p.PlantelId).ToList(), AdministradorGlobal = u.AdministradorGlobal
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(UsuarioFormViewModel m)
        {
            var u = await AdministrableAsync(m.UsuarioId);
            if (u is null) return TempData.ContainsKey("Error") ? RedirectToAction(nameof(Index)) : NotFound();
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
            await AsignarEscuelasAsync(u, m);
            await _userManager.UpdateAsync(u);
            if (!rolesActuales.Contains(m.Rol))
            {
                await _userManager.RemoveFromRolesAsync(u, rolesActuales);
                await _userManager.AddToRoleAsync(u, m.Rol);
            }
            // Cierra las sesiones abiertas de esa cuenta para que el cambio aplique de inmediato.
            await _userManager.UpdateSecurityStampAsync(u);
            _auditoria.Registrar("EDITAR_USUARIO", "Usuario", u.Id, $"{u.Email} ({m.Rol})", plantelId: m.PlantelIds.Count == 1 ? m.PlantelIds[0] : null);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Se actualizó la cuenta de {u.NombreCompleto}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarActivo(string id)
        {
            var u = await AdministrableAsync(id);
            if (u is null) return TempData.ContainsKey("Error") ? RedirectToAction(nameof(Index)) : NotFound();
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
            var u = await VisibleAsync(id);
            if (u is null) return NotFound();
            await _userManager.SetLockoutEndDateAsync(u, null);
            await _userManager.ResetAccessFailedCountAsync(u);
            _auditoria.Registrar("DESBLOQUEAR_USUARIO", "Usuario", u.Id, u.Email);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Se desbloqueó la cuenta de {u.Email}.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// La persona perdió su teléfono y sus códigos de respaldo: se borra su app autenticadora
        /// y se cierran sus sesiones. Al volver a entrar tendrá que configurarla de nuevo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestablecerDosPasos(string id)
        {
            var u = await AdministrableAsync(id);
            if (u is null) return TempData.ContainsKey("Error") ? RedirectToAction(nameof(Index)) : NotFound();
            await _userManager.SetTwoFactorEnabledAsync(u, false);
            await _userManager.ResetAuthenticatorKeyAsync(u);
            await _userManager.UpdateSecurityStampAsync(u);
            _auditoria.Registrar("RESTABLECER_2FA", "Usuario", u.Id, u.Email);
            await _db.SaveChangesAsync();
            TempData["Exito"] = $"Se restableció la verificación en dos pasos de {u.Email}. Al entrar la configurará de nuevo.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarEnlace(string id)
        {
            var u = await AdministrableAsync(id);
            if (u is null) return TempData.ContainsKey("Error") ? RedirectToAction(nameof(Index)) : NotFound();
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

        /// <summary>
        /// Escuelas del personal. Solo se tocan las escuelas que quien edita puede ver: si la
        /// persona también trabaja en otra escuela, esa asignación se conserva.
        /// </summary>
        private async Task AsignarEscuelasAsync(ApplicationUser u, UsuarioFormViewModel m)
        {
            var permitidas = (await _escuelas.VisiblesAsync()).Select(p => p.Id).ToHashSet();
            var deseadas = EsRolDePersonal(m.Rol) ? m.PlantelIds.Where(permitidas.Contains).ToHashSet() : new HashSet<int>();
            if (string.IsNullOrEmpty(u.Id) || !await _db.UsuarioPlanteles.AnyAsync(x => x.UsuarioId == u.Id))
            {
                foreach (var p in deseadas) u.Planteles.Add(new UsuarioPlantel { UsuarioId = u.Id, PlantelId = p });
            }
            else
            {
                var actuales = await _db.UsuarioPlanteles.Where(x => x.UsuarioId == u.Id).ToListAsync();
                _db.UsuarioPlanteles.RemoveRange(actuales.Where(x => permitidas.Contains(x.PlantelId) && !deseadas.Contains(x.PlantelId)));
                foreach (var p in deseadas.Where(p => actuales.All(x => x.PlantelId != p)))
                    _db.UsuarioPlanteles.Add(new UsuarioPlantel { UsuarioId = u.Id, PlantelId = p });
            }
            // Solo un administrador global da o quita esa marca, y solo a cuentas de Dirección.
            if (_alcance.EsGlobal) u.AdministradorGlobal = m.Rol == Roles.Administrador && m.AdministradorGlobal;
            else if (m.Rol != Roles.Administrador) u.AdministradorGlobal = false;
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
            // Una escuela solo da de alta especialistas de clínicas con las que tiene convenio
            // (Convenios viene filtrado por escuela). El administrador global, de cualquiera.
            else if (m.Rol == Roles.Especialista && !_alcance.SinRestriccion && !await _db.Convenios.AnyAsync(c => c.ClinicaId == m.ClinicaId))
                ModelState.AddModelError(nameof(m.ClinicaId), "Tu escuela no tiene convenio con esa clínica. Regístralo primero en Convenios.");
            // El vínculo (tutor, alumno o clínica) no debe alcanzar datos de otra escuela.
            if (ModelState.IsValid && !_alcance.SinRestriccion)
            {
                var vinculo = new ApplicationUser
                {
                    TutorId = m.Rol == Roles.Tutor ? m.TutorId : null,
                    AlumnoId = m.Rol == Roles.Alumno ? m.AlumnoId : null,
                    ClinicaId = m.Rol == Roles.Especialista ? m.ClinicaId : null
                };
                if (!await EsSoloDeMisEscuelasAsync(vinculo))
                    ModelState.AddModelError(string.Empty, "Ese tutor, alumno o clínica también está ligado a otra escuela. Por seguridad, esa cuenta la crea el administrador global.");
            }
            if (EsRolDePersonal(m.Rol))
            {
                var permitidas = (await _escuelas.VisiblesAsync()).Select(p => p.Id).ToHashSet();
                var conservadas = string.IsNullOrEmpty(m.UsuarioId) ? 0
                    : await _db.UsuarioPlanteles.CountAsync(x => x.UsuarioId == m.UsuarioId && !permitidas.Contains(x.PlantelId));
                var globalSolicitado = _alcance.EsGlobal && m.Rol == Roles.Administrador && m.AdministradorGlobal;
                if (!globalSolicitado && conservadas == 0 && !m.PlantelIds.Any(permitidas.Contains))
                    ModelState.AddModelError(nameof(m.PlantelIds), "Elige al menos una escuela.");
            }
        }

        private async Task CargarListasAsync()
        {
            ViewBag.Planteles = await _escuelas.VisiblesAsync();
            ViewBag.PuedeMarcarGlobal = _alcance.EsGlobal;
            ViewBag.Roles = new SelectList(Roles.Todos.Select(r => new { Valor = r, Texto = Roles.NombreVisible(r) }), "Valor", "Texto");
            ViewBag.Tutores = new SelectList(await _db.Tutores.OrderBy(t => t.Apellidos).Select(t => new { t.Id, Texto = t.Apellidos + " " + t.Nombre }).ToListAsync(), "Id", "Texto");
            ViewBag.Alumnos = new SelectList(await _db.Alumnos.OrderBy(a => a.Apellidos).Select(a => new { a.Id, Texto = a.Apellidos + " " + a.Nombre + " (" + a.Matricula + ")" }).ToListAsync(), "Id", "Texto");
            var clinicas = _db.Clinicas.AsQueryable();
            if (!_alcance.SinRestriccion)
            {
                var conConvenio = _db.Convenios.Select(c => c.ClinicaId);
                clinicas = clinicas.Where(c => conConvenio.Contains(c.Id));
            }
            ViewBag.Clinicas = new SelectList(await clinicas.OrderBy(c => c.Nombre).Select(c => new { c.Id, Texto = c.Nombre + (c.EsAsociada ? " ✓" : "") }).ToListAsync(), "Id", "Texto");
        }
    }
}
