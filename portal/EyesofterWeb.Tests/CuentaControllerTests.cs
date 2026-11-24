using EyesofterWeb.Controllers;
using EyesofterWeb.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace EyesofterWeb.Tests
{
    /// <summary>El botón de Google en la pantalla de inicio de sesión (Controllers/CuentaController.cs).</summary>
    public class CuentaControllerTests
    {
        private static CuentaController Controlador(bool googleConfigurado)
        {
            var valores = googleConfigurado
                ? new Dictionary<string, string?> { ["GOOGLE_CLIENT_ID"] = "id.apps.googleusercontent.com", ["GOOGLE_CLIENT_SECRET"] = "secreto" }
                : new Dictionary<string, string?>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
            // Estas acciones no usan Identity: basta con la configuración.
            return new CuentaController(null!, null!, null!, NullLogger<CuentaController>.Instance, null!, null!, null!, config)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void El_boton_de_Google_solo_aparece_si_esta_configurado(bool configurado)
        {
            var vista = Assert.IsType<ViewResult>(Controlador(configurado).Login(returnUrl: "/Casos"));
            var modelo = Assert.IsType<LoginViewModel>(vista.Model);

            Assert.Equal(configurado, modelo.GoogleDisponible);
            Assert.Equal("/Casos", modelo.ReturnUrl);
        }

        [Fact]
        public void Si_la_persona_cancela_en_Google_se_le_avisa()
        {
            var c = Controlador(true);
            c.Login(google: "cancelado");
            Assert.Contains("Google", (string)c.ViewBag.Aviso);
        }

        [Fact]
        public void Sin_configurar_el_paso_a_Google_regresa_al_login()
        {
            var r = Assert.IsType<RedirectToActionResult>(Controlador(false).LoginGoogle("/Casos"));
            Assert.Equal(nameof(CuentaController.Login), r.ActionName);
        }
    }
}
