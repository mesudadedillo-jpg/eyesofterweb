using System.ComponentModel.DataAnnotations;

namespace EyesofterWeb.Models
{
    /// <summary>Entrada en la línea de tiempo de un caso (tamizaje, aviso, cita, comprobante, cierre).</summary>
    public class EventoCaso
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Caso")]
        public int CasoId { get; set; }

        public Caso? Caso { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de evento.")]
        [Display(Name = "Tipo de evento")]
        public TipoEvento Tipo { get; set; } = TipoEvento.Seguimiento;

        [Required(ErrorMessage = "La fecha es obligatoria.")]
        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Describe el evento.")]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "La descripción debe tener entre {2} y {1} caracteres.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Descripción")]
        public string Descripcion { get; set; } = string.Empty;

        [StringLength(256, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        [Display(Name = "Registrado por")]
        public string? RegistradoPor { get; set; }
    }
}
