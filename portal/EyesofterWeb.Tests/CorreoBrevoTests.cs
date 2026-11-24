using System.Net;
using System.Text.Json;
using EyesofterWeb.Servicios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace EyesofterWeb.Tests
{
    /// <summary>Correo con Brevo y elección del proveedor (Servicios/Correo.cs), sin salir a internet.</summary>
    public class CorreoBrevoTests
    {
        private sealed class Red : HttpMessageHandler
        {
            public List<(HttpRequestMessage Peticion, string Cuerpo)> Llamadas { get; } = new();
            public HttpStatusCode Codigo = HttpStatusCode.Created;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Llamadas.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
                return new HttpResponseMessage(Codigo) { Content = new StringContent("""{"messageId":"<1@brevo>"}""") };
            }
        }

        private sealed class Fabrica(HttpMessageHandler red) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(red, disposeHandler: false);
        }

        private sealed class Host : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Production";
            public string ApplicationName { get; set; } = "EyesofterWeb";
            public string ContentRootPath { get; set; } = ".";
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        }

        private static IConfiguration Config(params (string, string?)[] v) =>
            new ConfigurationBuilder().AddInMemoryCollection(v.Select(x => new KeyValuePair<string, string?>(x.Item1, x.Item2))).Build();

        private static CorreoConfigurado Correo(Red red, IConfiguration config) =>
            new(new Fabrica(red), config, new Host(), NullLoggerFactory.Instance);

        [Fact]
        public async Task Envia_por_la_API_de_Brevo_con_su_llave_y_el_remitente_separado()
        {
            var red = new Red();
            var correo = Correo(red, Config(("BREVO_API_KEY", "xkeysib-prueba"), ("CORREO_REMITENTE", "EYESOFTER <eyesofter.avisos@gmail.com>")));

            Assert.True(await correo.EnviarAsync("tutor@ejemplo.mx", "EYESOFTER: aviso", "<p>Hola</p>"));

            var (peticion, cuerpo) = Assert.Single(red.Llamadas);
            Assert.Equal(HttpMethod.Post, peticion.Method);
            Assert.Equal("https://api.brevo.com/v3/smtp/email", peticion.RequestUri!.ToString());
            Assert.Equal("xkeysib-prueba", Assert.Single(peticion.Headers.GetValues("api-key")));
            Assert.Null(peticion.Headers.Authorization);

            using var json = JsonDocument.Parse(cuerpo);
            var raiz = json.RootElement;
            Assert.Equal("EYESOFTER", raiz.GetProperty("sender").GetProperty("name").GetString());
            Assert.Equal("eyesofter.avisos@gmail.com", raiz.GetProperty("sender").GetProperty("email").GetString());
            Assert.Equal("tutor@ejemplo.mx", Assert.Single(raiz.GetProperty("to").EnumerateArray()).GetProperty("email").GetString());
            Assert.Equal("EYESOFTER: aviso", raiz.GetProperty("subject").GetString());
            Assert.Equal("<p>Hola</p>", raiz.GetProperty("htmlContent").GetString());
        }

        [Fact]
        public async Task Si_Brevo_rechaza_el_correo_regresa_false()
        {
            var red = new Red { Codigo = HttpStatusCode.BadRequest };
            var correo = Correo(red, Config(("BREVO_API_KEY", "x"), ("CORREO_REMITENTE", "a@gmail.com")));

            Assert.False(await correo.EnviarAsync("tutor@ejemplo.mx", "a", "b"));
        }

        [Fact]
        public async Task Con_Resend_configurado_sigue_usando_Resend()
        {
            var red = new Red { Codigo = HttpStatusCode.OK };
            var correo = Correo(red, Config(("RESEND_API_KEY", "re_x"), ("CORREO_REMITENTE", "EYESOFTER <avisos@eyesofter.mx>")));

            Assert.True(await correo.EnviarAsync("tutor@ejemplo.mx", "a", "b"));
            Assert.Equal("https://api.resend.com/emails", Assert.Single(red.Llamadas).Peticion.RequestUri!.ToString());
        }

        [Fact]
        public async Task Sin_proveedor_no_llama_a_nadie()
        {
            var red = new Red();
            Assert.False(await Correo(red, Config(("CORREO_REMITENTE", "a@gmail.com"))).EnviarAsync("t@e.mx", "a", "b"));
            Assert.False(await Correo(red, Config(("BREVO_API_KEY", "x"))).EnviarAsync("t@e.mx", "a", "b"));
            Assert.Empty(red.Llamadas);
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData("x", null, "Brevo")]
        [InlineData(null, "re_x", "Resend")]
        [InlineData("x", "re_x", "Brevo")]
        public void Elige_el_proveedor(string? brevo, string? resend, string? esperado) =>
            Assert.Equal(esperado, CorreoConfigurado.Proveedor(Config(("BREVO_API_KEY", brevo), ("RESEND_API_KEY", resend), ("CORREO_REMITENTE", "a@gmail.com"))));

        [Theory]
        [InlineData("EYESOFTER <eyesofter.avisos@gmail.com>", "EYESOFTER", "eyesofter.avisos@gmail.com")]
        [InlineData("\"Escuela METANOIA\" <a@b.mx>", "Escuela METANOIA", "a@b.mx")]
        [InlineData("  avisos@gmail.com ", "EYESOFTER", "avisos@gmail.com")]
        [InlineData("<avisos@gmail.com>", "EYESOFTER", "avisos@gmail.com")]
        public void Separa_nombre_y_correo_del_remitente(string remitente, string nombre, string correo) =>
            Assert.Equal((nombre, correo), CorreoConfigurado.Remitente(remitente));
    }
}
