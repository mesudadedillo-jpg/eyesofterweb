using System.Security.Claims;
using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Seguridad;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EyesofterWeb.Tests
{
    /// <summary>Correo falso: guarda lo que se "envió" en vez de llamar a Resend.</summary>
    public class CorreoFalso : ICorreo
    {
        public record Enviado(string Destino, string Asunto, string Html);
        public List<Enviado> Enviados { get; } = new();
        public bool Responder { get; set; } = true;

        public Task<bool> EnviarAsync(string destino, string asunto, string html)
        {
            Enviados.Add(new Enviado(destino, asunto, html));
            return Task.FromResult(Responder);
        }
    }

    /// <summary>
    /// Servicios reales (Identity + AccesoGoogle + Auditoria) sobre una base en memoria.
    /// Cada instancia tiene su propia base: las pruebas no se estorban.
    /// </summary>
    public sealed class Entorno : IDisposable
    {
        private readonly ServiceProvider _raiz;
        private readonly IServiceScope _alcance;
        public CorreoFalso Correo { get; } = new();

        public Entorno()
        {
            var servicios = new ServiceCollection();
            servicios.AddLogging();
            servicios.AddHttpContextAccessor();
            servicios.AddSingleton(new CifradoDatos(CifradoDatos.ClaveDesarrollo));
            servicios.AddScoped<AlcanceEscuela>();
            var nombreBase = "pruebas-" + Guid.NewGuid();
            servicios.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(nombreBase));
            servicios.AddIdentityCore<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            servicios.Configure<OpcionesEyesofter>(o => o.UrlWeb = "https://portal.ejemplo.mx/");
            servicios.AddScoped<Auditoria>();
            servicios.AddSingleton<ICorreo>(Correo);
            servicios.AddScoped<AccesoGoogle>();
            _raiz = servicios.BuildServiceProvider();
            _alcance = _raiz.CreateScope();
        }

        public T Get<T>() where T : notnull => _alcance.ServiceProvider.GetRequiredService<T>();
        public UserManager<ApplicationUser> Usuarios => Get<UserManager<ApplicationUser>>();
        public ApplicationDbContext Db => Get<ApplicationDbContext>();
        public AccesoGoogle Google => Get<AccesoGoogle>();

        public async Task<ApplicationUser> CrearUsuarioAsync(string correo, bool activo = true, string nombre = "Ana López")
        {
            var u = new ApplicationUser { UserName = correo, Email = correo, NombreCompleto = nombre, Activo = activo };
            var r = await Usuarios.CreateAsync(u, "Eyesofter123!");
            Assert.True(r.Succeeded, string.Join("; ", r.Errors.Select(e => e.Description)));
            return u;
        }

        /// <summary>Lo que regresa Google después de iniciar sesión (claims del userinfo v3).</summary>
        public static ExternalLoginInfo RespuestaGoogle(string sub, string? correo, bool? verificado = true)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, sub) };
            if (correo is not null) claims.Add(new Claim(ClaimTypes.Email, correo));
            // El manejador de Google convierte el booleano de JSON con ToString(): "True"/"False".
            if (verificado is not null) claims.Add(new Claim(AccesoGoogle.ClaimCorreoVerificado, verificado.Value ? "True" : "False"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AccesoGoogle.Proveedor));
            return new ExternalLoginInfo(principal, AccesoGoogle.Proveedor, sub, AccesoGoogle.NombreVisible);
        }

        public List<RegistroAuditoria> Bitacora(string accion) =>
            Db.Auditoria.AsNoTracking().Where(a => a.Accion == accion).ToList();

        public void Dispose()
        {
            _alcance.Dispose();
            _raiz.Dispose();
        }
    }
}
