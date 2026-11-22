using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EyesofterWeb.Models
{
    /// <summary>
    /// Usuario de ASP.NET Core Identity. Es la ÚNICA tabla de cuentas del ecosistema:
    /// el servidor de la app móvil valida contra ella (mismo correo y contraseña en web y app).
    /// Según el rol, la cuenta se vincula con el tutor, el alumno o la clínica que representa.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(120, ErrorMessage = "El campo {0} no puede pasar de {1} caracteres.")]
        public string NombreCompleto { get; set; } = string.Empty;

        /// <summary>Rol Tutor: a qué tutor representa (C-RC-06: solo ve a sus alumnos).</summary>
        public int? TutorId { get; set; }
        public Tutor? Tutor { get; set; }

        /// <summary>Rol Alumno.</summary>
        public int? AlumnoId { get; set; }
        public Alumno? Alumno { get; set; }

        /// <summary>Rol Especialista: clínica donde trabaja (W-RFNN-04: solo asociadas y activas escanean QR).</summary>
        public int? ClinicaId { get; set; }
        public Clinica? Clinica { get; set; }

        /// <summary>false = cuenta desactivada (no puede entrar ni en la web ni en la app).</summary>
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Solo para Dirección: administra TODO el sistema (todas las escuelas, altas de escuelas).
        /// Es para el equipo del proyecto, no para el director de una escuela. Una cuenta de
        /// Dirección o Enfermería sin esta marca solo ve las escuelas de <see cref="Planteles"/>.
        /// </summary>
        [Display(Name = "Administrador global (todas las escuelas)")]
        public bool AdministradorGlobal { get; set; }

        /// <summary>Escuelas a las que pertenece el personal escolar (Dirección y Enfermería).</summary>
        public ICollection<UsuarioPlantel> Planteles { get; set; } = new List<UsuarioPlantel>();
    }
}
