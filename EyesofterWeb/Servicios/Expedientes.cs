using System.Security.Cryptography;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EyesofterWeb.Servicios
{
    /// <summary>
    /// Reglas del expediente que usan varias pantallas: línea de tiempo, destino del alumno,
    /// pase médico con QR, registro de atenciones y cierre del caso.
    ///
    /// El servidor de la app (eyesofter-backend) aplica las MISMAS reglas para lo que llega desde
    /// el teléfono y el escritorio. Si cambias una aquí, cámbiala también allá.
    /// </summary>
    public class Expedientes
    {
        private readonly ApplicationDbContext _db;
        private readonly Notificador _notificador;
        private readonly Auditoria _auditoria;
        private readonly OpcionesEyesofter _opciones;

        public Expedientes(ApplicationDbContext db, Notificador notificador, Auditoria auditoria, IOptions<OpcionesEyesofter> opciones)
        {
            _db = db;
            _notificador = notificador;
            _auditoria = auditoria;
            _opciones = opciones.Value;
        }

        public void Evento(int casoId, TipoEvento tipo, string descripcion, string? usuario, DateTime? fecha = null) =>
            _db.EventosCaso.Add(new EventoCaso
            {
                CasoId = casoId,
                Tipo = tipo,
                Fecha = (fecha ?? DateTime.Today).Date,
                Descripcion = descripcion.Length > 500 ? descripcion[..500] : descripcion,
                RegistradoPor = usuario
            });

        public async Task<string> GenerarFolioAsync()
        {
            var prefijo = $"EYS-{DateTime.Today.Year}-";
            var folios = await _db.Casos.Where(c => c.Folio.StartsWith(prefijo)).Select(c => c.Folio).ToListAsync();
            var siguiente = folios.Select(f => int.TryParse(f[prefijo.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
            return $"{prefijo}{siguiente:D4}";
        }

        /// <summary>
        /// W-RFN-04: un caso solo se cierra cuando hay corrección óptica comprobada, es decir,
        /// una atención registrada o un comprobante (receta o lentes) validado por Enfermería.
        /// </summary>
        public async Task<(bool Puede, string? Motivo)> PuedeCerrarAsync(int casoId)
        {
            var atencion = await _db.Atenciones.AnyAsync(a => a.CasoId == casoId);
            var comprobante = await _db.Comprobantes.AnyAsync(c => c.CasoId == casoId && c.Estado == EstadoComprobante.Aprobado);
            return atencion || comprobante
                ? (true, null)
                : (false, "Para cerrar el caso debe existir una atención registrada o un comprobante validado (corrección óptica comprobada).");
        }

        public static string NuevoCodigoPase()
        {
            // 16 bytes aleatorios en base32 (sin letras confundibles al escribirlo a mano).
            const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = RandomNumberGenerator.GetBytes(16);
            var chars = new char[26];
            for (var i = 0; i < chars.Length; i++)
            {
                var bit = i * 5;
                var valor = (bytes[bit / 8] << 8 | (bit / 8 + 1 < bytes.Length ? bytes[bit / 8 + 1] : 0)) >> (11 - bit % 8) & 31;
                chars[i] = alfabeto[valor];
            }
            return new string(chars);
        }

        /// <summary>Normaliza lo que escribe a mano el especialista: sin espacios ni guiones, en mayúsculas.</summary>
        public static string NormalizarCodigo(string codigo) =>
            new string(codigo.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        /// <summary>
        /// M-RFN-09 / C-RFN-04 / C-RFNN-07: guarda a dónde acudirá el alumno.
        /// Clínica registrada, asociada y activa → pase con QR (C-RFN-05).
        /// Cualquier otro caso → solo el resumen del tamizaje, sin QR (C-RFN-06).
        /// Devuelve el pase creado o null si corresponde resumen.
        /// </summary>
        public async Task<Pase?> AsignarDestinoAsync(Caso caso, int? clinicaId, string? nombreManual, string? usuario)
        {
            Clinica? clinica = clinicaId is null ? null : await _db.Clinicas.FirstOrDefaultAsync(c => c.Id == clinicaId);
            caso.ClinicaId = clinica?.Id;
            caso.ClinicaDestinoManual = clinica is null ? nombreManual?.Trim() : null;
            if (caso.Estado == EstadoCaso.Abierto) caso.Estado = EstadoCaso.EnSeguimiento;

            // Un pase anterior sin usar queda cancelado: solo puede haber uno vigente.
            var anteriores = await _db.Pases.Where(p => p.CasoId == caso.Id && p.Estado == EstadoPase.Vigente).ToListAsync();
            anteriores.ForEach(p => p.Estado = EstadoPase.Cancelado);

            var asociada = clinica is { EsAsociada: true, Activa: true };
            Pase? pase = null;
            if (asociada)
            {
                pase = new Pase
                {
                    CasoId = caso.Id,
                    ClinicaId = clinica!.Id,
                    Codigo = NuevoCodigoPase(),
                    ExpiraEn = DateTime.UtcNow.AddDays(_opciones.DiasVigenciaPase)
                };
                _db.Pases.Add(pase);
            }
            var nombre = clinica?.Nombre ?? caso.ClinicaDestinoManual ?? "sin especificar";
            Evento(caso.Id, TipoEvento.Destino,
                asociada ? $"Destino: {nombre} (asociada). Se generó el pase médico con QR."
                         : $"Destino: {nombre} (no asociada{(clinica is null ? ", no registrada" : "")}). Se entrega el resumen del tamizaje, sin QR.",
                usuario);
            _auditoria.Registrar("ASIGNAR_DESTINO", "Caso", caso.Id, nombre);
            return pase;
        }

        public record ResultadoPase(bool Valido, string Mensaje, Pase? Pase);

        /// <summary>
        /// W-RFNN-03 y W-RFNN-04: el QR solo sirve a un especialista de la clínica asociada y activa
        /// a la que va dirigido, una sola vez y antes de que expire.
        /// </summary>
        public async Task<ResultadoPase> ValidarPaseAsync(string codigo, ApplicationUser especialista)
        {
            var clinica = especialista.ClinicaId is null ? null : await _db.Clinicas.FindAsync(especialista.ClinicaId);
            if (clinica is null || !clinica.EsAsociada || !clinica.Activa)
                return new(false, "Tu cuenta no pertenece a una clínica asociada y activa. Solo esas clínicas pueden leer pases.", null);

            var pase = await _db.Pases
                .Include(p => p.Clinica)
                .Include(p => p.Caso).ThenInclude(c => c!.Alumno)
                .Include(p => p.Caso).ThenInclude(c => c!.Tamizaje)
                .FirstOrDefaultAsync(p => p.Codigo == NormalizarCodigo(codigo));
            if (pase is null) return new(false, "El código no corresponde a ningún pase. Revisa que esté bien escrito.", null);
            if (pase.Estado == EstadoPase.Usado) return new(false, $"Este pase ya se usó el {pase.UsadoEn!.Value.ToLocalTime():dd/MM/yyyy}.", null);
            if (pase.Estado == EstadoPase.Cancelado) return new(false, "Este pase fue reemplazado por uno nuevo. Pide a la familia el pase vigente.", null);
            if (pase.ExpiraEn <= DateTime.UtcNow) return new(false, $"Este pase expiró el {pase.ExpiraEn.ToLocalTime():dd/MM/yyyy}. La familia puede generar uno nuevo.", null);
            if (pase.ClinicaId != clinica.Id) return new(false, $"Este pase es para {pase.Clinica?.Nombre}, no para tu clínica.", null);
            return new(true, "Pase válido.", pase);
        }

        /// <summary>W-RFN-10 / W-RFN-11: registra la atención, la agrega a la línea de tiempo y avisa al tutor.</summary>
        public async Task<Atencion> RegistrarAtencionAsync(Caso caso, Atencion atencion, string? usuario, Pase? pase = null)
        {
            atencion.CasoId = caso.Id;
            atencion.RegistradoPor = usuario;
            atencion.PaseId = pase?.Id;
            _db.Atenciones.Add(atencion);
            if (pase is not null)
            {
                pase.Estado = EstadoPase.Usado;
                pase.UsadoEn = DateTime.UtcNow;
                pase.UsadoPor = usuario;
            }
            if (caso.FechaAtencion is null || atencion.Fecha < caso.FechaAtencion) caso.FechaAtencion = atencion.Fecha.Date;
            if (caso.Estado == EstadoCaso.Abierto) caso.Estado = EstadoCaso.EnSeguimiento;

            var lugar = atencion.ClinicaNombre ?? (await _db.Clinicas.FindAsync(atencion.ClinicaId))?.Nombre ?? "clínica";
            Evento(caso.Id, TipoEvento.Atencion,
                $"Atención en {lugar} ({(atencion.Origen == OrigenAtencion.PaseQr ? "pase con QR" : "registro manual")})" +
                (atencion.CorreccionOptica ? ". Se indicó corrección óptica." : "."), usuario, atencion.Fecha);
            _auditoria.Registrar("REGISTRAR_ATENCION", "Caso", caso.Id, lugar);

            var alumno = caso.Alumno ?? await _db.Alumnos.FindAsync(caso.AlumnoId);
            await _notificador.ATutorDelAlumnoAsync(caso.AlumnoId, "CASO_ACTUALIZADO", "Se registró la atención",
                $"Quedó registrada la atención de {alumno?.Nombre} en {lugar}." +
                (string.IsNullOrWhiteSpace(atencion.Indicaciones) ? "" : " Revisa las indicaciones en la app o el portal."),
                new Dictionary<string, string> { ["ruta"] = $"tamizaje/{caso.TamizajeId}", ["casoId"] = caso.Id.ToString() });
            return atencion;
        }
    }
}
