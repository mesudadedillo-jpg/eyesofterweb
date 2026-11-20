namespace EyesofterWeb.Models
{
    /// <summary>
    /// Roles de la aplicación. Se usan en [Authorize(Roles = ...)] para que las
    /// restricciones se apliquen en el servidor y no solo ocultando botones.
    /// </summary>
    public static class Roles
    {
        public const string Administrador = "Administrador";
        public const string Enfermeria = "Enfermeria";
        public const string Docente = "Docente";

        /// <summary>Combinación para acciones que pueden hacer Administrador y Enfermería.</summary>
        public const string AdminOEnfermeria = Administrador + "," + Enfermeria;

        public static readonly string[] Todos = { Administrador, Enfermeria, Docente };
    }
}
