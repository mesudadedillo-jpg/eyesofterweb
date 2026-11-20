using System.ComponentModel.DataAnnotations;

namespace EyesofterWeb.Models
{
    /// <summary>
    /// Notificación de la bandeja (web y app). Funciona como "bandeja de salida": quien la
    /// crea (el portal o el servidor de la app) la guarda con PendienteEnvio = true y el
    /// servidor Ktor la entrega por push o correo y la marca como enviada.
    /// Tipos: RESULTADO_TAMIZAJE, ALERTA_ENFERMERIA, AVISO, COMPROBANTE_RECIBIDO,
    /// COMPROBANTE_REVISADO, CITA, CASO_ACTUALIZADO.
    /// </summary>
    public class Notificacion
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, StringLength(450)]
        public string UsuarioId { get; set; } = string.Empty;

        [Required, StringLength(40)]
        public string Tipo { get; set; } = "AVISO";

        [Required, StringLength(120)]
        public string Titulo { get; set; } = string.Empty;

        [Required, StringLength(600)]
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>JSON con datos para navegar ({"ruta":"tamizaje/5",...}).</summary>
        [Required]
        public string Datos { get; set; } = "{}";

        public DateTime CreadaEn { get; set; } = DateTime.UtcNow;

        /// <summary>Confirmación de lectura (C-RFN-07).</summary>
        public DateTime? LeidaEn { get; set; }

        public int Recordatorios { get; set; }
        public DateTime UltimoAvisoEn { get; set; } = DateTime.UtcNow;

        /// <summary>true = el servidor Ktor aún no la entrega por push/correo.</summary>
        public bool PendienteEnvio { get; set; } = true;

        /// <summary>El envío pendiente es un recordatorio (se antepone "Recordatorio:").</summary>
        public bool EnvioEsRecordatorio { get; set; }

        public ICollection<EntregaNotificacion> Entregas { get; set; } = new List<EntregaNotificacion>();
    }

    public class EntregaNotificacion
    {
        public long Id { get; set; }
        public Guid NotificacionId { get; set; }
        public Notificacion? Notificacion { get; set; }

        [Required, StringLength(20)]
        public string Canal { get; set; } = string.Empty;

        public bool Exito { get; set; }
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        [StringLength(300)]
        public string? Detalle { get; set; }
    }

    /// <summary>Token de Firebase de un teléfono (lo registra la app móvil).</summary>
    public class Dispositivo
    {
        [Key, StringLength(4096)]
        public string Token { get; set; } = string.Empty;

        [Required, StringLength(450)]
        public string UsuarioId { get; set; } = string.Empty;

        [Required, StringLength(10)]
        public string Plataforma { get; set; } = "ANDROID";

        public DateTime RegistradoEn { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Aceptación del aviso de privacidad (C-RC-08, LFPDPPP). Se guarda la versión aceptada:
    /// si el aviso cambia, se vuelve a pedir.
    /// </summary>
    public class Consentimiento
    {
        public long Id { get; set; }

        [Required, StringLength(450)]
        public string UsuarioId { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string VersionAviso { get; set; } = string.Empty;

        public DateTime AceptadoEn { get; set; } = DateTime.UtcNow;

        /// <summary>"web" o "app".</summary>
        [Required, StringLength(10)]
        public string Medio { get; set; } = "web";

        [StringLength(64)]
        public string? Ip { get; set; }
    }

    /// <summary>
    /// Bitácora de auditoría (C-RC-07): accesos y cambios sobre expedientes, y accesos
    /// denegados (C-RFNN-06). Solo se agregan filas; nunca se editan.
    /// </summary>
    public class RegistroAuditoria
    {
        public long Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        [StringLength(256)]
        public string? Usuario { get; set; }

        [Required, StringLength(60)]
        public string Accion { get; set; } = string.Empty;

        [StringLength(40)]
        public string? Entidad { get; set; }

        [StringLength(60)]
        public string? EntidadId { get; set; }

        [StringLength(500)]
        public string? Detalle { get; set; }

        [StringLength(64)]
        public string? Ip { get; set; }

        /// <summary>"web", "app" o "escritorio".</summary>
        [StringLength(12)]
        public string Origen { get; set; } = "web";

        public bool Exito { get; set; } = true;
    }

    /// <summary>Canal para reportar fallas o dudas durante el piloto (C-RS-04).</summary>
    public class ReporteFalla
    {
        public int Id { get; set; }

        [StringLength(450)]
        public string? UsuarioId { get; set; }

        [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
        [StringLength(256)]
        [Display(Name = "Correo para responderte")]
        public string? Correo { get; set; }

        [Required(ErrorMessage = "Elige el tipo.")]
        [StringLength(10)]
        public string Tipo { get; set; } = "Falla";

        [Required(ErrorMessage = "Cuéntanos qué pasó.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Escribe entre {2} y {1} caracteres.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Descripción")]
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>"web", "android", "ios".</summary>
        [StringLength(20)]
        public string Plataforma { get; set; } = "web";

        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
        public EstadoReporte Estado { get; set; } = EstadoReporte.Nuevo;

        [StringLength(1000)]
        public string? Respuesta { get; set; }
    }

    /// <summary>Marca una propiedad para que se guarde cifrada con AES-256-GCM (C-RC-04).</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class CifradoAttribute : Attribute { }
}
