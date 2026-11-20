using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EyesofterWeb.Validation;

namespace EyesofterWeb.Models
{
    /// <summary>Alumno de primaria o secundaria que participa en el tamizaje visual.</summary>
    public class Alumno : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La matrícula es obligatoria.")]
        [RegularExpression(@"^[A-Z0-9]{6,12}$", ErrorMessage = "La matrícula debe tener de 6 a 12 letras mayúsculas o números.")]
        [StringLength(12, ErrorMessage = "La matrícula no puede pasar de {1} caracteres.")]
        [Display(Name = "Matrícula")]
        public string Matricula { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los apellidos son obligatorios.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre {2} y {1} caracteres.")]
        public string Apellidos { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
        [DataType(DataType.Date)]
        [FechaNoFutura]
        [Display(Name = "fecha de nacimiento")]
        public DateTime FechaNacimiento { get; set; } = DateTime.Today.AddYears(-8);

        [Required(ErrorMessage = "Selecciona el nivel educativo.")]
        [Display(Name = "Nivel")]
        public NivelEducativo Nivel { get; set; }

        [Required(ErrorMessage = "El grado es obligatorio.")]
        [Range(1, 6, ErrorMessage = "El grado debe estar entre {1} y {2}.")]
        public int Grado { get; set; } = 1;

        [Required(ErrorMessage = "El grupo es obligatorio.")]
        [RegularExpression(@"^[A-F]$", ErrorMessage = "El grupo debe ser una letra de la A a la F.")]
        [StringLength(1, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        public string Grupo { get; set; } = "A";

        [Display(Name = "¿Usa lentes?")]
        public bool UsaLentes { get; set; }

        [Required(ErrorMessage = "Selecciona un tutor.")]
        [Display(Name = "Tutor")]
        public int TutorId { get; set; }

        public Tutor? Tutor { get; set; }

        public ICollection<Caso> Casos { get; set; } = new List<Caso>();

        [NotMapped]
        [Display(Name = "Alumno")]
        public string NombreCompleto => $"{Nombre} {Apellidos}";

        [NotMapped]
        public string GradoGrupo => $"{Grado}° {Grupo} - {Nivel}";

        /// <summary>Reglas de negocio que involucran varios campos a la vez.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Nivel == NivelEducativo.Secundaria && Grado > 3)
            {
                yield return new ValidationResult(
                    "En secundaria el grado solo puede ser 1, 2 o 3.",
                    new[] { nameof(Grado) });
            }

            var edad = DateTime.Today.Year - FechaNacimiento.Year;
            if (FechaNacimiento.Date > DateTime.Today.AddYears(-edad)) edad--;
            if (edad < 5 || edad > 17)
            {
                yield return new ValidationResult(
                    "La edad del alumno debe estar entre 5 y 17 años.",
                    new[] { nameof(FechaNacimiento) });
            }
        }
    }
}
