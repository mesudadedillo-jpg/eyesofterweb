namespace EyesofterWeb.Models
{
    /// <summary>
    /// Roles del documento de requerimientos (C-RFN-01): Dirección, Enfermería, Tutor,
    /// Alumno y Especialista externo. Se usan en [Authorize(Roles = ...)] para que las
    /// restricciones se apliquen en el servidor y no solo ocultando botones.
    ///
    /// Los nombres internos no llevan acentos porque viajan en cookies y en la base,
    /// que comparte el servidor de la app móvil. El nombre visible está en <see cref="NombreVisible"/>.
    /// </summary>
    public static class Roles
    {
        public const string Administrador = "Administrador"; // Dirección
        public const string Enfermeria = "Enfermeria";
        public const string Tutor = "Tutor";
        public const string Alumno = "Alumno";
        public const string Especialista = "Especialista";   // Especialista de clínica asociada

        /// <summary>Personal escolar: puede ver y gestionar expedientes.</summary>
        public const string AdminOEnfermeria = Administrador + "," + Enfermeria;

        public static readonly string[] Todos = { Administrador, Enfermeria, Tutor, Alumno, Especialista };

        public static string NombreVisible(string rol) => rol switch
        {
            Administrador => "Dirección",
            Enfermeria => "Enfermería",
            Tutor => "Tutor",
            Alumno => "Alumno",
            Especialista => "Especialista",
            _ => "Sin rol"
        };
    }
}
