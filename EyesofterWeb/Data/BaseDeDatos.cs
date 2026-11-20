using EyesofterWeb.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace EyesofterWeb.Data
{
    public static class BaseDeDatos
    {
        /// <summary>
        /// Cadena de conexión a PostgreSQL. Railway entrega DATABASE_URL
        /// (postgresql://usuario:clave@host:puerto/base); si existe, tiene prioridad sobre
        /// ConnectionStrings:DefaultConnection de appsettings.json (que es para desarrollo local).
        /// </summary>
        public static string CadenaConexion(IConfiguration config)
        {
            var url = config["DATABASE_URL"];
            if (string.IsNullOrWhiteSpace(url))
            {
                return config.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection o DATABASE_URL.");
            }

            var uri = new Uri(url);
            var credenciales = uri.UserInfo.Split(':', 2);
            var cadena = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.TrimStart('/'),
                Username = Uri.UnescapeDataString(credenciales[0]),
                Password = credenciales.Length > 1 ? Uri.UnescapeDataString(credenciales[1]) : null,
                // En la red privada de Railway no hay TLS; por internet sí se exige (C-RC-03).
                SslMode = uri.Host.EndsWith(".railway.internal", StringComparison.OrdinalIgnoreCase) ? SslMode.Disable : SslMode.Require,
                MaxPoolSize = 30
            };
            return cadena.ConnectionString;
        }

        public static void Configurar(DbContextOptionsBuilder opciones, string cadena) =>
            opciones.UseNpgsql(cadena).UseSnakeCaseNamingConvention();
    }

    /// <summary>Lo usa "dotnet ef migrations ..." (no arranca la app completa).</summary>
    public class FabricaDisenio : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
            var opciones = new DbContextOptionsBuilder<ApplicationDbContext>();
            BaseDeDatos.Configurar(opciones, BaseDeDatos.CadenaConexion(config));
            return new ApplicationDbContext(opciones.Options, new CifradoDatos(CifradoDatos.ClaveDesarrollo));
        }
    }
}
