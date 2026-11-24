using EyesofterWeb.Seguridad;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EyesofterWeb.Tests
{
    /// <summary>Cabeceras de seguridad (Seguridad/CabecerasSeguridad.cs).</summary>
    public class CabecerasSeguridadTests
    {
        private static async Task<HttpContext> EjecutarAsync()
        {
            var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
            app.UseCabecerasSeguridad();
            app.Run(_ => Task.CompletedTask);
            var ctx = new DefaultHttpContext();
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
    }
}
