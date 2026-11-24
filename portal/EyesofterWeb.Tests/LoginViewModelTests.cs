using System.ComponentModel.DataAnnotations;
using EyesofterWeb.Models.ViewModels;

namespace EyesofterWeb.Tests
{
    /// <summary>Validación del formulario de inicio de sesión con correo y contraseña.</summary>
    public class LoginViewModelTests
    {
        private static List<ValidationResult> Validar(object modelo)
        {
            var errores = new List<ValidationResult>();
            Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);
            return errores;
        }

        [Fact]
        public void Correo_y_contrasena_validos_pasan()
        {
            Assert.Empty(Validar(new LoginViewModel { Correo = "ana@ejemplo.mx", Password = "Eyesofter123!" }));
        }

        [Theory]
        [InlineData("", "El correo es obligatorio.")]
        [InlineData("ana-sin-arroba", "Escribe un correo electrónico válido.")]
        public void Correo_vacio_o_invalido_muestra_mensaje_en_espanol(string correo, string mensaje)
        {
            var errores = Validar(new LoginViewModel { Correo = correo, Password = "x" });
            Assert.Contains(errores, e => e.ErrorMessage == mensaje);
        }

        [Fact]
        public void Contrasena_obligatoria()
        {
            var errores = Validar(new LoginViewModel { Correo = "ana@ejemplo.mx", Password = "" });
            Assert.Contains(errores, e => e.ErrorMessage == "La contraseña es obligatoria.");
        }
    }
}
