using System.Security.Claims;
using EyesofterWeb.Servicios;
using Microsoft.Extensions.Configuration;

namespace EyesofterWeb.Tests
{
    /// <summary>Reglas de "Iniciar sesión con Google" (Servicios/AccesoGoogle.cs).</summary>
    public class AccesoGoogleTests
    {
        // ---------- Primera vez: vincular por correo verificado ----------

        [Fact]
        public async Task Primera_vez_vincula_la_cuenta_con_el_mismo_correo_y_avisa_por_correo()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@ejemplo.mx");

            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("google-123", "ana@ejemplo.mx"));

            Assert.True(r.Exito);
            Assert.True(r.RecienVinculada);
            Assert.Equal(ana.Id, r.Usuario!.Id);
            var logins = await e.Usuarios.GetLoginsAsync(ana);
            var login = Assert.Single(logins);
            Assert.Equal(AccesoGoogle.Proveedor, login.LoginProvider);
            Assert.Equal("google-123", login.ProviderKey);

            var aviso = Assert.Single(e.Correo.Enviados);
            Assert.Equal("ana@ejemplo.mx", aviso.Destino);
            Assert.Contains("vinculaste tu cuenta de Google", aviso.Asunto);
            Assert.Contains("https://portal.ejemplo.mx/Cuenta/Google", aviso.Html);
            Assert.Single(e.Bitacora("GOOGLE_VINCULADA"));
        }

        [Fact]
        public async Task El_correo_de_Google_se_compara_sin_importar_mayusculas_ni_espacios()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@ejemplo.mx");

            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "  Ana@Ejemplo.MX "));

            Assert.True(r.Exito);
            Assert.Equal(ana.Id, r.Usuario!.Id);
        }

        [Fact]
        public async Task Despues_se_reconoce_por_el_id_de_Google_sin_volver_a_vincular_ni_mandar_correo()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@ejemplo.mx");
            await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "ana@ejemplo.mx"));
            e.Correo.Enviados.Clear();

            // Aunque la persona cambie el correo de su cuenta de Google, el "sub" es el mismo.
            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "ana.nueva@gmail.com"));

            Assert.True(r.Exito);
            Assert.False(r.RecienVinculada);
            Assert.Equal(ana.Id, r.Usuario!.Id);
            Assert.Empty(e.Correo.Enviados);
            Assert.Single(await e.Usuarios.GetLoginsAsync(ana));
        }

        [Fact]
        public async Task Si_el_correo_no_sale_igual_se_puede_entrar()
        {
            using var e = new Entorno();
            await e.CrearUsuarioAsync("ana@ejemplo.mx");
            e.Correo.Responder = false;

            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "ana@ejemplo.mx"));

            Assert.True(r.Exito);
            Assert.Single(e.Correo.Enviados);
        }

        // ---------- Rechazos ----------

        [Fact]
        public async Task Correo_no_verificado_por_Google_no_entra_ni_se_vincula()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@ejemplo.mx");

            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "ana@ejemplo.mx", verificado: false));

            Assert.False(r.Exito);
            Assert.Equal(RechazoGoogle.CorreoNoVerificado, r.Rechazo);
            Assert.Empty(await e.Usuarios.GetLoginsAsync(ana));
            Assert.Empty(e.Correo.Enviados);
        }

        [Fact]
        public async Task Sin_dato_email_verified_se_trata_como_no_verificado()
        {
            using var e = new Entorno();
            await e.CrearUsuarioAsync("ana@ejemplo.mx");

            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "ana@ejemplo.mx", verificado: null));

            Assert.Equal(RechazoGoogle.CorreoNoVerificado, r.Rechazo);
        }

        [Fact]
        public async Task Google_sin_correo_se_rechaza()
        {
            using var e = new Entorno();

            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", correo: null));

            Assert.Equal(RechazoGoogle.SinCorreo, r.Rechazo);
        }

        [Fact]
        public async Task Correo_sin_cuenta_no_crea_una_cuenta_nueva()
        {
            using var e = new Entorno();

            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "desconocido@gmail.com"));

            Assert.Equal(RechazoGoogle.SinCuenta, r.Rechazo);
            Assert.Empty(e.Usuarios.Users);
            var registro = Assert.Single(e.Bitacora("LOGIN_GOOGLE_RECHAZADO"));
            Assert.False(registro.Exito);
            Assert.Equal("desconocido@gmail.com", registro.Usuario);
        }

        [Fact]
        public async Task Cuenta_desactivada_no_entra_ni_la_primera_vez_ni_ya_vinculada()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@ejemplo.mx", activo: false);

            var primera = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "ana@ejemplo.mx"));
            Assert.Equal(RechazoGoogle.CuentaDesactivada, primera.Rechazo);

            // Vinculada cuando estaba activa y desactivada después.
            var beto = await e.CrearUsuarioAsync("beto@ejemplo.mx");
            await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-2", "beto@ejemplo.mx"));
            beto.Activo = false;
            await e.Usuarios.UpdateAsync(beto);

            var despues = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-2", "beto@ejemplo.mx"));
            Assert.Equal(RechazoGoogle.CuentaDesactivada, despues.Rechazo);
            Assert.Null(despues.Usuario);
        }

        [Fact]
        public async Task Una_cuenta_solo_se_vincula_con_una_cuenta_de_Google()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@ejemplo.mx");
            await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "ana@ejemplo.mx"));

            // Otra cuenta de Google que dice tener el mismo correo (p. ej. un alias recreado).
            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-otra", "ana@ejemplo.mx"));

            Assert.Equal(RechazoGoogle.OtraCuentaGoogle, r.Rechazo);
            Assert.Equal("g-1", Assert.Single(await e.Usuarios.GetLoginsAsync(ana)).ProviderKey);
        }

        // ---------- Vincular con la sesión iniciada y quitar ----------

        [Fact]
        public async Task Con_sesion_iniciada_se_puede_vincular_una_cuenta_de_Google_con_otro_correo()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@escuela.edu.mx");

            var r = await e.Google.VincularAsync(ana, Entorno.RespuestaGoogle("g-1", "ana.personal@gmail.com"));

            Assert.True(r.Exito);
            Assert.True(r.RecienVinculada);
            var aviso = Assert.Single(e.Correo.Enviados);
            Assert.Equal("ana@escuela.edu.mx", aviso.Destino); // el aviso va al correo de la escuela
            Assert.Contains("ana.personal@gmail.com", aviso.Html);

            // Y desde entonces esa cuenta de Google entra directo.
            var entrada = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "ana.personal@gmail.com"));
            Assert.Equal(ana.Id, entrada.Usuario!.Id);
        }

        [Fact]
        public async Task No_se_puede_vincular_una_cuenta_de_Google_que_ya_es_de_otra_persona()
        {
            using var e = new Entorno();
            await e.CrearUsuarioAsync("ana@ejemplo.mx");
            var beto = await e.CrearUsuarioAsync("beto@ejemplo.mx");
            await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-ana", "ana@ejemplo.mx"));

            var r = await e.Google.VincularAsync(beto, Entorno.RespuestaGoogle("g-ana", "ana@ejemplo.mx"));

            Assert.Equal(RechazoGoogle.GoogleDeOtraCuenta, r.Rechazo);
            Assert.Empty(await e.Usuarios.GetLoginsAsync(beto));
        }

        [Fact]
        public async Task Vincular_dos_veces_la_misma_cuenta_de_Google_no_falla_ni_repite_el_aviso()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@ejemplo.mx");
            await e.Google.VincularAsync(ana, Entorno.RespuestaGoogle("g-1", "ana@ejemplo.mx"));

            var r = await e.Google.VincularAsync(ana, Entorno.RespuestaGoogle("g-1", "ana@ejemplo.mx"));

            Assert.True(r.Exito);
            Assert.False(r.RecienVinculada);
            Assert.Single(e.Correo.Enviados);
        }

        [Fact]
        public async Task Desvincular_quita_Google_avisa_por_correo_y_ya_no_reconoce_esa_cuenta()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@ejemplo.mx");
            await e.Google.VincularAsync(ana, Entorno.RespuestaGoogle("g-1", "ana.personal@gmail.com"));
            Assert.True(await e.Google.VinculadaAsync(ana));
            e.Correo.Enviados.Clear();

            Assert.True(await e.Google.DesvincularAsync(ana));

            Assert.False(await e.Google.VinculadaAsync(ana));
            Assert.Contains("quitaste", Assert.Single(e.Correo.Enviados).Asunto);
            Assert.Single(e.Bitacora("GOOGLE_DESVINCULADA"));
            var r = await e.Google.ResolverAsync(Entorno.RespuestaGoogle("g-1", "ana.personal@gmail.com"));
            Assert.Equal(RechazoGoogle.SinCuenta, r.Rechazo);
        }

        [Fact]
        public async Task Desvincular_sin_Google_no_hace_nada()
        {
            using var e = new Entorno();
            var ana = await e.CrearUsuarioAsync("ana@ejemplo.mx");

            Assert.False(await e.Google.DesvincularAsync(ana));
            Assert.Empty(e.Correo.Enviados);
        }

        // ---------- Configuración y datos de Google ----------

        private static IConfiguration Config(params (string Clave, string? Valor)[] valores) =>
            new ConfigurationBuilder().AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor))).Build();

        [Fact]
        public void Sin_credenciales_Google_queda_apagado()
        {
            Assert.False(AccesoGoogle.Configurado(Config()));
            Assert.False(AccesoGoogle.Configurado(Config(("GOOGLE_CLIENT_ID", "id"))));
            Assert.False(AccesoGoogle.Configurado(Config(("GOOGLE_CLIENT_ID", "id"), ("GOOGLE_CLIENT_SECRET", "  "))));
        }

        [Fact]
        public void Credenciales_por_variables_de_Railway_o_por_appsettings()
        {
            Assert.True(AccesoGoogle.Configurado(Config(("GOOGLE_CLIENT_ID", "id"), ("GOOGLE_CLIENT_SECRET", "s"))));
            Assert.True(AccesoGoogle.Configurado(Config(("Authentication:Google:ClientId", "id"), ("Authentication:Google:ClientSecret", "s"))));
            // Las variables de entorno ganan.
            var (id, secreto) = AccesoGoogle.Credenciales(Config(("GOOGLE_CLIENT_ID", " env "), ("Authentication:Google:ClientId", "json"),
                                                                ("GOOGLE_CLIENT_SECRET", "s")));
            Assert.Equal("env", id);
            Assert.Equal("s", secreto);
        }

        [Theory]
        [InlineData("True", true)]
        [InlineData("true", true)]
        [InlineData("False", false)]
        [InlineData("si", false)]
        [InlineData(null, false)]
        public void Lee_email_verified(string? valor, bool esperado)
        {
            var claims = valor is null ? Array.Empty<Claim>() : new[] { new Claim(AccesoGoogle.ClaimCorreoVerificado, valor) };
            Assert.Equal(esperado, AccesoGoogle.CorreoVerificado(new ClaimsPrincipal(new ClaimsIdentity(claims))));
        }

        [Theory]
        [InlineData(" Ana@Gmail.COM ", "ana@gmail.com")]
        [InlineData("sin-arroba", null)]
        [InlineData("", null)]
        public void Normaliza_el_correo_de_Google(string valor, string? esperado)
        {
            var p = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, valor) }));
            Assert.Equal(esperado, AccesoGoogle.Correo(p));
        }

        [Fact]
        public void Cada_rechazo_tiene_un_mensaje_claro()
        {
            foreach (var motivo in Enum.GetValues<RechazoGoogle>())
            {
                var texto = AccesoGoogle.Mensaje(motivo);
                Assert.False(string.IsNullOrWhiteSpace(texto));
                Assert.NotEqual("No se pudo iniciar sesión con Google.", texto);
            }
        }

        [Fact]
        public void Los_correos_de_aviso_escapan_el_nombre_y_el_correo()
        {
            var html = AccesoGoogle.HtmlAvisoVinculacion("<script>alert(1)</script>", "x\"@gmail.com", "https://portal.mx");
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.DoesNotContain("x\"@", html);

            var quitar = AccesoGoogle.HtmlAvisoDesvinculacion("<b>Ana</b>", "https://portal.mx/");
            Assert.DoesNotContain("<b>Ana</b>", quitar);
            Assert.Contains("https://portal.mx/Cuenta/Olvide", quitar);
        }
    }
}
