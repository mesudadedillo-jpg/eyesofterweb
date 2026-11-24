using System.Net;
using System.Text.Json;
using EyesofterWeb.Servicios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace EyesofterWeb.Tests
{
    /// <summary>Envío de correo con Resend (Servicios/Correo.cs) sin salir a internet.</summary>
    public class CorreoTests
    {
        private sealed class ManejadorFalso : HttpMessageHandler
        {
            public HttpRequestMessage? Peticion;
            public string? Cuerpo;
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public bool Fallar;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (Fallar) throw new HttpRequestException("sin red");
                Peticion = request;
                Cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
                return new HttpResponseMessage(Codigo);
            }
        }

        private sealed class FabricaHttp(HttpMessageHandler manejador) : IHttpClientFactory
        {
            public int Creados;
            public HttpClient CreateClient(string name) { Creados++; return new HttpClient(manejador, disposeHandler: false); }
        }

        private sealed class EntornoHost(string nombre) : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = nombre;
            public string ApplicationName { get; set; } = "EyesofterWeb";
            public string ContentRootPath { get; set; } = ".";
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        }

        private static (CorreoResend Correo, ManejadorFalso Red, FabricaHttp Fabrica) Crear(bool configurado = true, string entorno = "Production")
        {
            var red = new ManejadorFalso();
            var fabrica = new FabricaHttp(red);
            var valores = configurado
                ? new Dictionary<string, string?> { ["RESEND_API_KEY"] = "re_prueba", ["CORREO_REMITENTE"] = "EYESOFTER <avisos@eyesofter.mx>" }
                : new Dictionary<string, string?>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
            return (new CorreoResend(fabrica, config, new EntornoHost(entorno), NullLogger<CorreoResend>.Instance), red, fabrica);
        }

        [Fact]
        public async Task Envia_a_la_API_de_Resend_con_la_llave_y_los_datos_del_correo()
        {
            var (correo, red, _) = Crear();

            var ok = await correo.EnviarAsync("tutor@ejemplo.mx", "EYESOFTER: aviso", "<p>Hola</p>");

            Assert.True(ok);
            Assert.Equal(HttpMethod.Post, red.Peticion!.Method);
            Assert.Equal("https://api.resend.com/emails", red.Peticion.RequestUri!.ToString());
            Assert.Equal("Bearer", red.Peticion.Headers.Authorization!.Scheme);
            Assert.Equal("re_prueba", red.Peticion.Headers.Authorization.Parameter);

            using var json = JsonDocument.Parse(red.Cuerpo!);
            var raiz = json.RootElement;
            Assert.Equal("EYESOFTER <avisos@eyesofter.mx>", raiz.GetProperty("from").GetString());
            Assert.Equal("tutor@ejemplo.mx", Assert.Single(raiz.GetProperty("to").EnumerateArray()).GetString());
            Assert.Equal("EYESOFTER: aviso", raiz.GetProperty("subject").GetString());
            Assert.Equal("<p>Hola</p>", raiz.GetProperty("html").GetString());
        }

        [Fact]
        public async Task Si_Resend_responde_error_regresa_false()
        {
            var (correo, red, _) = Crear();
            red.Codigo = HttpStatusCode.UnprocessableEntity;

            Assert.False(await correo.EnviarAsync("tutor@ejemplo.mx", "a", "b"));
        }

        [Fact]
        public async Task Sin_red_regresa_false_sin_lanzar_excepcion()
        {
            var (correo, red, _) = Crear();
            red.Fallar = true;

            Assert.False(await correo.EnviarAsync("tutor@ejemplo.mx", "a", "b"));
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Development")]
        public async Task Sin_configurar_no_llama_a_Resend(string entorno)
        {
            var (correo, red, fabrica) = Crear(configurado: false, entorno);

            Assert.False(await correo.EnviarAsync("tutor@ejemplo.mx", "a", "b"));
            Assert.Null(red.Peticion);
            Assert.Equal(0, fabrica.Creados);
        }
    }
}
