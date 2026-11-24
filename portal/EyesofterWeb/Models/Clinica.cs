using System.ComponentModel.DataAnnotations;

namespace EyesofterWeb.Models
{
    /// <summary>Óptica, consultorio u hospital al que se canaliza a los alumnos.</summary>
    public class Clinica
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona el tipo de clínica.")]
        public TipoClinica Tipo { get; set; }

        [Required(ErrorMessage = "La dirección es obligatoria.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "La dirección debe tener entre {2} y {1} caracteres.")]
        [Display(Name = "Dirección")]
        public string Direccion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "El teléfono debe tener exactamente 10 dígitos, sin espacios.")]
        [StringLength(10, ErrorMessage = "El teléfono debe tener exactamente 10 dígitos, sin espacios.")]
        [Display(Name = "Teléfono")]
        [DataType(DataType.PhoneNumber)]
        public string Telefono { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Escribe un correo electrónico válido.")]
        [StringLength(120, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        [Display(Name = "Correo electrónico")]
        public string? Correo { get; set; }

        [StringLength(80, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        public string Ciudad { get; set; } = "Tulancingo";

        [RegularExpression(@"^\d{5}$", ErrorMessage = "El código postal debe tener 5 dígitos.")]
        [StringLength(5)]
        [Display(Name = "Código postal")]
        public string? CodigoPostal { get; set; }

        /// <summary>Ubicación geográfica (W-RFN-12): la usa el mapa de la app móvil.</summary>
        [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        [Display(Name = "Latitud")]
        public double? Latitud { get; set; }

        [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        [Display(Name = "Longitud")]
        public double? Longitud { get; set; }

        [StringLength(120, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        [Display(Name = "Horario de atención")]
        public string? Horario { get; set; }

        /// <summary>Asociada al programa (W-RFN-13): sus especialistas escanean el pase con QR.</summary>
        [Display(Name = "¿Es asociada?")]
        public bool EsAsociada { get; set; }

        /// <summary>Desactivar en vez de borrar (W-RFN-12): conserva el historial de casos.</summary>
        [Display(Name = "Activa")]
        public bool Activa { get; set; } = true;

        public ICollection<Caso> Casos { get; set; } = new List<Caso>();
    }
}
