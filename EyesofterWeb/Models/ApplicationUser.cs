using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EyesofterWeb.Models
{
    /// <summary>Usuario de ASP.NET Core Identity extendido con el nombre completo.</summary>
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(120, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        public string NombreCompleto { get; set; } = string.Empty;
    }
}
