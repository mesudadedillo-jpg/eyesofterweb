using System.ComponentModel.DataAnnotations;

namespace EyesofterWeb.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Escribe un correo electrónico válido.")]
        [Display(Name = "Correo electrónico")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }

        /// <summary>El portal tiene configurado "Iniciar sesión con Google".</summary>
        public bool GoogleDisponible { get; set; }
    }

    /// <summary>Mi cuenta → Google.</summary>
    public class CuentaGoogleViewModel
    {
        public bool Disponible { get; set; }
        public bool Vinculada { get; set; }
        public string Correo { get; set; } = string.Empty;
    }

    public class LoginDosPasosViewModel
    {
        [Required(ErrorMessage = "Escribe el código.")]
        [StringLength(20)]
        [Display(Name = "Código de verificación")]
        public string Codigo { get; set; } = string.Empty;

        /// <summary>true = es uno de los códigos de respaldo (si perdiste el teléfono).</summary>
        public bool EsCodigoDeRespaldo { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class DosPasosViewModel
    {
        [Display(Name = "Código de 6 dígitos de tu app")]
        public string? Codigo { get; set; }
        public string ClaveLegible { get; set; } = string.Empty;
        public string QrDataUri { get; set; } = string.Empty;
        public bool Obligatoria { get; set; }
    }

    public class OlvideViewModel
    {
        /// <summary>Correo de la cuenta o celular de 10 dígitos (el del tutor que registró la escuela).</summary>
        [Required(ErrorMessage = "Escribe tu correo o tu celular.")]
        [StringLength(120, ErrorMessage = "Es demasiado largo.")]
        [Display(Name = "Correo electrónico o celular")]
        public string Cuenta { get; set; } = string.Empty;
    }

    /// <summary>Paso 2 de "¿Olvidaste tu contraseña?": el código que llegó y la contraseña nueva.</summary>
    public class IngresarCodigoViewModel
    {
        [Required(ErrorMessage = "Escribe tu correo o tu celular.")]
        [StringLength(120, ErrorMessage = "Es demasiado largo.")]
        [Display(Name = "Correo electrónico o celular")]
        public string Cuenta { get; set; } = string.Empty;

        [Required(ErrorMessage = "Escribe el código que te enviamos.")]
        [RegularExpression(@"^\s*\d{3}\s?\d{3}\s*$", ErrorMessage = "El código son 6 números.")]
        [Display(Name = "Código de 6 dígitos")]
        public string Codigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(64, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos {2} caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirma la contraseña.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarPassword { get; set; } = string.Empty;

        /// <summary>Se escribió un celular (para decir dónde buscar el código).</summary>
        public bool PorCelular { get; set; }
        /// <summary>Hay proveedor de SMS; si no, el código de quien escribe su celular va a su correo.</summary>
        public bool SmsDisponible { get; set; }
    }

    public class RestablecerViewModel
    {
        [Required] public string Correo { get; set; } = string.Empty;
        [Required] public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(64, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos {2} caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirma la contraseña.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarPassword { get; set; } = string.Empty;
    }

    public class CambiarPasswordViewModel
    {
        [Required(ErrorMessage = "Escribe tu contraseña actual.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string Actual { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(64, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos {2} caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string Nueva { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirma la contraseña.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Nueva), ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar contraseña")]
        public string Confirmar { get; set; } = string.Empty;
    }

    public class UsuarioListaItem
    {
        public string Id { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public IList<string> Roles { get; set; } = new List<string>();
        public bool Bloqueado { get; set; }
        /// <summary>Tiene activa la verificación en dos pasos.</summary>
        public bool DosPasos { get; set; }
        public bool Activo { get; set; }
        public string? Vinculo { get; set; }
        /// <summary>Escuelas del personal escolar ("Todas" para el administrador global).</summary>
        public string? Escuelas { get; set; }
    }

    /// <summary>Alta y edición de cuentas (solo Dirección). La contraseña la define el usuario con el enlace de correo.</summary>
    public class UsuarioFormViewModel
    {
        public string? UsuarioId { get; set; }

        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        [StringLength(120, MinimumLength = 5, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Escribe un correo electrónico válido.")]
        [Display(Name = "Correo electrónico")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona un rol.")]
        [Display(Name = "Rol")]
        public string Rol { get; set; } = Roles.Tutor;

        [Display(Name = "Tutor que representa")]
        public int? TutorId { get; set; }

        [Display(Name = "Alumno")]
        public int? AlumnoId { get; set; }

        [Display(Name = "Clínica donde trabaja")]
        public int? ClinicaId { get; set; }

        /// <summary>Dirección y Enfermería: a qué escuelas pertenece (al menos una).</summary>
        [Display(Name = "Escuelas")]
        public List<int> PlantelIds { get; set; } = new();

        /// <summary>Solo lo puede marcar otro administrador global.</summary>
        [Display(Name = "Administrador global (todas las escuelas)")]
        public bool AdministradorGlobal { get; set; }
    }
}
