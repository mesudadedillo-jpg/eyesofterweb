using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EyesofterWeb.Models
{
    /// <summary>Padre, madre o tutor legal responsable de uno o más alumnos.</summary>
    public class Tutor
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los apellidos son obligatorios.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre {2} y {1} caracteres.")]
        public string Apellidos { get; set; } = string.Empty;

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

        [Required(ErrorMessage = "Indica el parentesco.")]
        [StringLength(30, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        public string Parentesco { get; set; } = "Madre";

        public ICollection<Alumno> Alumnos { get; set; } = new List<Alumno>();

        [NotMapped]
        [Display(Name = "Tutor")]
        public string NombreCompleto => $"{Nombre} {Apellidos}";
    }
}
