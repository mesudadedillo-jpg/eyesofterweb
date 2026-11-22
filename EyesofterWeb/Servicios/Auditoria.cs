using EyesofterWeb.Data;
using Microsoft.EntityFrameworkCore;
using EyesofterWeb.Models;
using EyesofterWeb.Seguridad;

namespace EyesofterWeb.Servicios
{
    /// <summary>
    /// Bitácora de auditoría (C-RC-07, C-RFNN-06). Se escribe en la misma transacción que el
    /// cambio cuando se llama antes de SaveChanges; para los accesos denegados se guarda sola.
    /// </summary>
    public class Auditoria
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<Auditoria> _log;
        private readonly AlcanceEscuela _alcance;

        public Auditoria(ApplicationDbContext db, IHttpContextAccessor http, ILogger<Auditoria> log, AlcanceEscuela alcance)
        {
            _db = db;
            _http = http;
            _log = log;
            _alcance = alcance;
        }

        /// <summary>
        /// Agrega el registro al contexto (se guarda con el próximo SaveChanges).
        /// <paramref name="plantelId"/>: escuela a la que se refiere. Si no se indica, se deduce del
        /// expediente (alumno, caso, tamizaje, comprobante, tutor, convenio, campaña); si no se puede y
        /// quien actúa tiene una sola escuela, se usa esa. Sin escuela, solo lo ve el administrador global
        /// (y la Dirección de quien actuó, si es un movimiento de su propia cuenta).
        /// <paramref name="usuario"/>: a nombre de quién (p. ej. el correo de la cuenta en un login fallido).
        /// </summary>
        public void Registrar(string accion, string? entidad = null, object? entidadId = null, string? detalle = null, bool exito = true,
                              int? plantelId = null, string? usuario = null)
        {
            var planteles = _alcance.EsPersonal ? _alcance.Planteles : Array.Empty<int>();
            var ctx = _http.HttpContext;
            plantelId ??= PlantelDe(entidad, entidadId?.ToString());
            _db.Auditoria.Add(new RegistroAuditoria
            {
                Usuario = usuario ?? ctx?.User.Identity?.Name,
                Accion = accion,
                Entidad = entidad,
                EntidadId = entidadId?.ToString(),
                Detalle = detalle is { Length: > 500 } ? detalle[..500] : detalle,
                Ip = ctx?.Connection.RemoteIpAddress?.ToString(),
                Origen = "web",
                Exito = exito,
                PlantelId = plantelId ?? (planteles.Length == 1 ? planteles[0] : null)
            });
        }

        /// <summary>
        /// La escuela del expediente al que se refiere el registro. Sin filtro por escuela: un acceso
        /// denegado a otra escuela debe registrarse con la escuela real.
        /// </summary>
        private int? PlantelDe(string? entidad, string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try
            {
                var n = int.TryParse(id, out var v) ? v : (int?)null;
                return entidad switch
                {
                    "Alumno" when n is not null => _db.Alumnos.IgnoreQueryFilters().Where(a => a.Id == n).Select(a => (int?)a.PlantelId).FirstOrDefault(),
                    "Caso" when n is not null => _db.Casos.IgnoreQueryFilters().Where(c => c.Id == n).Select(c => (int?)c.Alumno!.PlantelId).FirstOrDefault(),
                    "Tamizaje" when n is not null => _db.Tamizajes.IgnoreQueryFilters().Where(t => t.Id == n).Select(t => (int?)t.Alumno!.PlantelId).FirstOrDefault(),
                    "Tutor" when n is not null => _db.Tutores.IgnoreQueryFilters().Where(t => t.Id == n).Select(t => (int?)t.PlantelId).FirstOrDefault(),
                    "Campana" when n is not null => _db.Campanas.IgnoreQueryFilters().Where(c => c.Id == n).Select(c => (int?)c.PlantelId).FirstOrDefault(),
                    "Convenio" when n is not null => _db.Convenios.IgnoreQueryFilters().Where(c => c.Id == n).Select(c => (int?)c.PlantelId).FirstOrDefault(),
                    "Plantel" when n is not null => n,
                    "Comprobante" when Guid.TryParse(id, out var g) => _db.Comprobantes.IgnoreQueryFilters().Where(c => c.Id == g).Select(c => (int?)c.Caso!.Alumno!.PlantelId).FirstOrDefault(),
                    _ => null
                };
            }
            catch (Exception e)
            {
                _log.LogWarning(e, "No se pudo saber la escuela de {Entidad} {Id} para la bitácora", entidad, id);
                return null;
            }
        }

        /// <summary>Acceso denegado a un expediente: se guarda de inmediato y se avisa en el log.</summary>
        public async Task DenegadoAsync(string entidad, object? entidadId, string detalle)
        {
            Registrar("ACCESO_DENEGADO", entidad, entidadId, detalle, exito: false);
            _log.LogWarning("Acceso denegado: {Usuario} → {Entidad} {Id}: {Detalle}",
                _http.HttpContext?.User.Identity?.Name, entidad, entidadId, detalle);
            await _db.SaveChangesAsync();
        }
    }
}
