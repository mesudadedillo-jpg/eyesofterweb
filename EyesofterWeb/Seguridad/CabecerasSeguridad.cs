namespace EyesofterWeb.Seguridad
{
    /// <summary>
    /// Cabeceras HTTP de seguridad en todas las respuestas:
    ///  - CSP: solo se ejecutan scripts propios (wwwroot), nada de terceros ni en línea.
    ///  - frame-ancestors 'none': nadie puede incrustar el portal en un iframe (clickjacking).
    ///  - Permissions-Policy: la cámara solo para el propio portal (escaneo del QR, W-RI-02).
    /// </summary>
    public static class CabecerasSeguridad
    {
        public static IApplicationBuilder UseCabecerasSeguridad(this IApplicationBuilder app) =>
            app.Use(async (ctx, siguiente) =>
            {
                var h = ctx.Response.Headers;
                h["Content-Security-Policy"] =
                    "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
                    "img-src 'self' data: blob:; media-src 'self' blob: mediastream:; font-src 'self'; " +
                    "connect-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'";
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), payment=()";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                await siguiente();
            });
    }
}
