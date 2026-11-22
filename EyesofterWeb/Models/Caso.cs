using System.ComponentModel.DataAnnotations;
using EyesofterWeb.Validation;

namespace EyesofterWeb.Models
{
    /// <summary>Expediente de seguimiento visual de un alumno.</summary>
    public class Caso : IValidatableObject
    {
        public int Id { get; set; }

        [StringLength(20, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        public string Folio { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona un alumno.")]
        [Display(Name = "Alumno")]
        public int AlumnoId { get; set; }

        public Alumno? Alumno { get; set; }

        [Display(Name = "Clínica de canalización")]
        public int? ClinicaId { get; set; }

        public Clinica? Clinica { get; set; }

        [Required(ErrorMessage = "Selecciona el semáforo.")]
        [Display(Name = "Semáforo")]
        public Semaforo Semaforo { get; set; } = Semaforo.Amarillo;

        [Required]
        public EstadoCaso Estado { get; set; } = EstadoCaso.Abierto;

        [Required(ErrorMessage = "La fecha de detección es obligatoria.")]
        [DataType(DataType.Date)]
        [FechaNoFutura]
        [Display(Name = "fecha de detección")]
        public DateTime FechaDeteccion { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Captura la agudeza del ojo derecho.")]
        [RegularExpression(@"^20/\d{2,3}$", ErrorMessage = "Usa el formato Snellen, por ejemplo 20/40.")]
        [Cifrado]
        [Display(Name = "Agudeza ojo derecho")]
        [StringLength(7, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        public string AgudezaOjoDerecho { get; set; } = "20/20";

        [Required(ErrorMessage = "Captura la agudeza del ojo izquierdo.")]
        [RegularExpression(@"^20/\d{2,3}$", ErrorMessage = "Usa el formato Snellen, por ejemplo 20/40.")]
        [Cifrado]
        [Display(Name = "Agudeza ojo izquierdo")]
        [StringLength(7, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        public string AgudezaOjoIzquierdo { get; set; } = "20/20";

        [Cifrado]
        [StringLength(500, ErrorMessage = "Las observaciones no pueden pasar de {1} caracteres.")]
        [DataType(DataType.MultilineText)]
        public string? Observaciones { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha de cierre")]
        public DateTime? FechaCierre { get; set; }

        [StringLength(256, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        [Display(Name = "Registrado por")]
        public string? RegistradoPor { get; set; }

        /// <summary>Tamizaje que abrió el expediente (W-RFN-01), si vino del sistema de escritorio.</summary>
        public int? TamizajeId { get; set; }
        public Tamizaje? Tamizaje { get; set; }

        /// <summary>
        /// Clínica que el tutor escribió a mano porque no está en el directorio (M-RFN-09).
        /// Se trata como NO asociada (C-RFNN-07): solo se genera el resumen, sin QR.
        /// </summary>
        [StringLength(160)]
        [Display(Name = "Clínica indicada por el tutor")]
        public string? ClinicaDestinoManual { get; set; }

        /// <summary>Fecha de la primera atención médica (para el tiempo promedio, W-RFN-17).</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de atención")]
        public DateTime? FechaAtencion { get; set; }

        public ICollection<EventoCaso> Eventos { get; set; } = new List<EventoCaso>();
        public ICollection<Pase> Pases { get; set; } = new List<Pase>();
        public ICollection<Atencion> Atenciones { get; set; } = new List<Atencion>();
        public ICollection<Comprobante> Comprobantes { get; set; } = new List<Comprobante>();
        public ICollection<Cita> Citas { get; set; } = new List<Cita>();

        /// <summary>Regla de negocio: un caso en rojo debe canalizarse a una clínica.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Semaforo == Semaforo.Rojo && ClinicaId is null)
            {
                yield return new ValidationResult(
                    "Un caso en semáforo rojo debe canalizarse a una clínica.",
                    new[] { nameof(ClinicaId) });
            }

            if (FechaCierre.HasValue && FechaCierre.Value.Date < FechaDeteccion.Date)
            {
                yield return new ValidationResult(
                    "La fecha de cierre no puede ser anterior a la de detección.",
                    new[] { nameof(FechaCierre) });
            }
        }
    }
}
