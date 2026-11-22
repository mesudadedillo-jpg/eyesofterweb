using System.Text.Json;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Servicios
{
    /// <summary>
    /// Crea notificaciones en la bandeja compartida. El portal NO envía push ni correo: deja la
    /// notificación con PendienteEnvio = true y el servidor de la app (Ktor) la entrega en
    /// segundos por Firebase o correo (bandeja de salida). Así hay un solo lugar que habla con
    /// Firebase y Resend, y la notificación se ve igual en la web y en la app.
    /// Las notificaciones se agregan al contexto: se guardan con el SaveChanges del cambio.
    /// </summary>
    public class Notificador
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _usuarios;

        public Notificador(ApplicationDbContext db, UserManager<ApplicationUser> usuarios)
        {
            _db = db;
            _usuarios = usuarios;
        }

        public void AUsuario(string usuarioId, string tipo, string titulo, string mensaje, IDictionary<string, string>? datos = null)
        {
            var d = new Dictionary<string, string>(datos ?? new Dictionary<string, string>());
            var notificacion = new Notificacion { UsuarioId = usuarioId, Tipo = tipo, Titulo = titulo, Mensaje = mensaje };
            if (!d.ContainsKey("ruta")) d["ruta"] = $"notificacion/{notificacion.Id}";
            notificacion.Datos = JsonSerializer.Serialize(d);
            _db.Notificaciones.Add(notificacion);
        }

        /// <summary>A todas las cuentas activas del tutor del alumno.</summary>
        public async Task<int> ATutorDelAlumnoAsync(int alumnoId, string tipo, string titulo, string mensaje, IDictionary<string, string>? datos = null)
        {
            var tutorId = await _db.Alumnos.Where(a => a.Id == alumnoId).Select(a => (int?)a.TutorId).FirstOrDefaultAsync();
            if (tutorId is null) return 0;
            var cuentas = await _db.Users.Where(u => u.TutorId == tutorId && u.Activo).Select(u => u.Id).ToListAsync();
            foreach (var id in cuentas) AUsuario(id, tipo, titulo, mensaje, datos);
            return cuentas.Count;
        }

        /// <summary>
        /// Al personal con ese rol (Dirección o Enfermería) de UNA escuela: la de ese alumno.
        /// Así la enfermera de una escuela no recibe avisos de los alumnos de otra.
        /// </summary>
        public async Task<int> APersonalDeLaEscuelaAsync(string rol, int plantelId, string tipo, string titulo, string mensaje, IDictionary<string, string>? datos = null)
        {
            var delRol = (await _usuarios.GetUsersInRoleAsync(rol)).Where(u => u.Activo).Select(u => u.Id).ToList();
            var cuentas = await _db.UsuarioPlanteles.Where(x => x.PlantelId == plantelId && delRol.Contains(x.UsuarioId)).Select(x => x.UsuarioId).Distinct().ToListAsync();
            foreach (var id in cuentas) AUsuario(id, tipo, titulo, mensaje, datos);
            return cuentas.Count;
        }

        /// <summary>Al personal de la escuela del alumno.</summary>
        public async Task<int> APersonalDelAlumnoAsync(string rol, int alumnoId, string tipo, string titulo, string mensaje, IDictionary<string, string>? datos = null)
        {
            // Sin filtro: lo llama el tutor, que no tiene escuela asignada.
            var plantelId = await _db.Alumnos.IgnoreQueryFilters().Where(a => a.Id == alumnoId).Select(a => (int?)a.PlantelId).FirstOrDefaultAsync();
            return plantelId is null ? 0 : await APersonalDeLaEscuelaAsync(rol, plantelId.Value, tipo, titulo, mensaje, datos);
        }

        /// <summary>
        /// A los administradores globales (equipo del proyecto): reportes de fallas y asuntos del
        /// sistema. Si todavía no hay ninguno, a toda Dirección, como antes.
        /// </summary>
        public async Task<int> AAdministracionDelSistemaAsync(string tipo, string titulo, string mensaje, IDictionary<string, string>? datos = null)
        {
            var direccion = (await _usuarios.GetUsersInRoleAsync(Roles.Administrador)).Where(u => u.Activo).ToList();
            var globales = direccion.Where(u => u.AdministradorGlobal).ToList();
            var cuentas = globales.Count > 0 ? globales : direccion;
            foreach (var u in cuentas) AUsuario(u.Id, tipo, titulo, mensaje, datos);
            return cuentas.Count;
        }
    }
}
