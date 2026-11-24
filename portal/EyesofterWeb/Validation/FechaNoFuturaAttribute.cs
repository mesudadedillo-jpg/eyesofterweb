using System.ComponentModel.DataAnnotations;

namespace EyesofterWeb.Validation
{
    /// <summary>
    /// Data Annotation personalizada: la fecha no puede ser posterior al día de hoy.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class FechaNoFuturaAttribute : ValidationAttribute
    {
        public FechaNoFuturaAttribute()
            : base("La {0} no puede ser una fecha futura.")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is DateTime fecha && fecha.Date > DateTime.Today)
            {
                return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
            }
            return ValidationResult.Success;
        }
    }
}
