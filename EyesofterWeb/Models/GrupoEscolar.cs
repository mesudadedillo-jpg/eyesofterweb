using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EyesofterWeb.Models
{
    /// <summary>
    /// Grupo de una escuela (p. ej. 3° B de primaria). Sus alumnos se mueven juntos: al
    /// "pasar de año" el grupo sube un grado y todos sus alumnos con él, sin editarlos uno por uno.
    /// En el último grado (6° de primaria, 3° de secundaria) el grupo egresa: se cierra y sus
    /// alumnos quedan marcados como egresados (su historial clínico se conserva).
    ///
    /// Las columnas nivel / grado / grupo de cada alumno se siguen llenando (copiadas del grupo)
    /// porque el servidor de la app y el escritorio agrupan por ellas.
    /// </summary>
    public class GrupoEscolar : IValidatableObject
    {
        public int Id { get; set; }

        [Display(Name = "Escuela")]
        public int PlantelId { get; set; }
        public Plantel? Plantel { get; set; }

        [Required(ErrorMessage = "Selecciona el nivel educativo.")]
        public NivelEducativo Nivel { get; set; }

        [Required(ErrorMessage = "El grado es obligatorio.")]
        [Range(1, 6, ErrorMessage = "El grado debe estar entre {1} y {2}.")]
        public int Grado { get; set; } = 1;

        [Required(ErrorMessage = "La letra del grupo es obligatoria.")]
        [RegularExpression(@"^[A-F]$", ErrorMessage = "El grupo debe ser una letra de la A a la F.")]
        [StringLength(1)]
        [Display(Name = "Grupo")]
        public string Letra { get; set; } = "A";

        [Required(ErrorMessage = "El ciclo escolar es obligatorio.")]
        [RegularExpression(@"^\d{4}-\d{4}$", ErrorMessage = "Usa el formato 2026-2027.")]
        [StringLength(9)]
        [Display(Name = "Ciclo escolar")]
        public string CicloEscolar { get; set; } = CicloActual();

        [StringLength(100, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
        [Display(Name = "Docente titular")]
        public string? Docente { get; set; }

        /// <summary>false = el grupo ya egresó (se conserva como historial).</summary>
        public bool Activo { get; set; } = true;

        /// <summary>Última vez que pasó de año; evita pasarlo dos veces por accidente.</summary>
        [Display(Name = "Pasó de año")]
        public DateTime? PromovidoEn { get; set; }

        public ICollection<Alumno> Alumnos { get; set; } = new List<Alumno>();

        [NotMapped]
        public string Nombre => $"{Grado}° {Letra} - {Nivel}";

        [NotMapped]
        public bool EsUltimoGrado => Grado >= GradoMaximo(Nivel);

        public static int GradoMaximo(NivelEducativo nivel) => nivel == NivelEducativo.Secundaria ? 3 : 6;

        /// <summary>Ciclo escolar vigente en una fecha. El ciclo nuevo empieza en agosto.</summary>
        public static string CicloActual(DateTime? fecha = null)
        {
            var f = fecha ?? DateTime.Today;
            var inicio = f.Month >= 8 ? f.Year : f.Year - 1;
            return $"{inicio}-{inicio + 1}";
        }

        /// <summary>"2025-2026" → "2026-2027". Si el texto no tiene el formato, se usa el ciclo actual.</summary>
        public static string CicloSiguiente(string ciclo)
        {
            if (ciclo is { Length: 9 } && int.TryParse(ciclo[..4], out var inicio))
                return $"{inicio + 1}-{inicio + 2}";
            return CicloActual();
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Grado > GradoMaximo(Nivel))
            {
                yield return new ValidationResult(
                    $"En {Nivel.ToString().ToLowerInvariant()} el grado solo puede ser de 1 a {GradoMaximo(Nivel)}.",
                    new[] { nameof(Grado) });
            }
        }
    }
}
