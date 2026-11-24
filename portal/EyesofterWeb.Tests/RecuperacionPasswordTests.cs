using EyesofterWeb.Servicios;

namespace EyesofterWeb.Tests
{
    /// <summary>Reglas puras de "¿Olvidaste tu contraseña?" (Servicios/RecuperacionPassword.cs).</summary>
    public class RecuperacionPasswordTests
    {
        [Theory]
        [InlineData("tutor@ejemplo.mx", false)]
        [InlineData("775 123 4567", true)]
        [InlineData("+52 (775) 123-4567", true)]
        [InlineData("12345", false)]
        public void Distingue_correo_de_celular(string cuenta, bool esTelefono) =>
            Assert.Equal(esTelefono, RecuperacionPassword.EsTelefono(cuenta));

        [Theory]
        [InlineData("+52 775 123 4567", "7751234567")]
        [InlineData("7751234567", "7751234567")]
        [InlineData("123", null)]
        public void Toma_los_ultimos_10_digitos(string cuenta, string? esperado) =>
            Assert.Equal(esperado, RecuperacionPassword.Telefono10(cuenta));

        [Fact]
        public void El_correo_del_codigo_escapa_el_nombre_y_trae_el_codigo()
        {
            var html = RecuperacionPassword.HtmlCorreo("<img src=x onerror=alert(1)>", "042917");
            Assert.DoesNotContain("<img", html);
            Assert.Contains("042917", html);
            Assert.Contains($"{RecuperacionPassword.MinutosVigencia} minutos", html);
        }

        [Fact]
        public void El_SMS_trae_el_codigo_y_la_vigencia()
        {
            var sms = RecuperacionPassword.TextoSms("123456");
            Assert.Contains("123456", sms);
            Assert.Contains($"{RecuperacionPassword.MinutosVigencia} min", sms);
            Assert.True(sms.Length <= 160, "Debe caber en un solo SMS");
        }
    }
}
