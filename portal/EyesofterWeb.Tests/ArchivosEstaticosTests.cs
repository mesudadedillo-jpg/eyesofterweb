using EyesofterWeb.Seguridad;
using Microsoft.AspNetCore.Http;

namespace EyesofterWeb.Tests
{
    /// <summary>Caché y compresión de archivos estáticos (recomendaciones de Lighthouse).</summary>
    public class ArchivosEstaticosTests
    {
        [Theory]
        [InlineData("/css/site.css", "?v=abc123", "public, max-age=31536000, immutable")]
        [InlineData("/lib/bootstrap/dist/css/bootstrap.min.css", "", "public, max-age=604800")]
        [InlineData("/favicon.svg", "", "public, max-age=604800")]
        [InlineData("/js/lector-qr.js", "", "public, max-age=3600")]
        public void Cache_segun_el_tipo_de_archivo(string ruta, string consulta, string esperado) =>
            Assert.Equal(esperado, ArchivosEstaticos.CacheControl(new PathString(ruta), new QueryString(consulta)));

        [Fact]
        public void El_HTML_no_se_comprime_para_no_exponer_el_token_antifalsificacion()
        {
            Assert.DoesNotContain("text/html", ArchivosEstaticos.TiposComprimibles);
            Assert.Contains("text/css", ArchivosEstaticos.TiposComprimibles);
            Assert.Contains("text/javascript", ArchivosEstaticos.TiposComprimibles);
        }
    }
}
