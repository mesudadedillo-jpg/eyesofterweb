namespace EyesofterWeb.Seguridad
{
    /// <summary>
    /// Cabeceras HTTP de seguridad en todas las respuestas:
    ///  - CSP: solo se ejecutan scripts propios (wwwroot), nada de terceros ni en línea.
    ///  - frame-ancestors 'none': nadie puede incrustar el portal en un iframe (clickjacking).
    ///  - form-action permite accounts.google.com: el botón "Iniciar sesión con Google" es un formulario
    ///    que redirige allá (Chrome aplica form-action también a esa redirección).
    ///  - Permissions-Policy: la cámara solo para el propio portal (escaneo del QR, W-RI-02).
    ///  - Cache-Control: no-store en todo lo que se ve con sesión iniciada.
    ///  - /app-web (la app KMP compilada a WebAssembly, ver kmp/): política propia, más abierta solo
    ///    en lo que la app necesita (ejecutar wasm, llamar a la API y a OpenStreetMap, ubicación).
    /// </summary>
    public static class CabecerasSeguridad
    {
        /// <summary>Ruta donde se publica la versión web de la app (wwwroot/app-web).</summary>
        public const string RutaAppWeb = "/app-web";

        /// <summary>
        /// CSP de la app web: 'wasm-unsafe-eval' permite compilar WebAssembly (no habilita eval de JS).
        /// connect-src: la API de la app (Ktor) y los servicios de OpenStreetMap del mapa de clínicas.
        /// </summary>
        public const string PoliticaAppWeb =
            "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob: https:; font-src 'self' data:; " +
            "connect-src 'self' https://eyesofter-api.up.railway.app https://nominatim.openstreetmap.org " +
            "https://overpass-api.de https://overpass.kumi.systems https://overpass.private.coffee; " +
            "worker-src 'self' blob:; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'";

        public const string PoliticaContenido =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob:; media-src 'self' blob: mediastream:; font-src 'self'; " +
            "connect-src 'self'; frame-ancestors 'none'; form-action 'self' https://accounts.google.com; base-uri 'self'; object-src 'none'";

        public static IApplicationBuilder UseCabecerasSeguridad(this IApplicationBuilder app) =>
            app.Use(async (ctx, siguiente) =>
            {
                var h = ctx.Response.Headers;
                var esAppWeb = ctx.Request.Path.StartsWithSegments(RutaAppWeb);
                h["Content-Security-Policy"] = esAppWeb ? PoliticaAppWeb : PoliticaContenido;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = esAppWeb
                    ? "camera=(self), microphone=(), geolocation=(self), payment=()" // clínicas cercanas
                    : "camera=(self), microphone=(), geolocation=(), payment=()";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                // Páginas y archivos con sesión (datos de alumnos): que el navegador no los guarde.
                // En una computadora compartida de la escuela, "Atrás" tras cerrar sesión no muestra nada.
                ctx.Response.OnStarting(() =>
                {
                    if (ctx.User.Identity?.IsAuthenticated == true)
                    {
                        ctx.Response.Headers.CacheControl = "no-store, max-age=0";
                        ctx.Response.Headers.Pragma = "no-cache";
                    }
                    return Task.CompletedTask;
                });
                await siguiente();
            });
    }
}
