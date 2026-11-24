using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EyesofterWeb.Models
{
    /// <summary>
    /// Escuela (C-RE-01: cada institución por separado). Todo lo de un alumno (tamizajes,
    /// casos, comprobantes, citas) pertenece a la escuela del alumno, y el personal escolar
    /// solo ve las escuelas que tiene asignadas (<see cref="UsuarioPlantel"/>).
    /// </summary>
    public class Plantel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(20)]
        [RegularExpression(@"^[0-9A-Z]{5,20}$", ErrorMessage = "La clave debe tener de 5 a 20 letras mayúsculas o números.")]
        [Display(Name = "Clave del centro de trabajo")]
        public string? Clave { get; set; }

        [Display(Name = "Activa")]
        public bool Activo { get; set; } = true;
    }

    /// <summary>Qué escuelas puede ver una cuenta de Dirección o Enfermería. Una persona puede cubrir varias.</summary>
    public class UsuarioPlantel
    {
        [Required, StringLength(450)]
        public string UsuarioId { get; set; } = string.Empty;
        public ApplicationUser? Usuario { get; set; }

        public int PlantelId { get; set; }
        public Plantel? Plantel { get; set; }
    }

    /// <summary>
    /// Convenio de una escuela con una clínica: el beneficio (p. ej. descuento) que la clínica da
    /// a los alumnos de ESA escuela. Se muestra en el pase médico y en el directorio de la app;
    /// el pase con QR comprueba que el alumno es de la escuela. No se comparte ningún dato del
    /// alumno ni del tutor con la clínica por tener convenio.
    /// </summary>
    public class Convenio : IValidatableObject
    {
        public int Id { get; set; }

        [Display(Name = "Escuela")]
        public int PlantelId { get; set; }
        public Plantel? Plantel { get; set; }

        [Display(Name = "Clínica")]
        public int ClinicaId { get; set; }
        public Clinica? Clinica { get; set; }

        [Range(0, 100, ErrorMessage = "El descuento debe estar entre {1} y {2} %.")]
        [Display(Name = "Descuento (%)")]
        public int Descuento { get; set; }

        [Required(ErrorMessage = "Describe el beneficio.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Escribe entre {2} y {1} caracteres.")]
        [Display(Name = "Beneficio para los alumnos")]
        public string Beneficio { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Vigente hasta")]
        public DateTime? VigenteHasta { get; set; }

        public bool Activo { get; set; } = true;

        [NotMapped]
        public bool Vigente => Activo && (VigenteHasta is null || VigenteHasta.Value.Date >= DateTime.Today);

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (VigenteHasta is not null && VigenteHasta.Value.Date < DateTime.Today && Activo)
                yield return new ValidationResult("La fecha de vigencia ya pasó. Cámbiala o desactiva el convenio.", new[] { nameof(VigenteHasta) });
        }
    }

    /// <summary>Campaña de tamizaje dentro de un ciclo escolar (W-RFN-16: comparar entre ciclos y campañas).</summary>
    public class Campana
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(80)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El ciclo escolar es obligatorio.")]
        [RegularExpression(@"^\d{4}-\d{4}$", ErrorMessage = "Usa el formato 2026-2027.")]
        [StringLength(9)]
        [Display(Name = "Ciclo escolar")]
        public string CicloEscolar { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Inicio")]
        public DateTime FechaInicio { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Fin")]
        public DateTime? FechaFin { get; set; }

        public int PlantelId { get; set; } = 1;
        public Plantel? Plantel { get; set; }

        public ICollection<Tamizaje> Tamizajes { get; set; } = new List<Tamizaje>();
    }

    /// <summary>
    /// Resultado de tamizaje tal como lo manda el sistema de escritorio (C-RFN-02), sin
    /// alteraciones (C-RC-11). Hay uno por alumno evaluado; si sale amarillo o rojo se
    /// abre automáticamente un expediente (W-RFN-01).
    /// </summary>
    public class Tamizaje
    {
        public int Id { get; set; }

        /// <summary>Identificador del tamizaje en el sistema de escritorio. Evita duplicados si reintenta.</summary>
        [Required, StringLength(40)]
        public string Folio { get; set; } = string.Empty;

        public int AlumnoId { get; set; }
        public Alumno? Alumno { get; set; }

        public int? CampanaId { get; set; }
        public Campana? Campana { get; set; }

        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; }

        public Semaforo Semaforo { get; set; }

        /// <summary>Cifrado en la base (C-RC-04).</summary>
        [Cifrado, StringLength(7)]
        [Display(Name = "Agudeza ojo derecho")]
        public string? AgudezaOjoDerecho { get; set; }

        [Cifrado, StringLength(7)]
        [Display(Name = "Agudeza ojo izquierdo")]
        public string? AgudezaOjoIzquierdo { get; set; }

        [Cifrado, StringLength(500)]
        public string? Observaciones { get; set; }

        /// <summary>"escritorio" o "web".</summary>
        [StringLength(20)]
        public string Origen { get; set; } = "escritorio";

        public DateTime RecibidoEn { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Pase médico digital con QR único por caso (C-RFN-05). Solo para clínicas asociadas
    /// (C-RFN-04). El código es aleatorio (128 bits), expira y se usa una sola vez (W-RFNN-03).
    /// </summary>
    public class Pase
    {
        public int Id { get; set; }

        public int CasoId { get; set; }
        public Caso? Caso { get; set; }

        public int ClinicaId { get; set; }
        public Clinica? Clinica { get; set; }

        /// <summary>Lo que lleva el QR. 26 caracteres en base32, imposible de adivinar.</summary>
        [Required, StringLength(32)]
        public string Codigo { get; set; } = string.Empty;

        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
        public DateTime ExpiraEn { get; set; }
        public EstadoPase Estado { get; set; } = EstadoPase.Vigente;
        public DateTime? UsadoEn { get; set; }

        [StringLength(256)]
        public string? UsadoPor { get; set; }

        [NotMapped]
        public bool EstaVigente => Estado == EstadoPase.Vigente && ExpiraEn > DateTime.UtcNow;
    }

    /// <summary>Atención médica brindada al alumno (W-RFN-10 manual, W-RFN-11 por QR).</summary>
    public class Atencion
    {
        public int Id { get; set; }

        public int CasoId { get; set; }
        public Caso? Caso { get; set; }

        [Display(Name = "Clínica")]
        public int? ClinicaId { get; set; }
        public Clinica? Clinica { get; set; }

        [StringLength(160)]
        [Display(Name = "Clínica (no registrada)")]
        public string? ClinicaNombre { get; set; }

        [Required(ErrorMessage = "La fecha de atención es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de atención")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        /// <summary>Lo escribe el especialista; cifrado (C-RC-04). El tutor no lo ve como diagnóstico.</summary>
        [Cifrado]
        [Required(ErrorMessage = "Describe la valoración.")]
        [StringLength(1000, MinimumLength = 5, ErrorMessage = "Escribe entre {2} y {1} caracteres.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Valoración del especialista")]
        public string Valoracion { get; set; } = string.Empty;

        [Cifrado]
        [StringLength(1000)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Indicaciones para la familia")]
        public string? Indicaciones { get; set; }

        [Display(Name = "¿Se indicó corrección óptica (lentes)?")]
        public bool CorreccionOptica { get; set; }

        public OrigenAtencion Origen { get; set; }

        public int? PaseId { get; set; }
        public Pase? Pase { get; set; }

        public Guid? ComprobanteId { get; set; }
        public Comprobante? Comprobante { get; set; }

        [StringLength(256)]
        public string? RegistradoPor { get; set; }

        public DateTime RegistradoEn { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Receta o comprobante de lentes que sube el tutor (M-RFN-05). El archivo va cifrado (C-RC-04).</summary>
    public class Comprobante
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int CasoId { get; set; }
        public Caso? Caso { get; set; }

        [Required, StringLength(450)]
        public string SubidoPorId { get; set; } = string.Empty;

        public TipoComprobante Tipo { get; set; }

        [Required, StringLength(40)]
        public string ContentType { get; set; } = string.Empty;

        public int TamanoBytes { get; set; }

        [Cifrado]
        public byte[] Contenido { get; set; } = Array.Empty<byte>();

        public DateTime SubidoEn { get; set; } = DateTime.UtcNow;
        public EstadoComprobante Estado { get; set; } = EstadoComprobante.Pendiente;

        [StringLength(256)]
        public string? RevisadoPor { get; set; }
        public DateTime? RevisadoEn { get; set; }

        [StringLength(300)]
        [Display(Name = "Motivo del rechazo")]
        public string? MotivoRechazo { get; set; }
    }

    /// <summary>Cita solicitada por el tutor a una clínica del directorio (M-RFN-08).</summary>
    public class Cita
    {
        public int Id { get; set; }

        public int CasoId { get; set; }
        public Caso? Caso { get; set; }

        public int? ClinicaId { get; set; }
        public Clinica? Clinica { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha preferida")]
        public DateTime FechaPreferida { get; set; }

        [StringLength(20)]
        [Display(Name = "Horario preferido")]
        public string? Horario { get; set; }

        [StringLength(300)]
        public string? Comentario { get; set; }

        public EstadoCita Estado { get; set; } = EstadoCita.Solicitada;

        [Required, StringLength(450)]
        public string SolicitadaPorId { get; set; } = string.Empty;

        public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
    }
}
