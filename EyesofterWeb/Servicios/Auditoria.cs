using EyesofterWeb.Data;
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
        /// <paramref name="plantelId"/>: escuela a la que se refiere; si no se indica y quien actúa
        /// tiene una sola escuela, se usa esa. Sin escuela, solo lo ve el administrador global.
        /// </summary>
        public void Registrar(string accion, string? entidad = null, object? entidadId = null, string? detalle = null, bool exito = true, int? plantelId = null)
        {
            var planteles = _alcance.EsPersonal ? _alcance.Planteles : Array.Empty<int>();
            var ctx = _http.HttpContext;
            _db.Auditoria.Add(new RegistroAuditoria
            {
                Usuario = ctx?.User.Identity?.Name,
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
