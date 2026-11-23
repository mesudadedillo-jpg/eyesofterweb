using Microsoft.AspNetCore.StaticFiles;

namespace EyesofterWeb.Seguridad
{
    /// <summary>
    /// Caché y compresión de wwwroot (recomendaciones de Lighthouse en escritorio y móvil).
    ///  - Con "?v=..." (asp-append-version) el archivo cambia de URL al cambiar: se guarda 1 año.
    ///  - wwwroot/lib (Bootstrap, jQuery, íconos) y el favicon: 7 días.
    ///  - El resto: 1 hora.
    /// Las páginas con datos siguen con "no-store" (CabecerasSeguridad); esto es solo para archivos públicos.
    /// </summary>
    public static class ArchivosEstaticos
    {
        public const int UnAnio = 365 * 24 * 3600;
        public const int SieteDias = 7 * 24 * 3600;
        public const int UnaHora = 3600;

        /// <summary>Lo que vale la pena comprimir. Sin text/html: ver Program.cs (BREACH).</summary>
        public static readonly string[] TiposComprimibles =
        {
            "text/css", "text/javascript", "application/javascript", "image/svg+xml", "image/x-icon",
            "application/json", "font/ttf", "application/font-woff"
        };

        public static string CacheControl(PathString ruta, QueryString consulta)
        {
            if (consulta.HasValue && consulta.Value!.Contains("v=")) return $"public, max-age={UnAnio}, immutable";
            if (ruta.StartsWithSegments("/lib") || ruta.StartsWithSegments("/favicon.ico") || ruta.StartsWithSegments("/favicon.svg"))
                return $"public, max-age={SieteDias}";
            return $"public, max-age={UnaHora}";
        }

        public static void PonerCache(StaticFileResponseContext ctx) =>
            ctx.Context.Response.Headers.CacheControl = CacheControl(ctx.Context.Request.Path, ctx.Context.Request.QueryString);
    }
}
