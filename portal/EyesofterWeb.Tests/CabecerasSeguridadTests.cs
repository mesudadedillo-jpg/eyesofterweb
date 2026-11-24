using EyesofterWeb.Seguridad;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EyesofterWeb.Tests
{
    /// <summary>Cabeceras de seguridad (Seguridad/CabecerasSeguridad.cs).</summary>
    public class CabecerasSeguridadTests
    {
        private static async Task<HttpContext> EjecutarAsync(string ruta = "/")
        {
            var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
            app.UseCabecerasSeguridad();
            app.Run(_ => Task.CompletedTask);
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = ruta;
            await app.Build()(ctx);
            return ctx;
        }

        [Fact]
        public async Task El_formulario_de_Google_puede_redirigir_a_accounts_google_com()
        {
            var ctx = await EjecutarAsync();
            var csp = ctx.Response.Headers.ContentSecurityPolicy.ToString();

            Assert.Contains("form-action 'self' https://accounts.google.com;", csp);
        }

        [Fact]
        public async Task Sigue_sin_permitir_scripts_de_terceros_ni_iframes()
        {
            var ctx = await EjecutarAsync();
            var h = ctx.Response.Headers;

            Assert.Contains("script-src 'self';", h.ContentSecurityPolicy.ToString());
            Assert.Contains("frame-ancestors 'none'", h.ContentSecurityPolicy.ToString());
            Assert.Contains("object-src 'none'", h.ContentSecurityPolicy.ToString());
            Assert.Equal("DENY", h.XFrameOptions.ToString());
            Assert.Equal("nosniff", h.XContentTypeOptions.ToString());
        }

        [Fact]
        public async Task La_app_web_puede_ejecutar_wasm_y_llamar_a_la_API()
        {
            var ctx = await EjecutarAsync("/app-web/index.html");
            var csp = ctx.Response.Headers.ContentSecurityPolicy.ToString();

            Assert.Contains("script-src 'self' 'wasm-unsafe-eval';", csp);
            Assert.Contains("https://eyesofter-api.up.railway.app", csp);
            Assert.Contains("frame-ancestors 'none'", csp);
            Assert.DoesNotContain("'unsafe-eval'", csp.Replace("'wasm-unsafe-eval'", ""));
            Assert.Contains("geolocation=(self)", ctx.Response.Headers["Permissions-Policy"].ToString());
        }

        [Fact]
        public async Task El_portal_no_hereda_los_permisos_de_la_app_web()
        {
            var ctx = await EjecutarAsync("/Alumnos");

            Assert.DoesNotContain("wasm-unsafe-eval", ctx.Response.Headers.ContentSecurityPolicy.ToString());
            Assert.Contains("geolocation=()", ctx.Response.Headers["Permissions-Policy"].ToString());
        }
    }
}
