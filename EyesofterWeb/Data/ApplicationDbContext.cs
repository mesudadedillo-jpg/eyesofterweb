using System.Reflection;
using EyesofterWeb.Models;
using EyesofterWeb.Seguridad;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EyesofterWeb.Data
{
    /// <summary>
    /// Contexto único de Entity Framework Core (Code First) sobre PostgreSQL.
    ///
    /// Esta base la COMPARTEN el portal web y el servidor de la app móvil (eyesofter-backend):
    /// las migraciones de este proyecto son las dueñas del esquema. Las tablas y columnas van en
    /// snake_case (alumnos, fecha_deteccion) para que el servidor Ktor las use sin comillas.
    /// Si cambias una entidad, genera la migración y actualiza también
    /// eyesofter-backend/src/main/resources/esquema.sql (ver docs/MANUAL_TECNICO.md).
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        private readonly CifradoDatos _cifrado;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, CifradoDatos cifrado)
            : base(options)
        {
            _cifrado = cifrado;
        }

        public DbSet<Plantel> Planteles => Set<Plantel>();
        public DbSet<Campana> Campanas => Set<Campana>();
        public DbSet<Tutor> Tutores => Set<Tutor>();
        public DbSet<Alumno> Alumnos => Set<Alumno>();
        public DbSet<Clinica> Clinicas => Set<Clinica>();
        public DbSet<Tamizaje> Tamizajes => Set<Tamizaje>();
        public DbSet<Caso> Casos => Set<Caso>();
        public DbSet<EventoCaso> EventosCaso => Set<EventoCaso>();
        public DbSet<Pase> Pases => Set<Pase>();
        public DbSet<Atencion> Atenciones => Set<Atencion>();
        public DbSet<Comprobante> Comprobantes => Set<Comprobante>();
        public DbSet<Cita> Citas => Set<Cita>();
        public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
        public DbSet<EntregaNotificacion> EntregasNotificacion => Set<EntregaNotificacion>();
        public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();
        public DbSet<Consentimiento> Consentimientos => Set<Consentimiento>();
        public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();
        public DbSet<ReporteFalla> ReportesFalla => Set<ReporteFalla>();
        public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Primero la configuración de Identity.
            base.OnModelCreating(builder);

            // Tablas de Identity en snake_case, igual que el resto (el servidor Ktor las lee).
            builder.Entity<ApplicationUser>().ToTable("asp_net_users");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("asp_net_roles");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("asp_net_user_roles");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("asp_net_user_claims");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("asp_net_user_logins");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("asp_net_user_tokens");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("asp_net_role_claims");

            builder.Entity<ApplicationUser>(e =>
            {
                e.HasOne(u => u.Tutor).WithMany(t => t.Usuarios).HasForeignKey(u => u.TutorId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(u => u.Alumno).WithMany().HasForeignKey(u => u.AlumnoId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(u => u.Clinica).WithMany().HasForeignKey(u => u.ClinicaId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Plantel>(e =>
            {
                e.ToTable("planteles");
                e.HasData(new Plantel { Id = 1, Nombre = "Escuela Primaria-Secundaria METANOIA", Activo = true });
            });

            builder.Entity<Campana>(e =>
            {
                e.ToTable("campanas");
                e.Property(c => c.FechaInicio).HasColumnType("date");
                e.Property(c => c.FechaFin).HasColumnType("date");
                e.HasOne(c => c.Plantel).WithMany().HasForeignKey(c => c.PlantelId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Tutor>(e =>
            {
                e.ToTable("tutores");
                e.HasIndex(t => t.Telefono);
            });

            builder.Entity<Alumno>(e =>
            {
                e.ToTable("alumnos");
                e.HasIndex(a => a.Matricula).IsUnique();
                e.HasIndex(a => new { a.Nivel, a.Grado, a.Grupo });
                e.Property(a => a.Nivel).HasConversion<string>().HasMaxLength(20);
                e.Property(a => a.FechaNacimiento).HasColumnType("date");

                // No se puede borrar un tutor que todavía tiene alumnos.
                e.HasOne(a => a.Tutor).WithMany(t => t.Alumnos).HasForeignKey(a => a.TutorId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.Plantel).WithMany().HasForeignKey(a => a.PlantelId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Clinica>(e =>
            {
                e.ToTable("clinicas");
                e.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
            });

            builder.Entity<Tamizaje>(e =>
            {
                e.ToTable("tamizajes");
                e.HasIndex(t => t.Folio).IsUnique();
                e.Property(t => t.Fecha).HasColumnType("date");
                e.Property(t => t.Semaforo).HasConversion<string>().HasMaxLength(10);
                e.HasOne(t => t.Alumno).WithMany(a => a.Tamizajes).HasForeignKey(t => t.AlumnoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(t => t.Campana).WithMany(c => c.Tamizajes).HasForeignKey(t => t.CampanaId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Caso>(e =>
            {
                e.ToTable("casos");
                e.HasIndex(c => c.Folio).IsUnique();
                e.HasIndex(c => c.Estado);
                e.Property(c => c.Semaforo).HasConversion<string>().HasMaxLength(10);
                e.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
                e.Property(c => c.FechaDeteccion).HasColumnType("date");
                e.Property(c => c.FechaCierre).HasColumnType("date");
                e.Property(c => c.FechaAtencion).HasColumnType("date");

                // No se puede borrar un alumno con historial de casos ni una clínica con casos canalizados.
                e.HasOne(c => c.Alumno).WithMany(a => a.Casos).HasForeignKey(c => c.AlumnoId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(c => c.Clinica).WithMany(cl => cl.Casos).HasForeignKey(c => c.ClinicaId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(c => c.Tamizaje).WithMany().HasForeignKey(c => c.TamizajeId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<EventoCaso>(e =>
            {
                e.ToTable("eventos_caso");
                e.Property(ev => ev.Tipo).HasConversion<string>().HasMaxLength(20);
                e.Property(ev => ev.Fecha).HasColumnType("date");
                // Al borrar un caso se borra su línea de tiempo.
                e.HasOne(ev => ev.Caso).WithMany(c => c.Eventos).HasForeignKey(ev => ev.CasoId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Pase>(e =>
            {
                e.ToTable("pases");
                e.HasIndex(p => p.Codigo).IsUnique();
                e.Property(p => p.Estado).HasConversion<string>().HasMaxLength(12);
                e.HasOne(p => p.Caso).WithMany(c => c.Pases).HasForeignKey(p => p.CasoId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.Clinica).WithMany().HasForeignKey(p => p.ClinicaId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Atencion>(e =>
            {
                e.ToTable("atenciones");
                e.Property(a => a.Fecha).HasColumnType("date");
                e.Property(a => a.Origen).HasConversion<string>().HasMaxLength(10);
                e.HasOne(a => a.Caso).WithMany(c => c.Atenciones).HasForeignKey(a => a.CasoId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.Clinica).WithMany().HasForeignKey(a => a.ClinicaId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.Pase).WithMany().HasForeignKey(a => a.PaseId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(a => a.Comprobante).WithMany().HasForeignKey(a => a.ComprobanteId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Comprobante>(e =>
            {
                e.ToTable("comprobantes");
                e.HasIndex(c => c.Estado);
                e.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
                e.Property(c => c.Estado).HasConversion<string>().HasMaxLength(12);
                e.HasOne(c => c.Caso).WithMany(ca => ca.Comprobantes).HasForeignKey(c => c.CasoId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Cita>(e =>
            {
                e.ToTable("citas");
                e.Property(c => c.FechaPreferida).HasColumnType("date");
                e.Property(c => c.Estado).HasConversion<string>().HasMaxLength(12);
                e.HasOne(c => c.Caso).WithMany(ca => ca.Citas).HasForeignKey(c => c.CasoId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Clinica).WithMany().HasForeignKey(c => c.ClinicaId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Notificacion>(e =>
            {
                e.ToTable("notificaciones");
                e.HasIndex(n => new { n.UsuarioId, n.CreadaEn });
                // Índice parcial: el servidor Ktor busca rápido lo que falta enviar.
                e.HasIndex(n => n.PendienteEnvio).HasFilter("pendiente_envio");
                e.Property(n => n.Datos).HasColumnType("jsonb");
            });

            builder.Entity<EntregaNotificacion>(e =>
            {
                e.ToTable("entregas_notificacion");
                e.HasOne(x => x.Notificacion).WithMany(n => n.Entregas).HasForeignKey(x => x.NotificacionId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Dispositivo>(e =>
            {
                e.ToTable("dispositivos");
                e.HasIndex(d => d.UsuarioId);
            });

            builder.Entity<Consentimiento>(e =>
            {
                e.ToTable("consentimientos");
                e.HasIndex(c => new { c.UsuarioId, c.VersionAviso });
            });

            builder.Entity<RegistroAuditoria>(e =>
            {
                e.ToTable("auditoria");
                e.HasIndex(a => a.Fecha);
                e.HasIndex(a => new { a.Entidad, a.EntidadId });
            });

            builder.Entity<ReporteFalla>(e =>
            {
                e.ToTable("reportes_falla");
                e.Property(r => r.Estado).HasConversion<string>().HasMaxLength(12);
            });

            builder.Entity<DataProtectionKey>(e => e.ToTable("data_protection_keys"));

            AplicarCifrado(builder);
        }

        /// <summary>
        /// Las propiedades marcadas con [Cifrado] se guardan cifradas (C-RC-04). El cifrado
        /// cambia el tamaño, así que esas columnas son "text"/"bytea" sin límite; el límite
        /// de caracteres se sigue validando sobre el texto original con [StringLength].
        /// </summary>
        private void AplicarCifrado(ModelBuilder builder)
        {
            var texto = new ValueConverter<string?, string?>(v => _cifrado.Cifrar(v), v => _cifrado.Descifrar(v));
            var bytes = new ValueConverter<byte[], byte[]>(v => _cifrado.CifrarArchivo(v), v => _cifrado.DescifrarArchivo(v));

            foreach (var entidad in builder.Model.GetEntityTypes())
            {
                foreach (var prop in entidad.ClrType.GetProperties().Where(p => p.GetCustomAttribute<CifradoAttribute>() != null))
                {
                    var p = builder.Entity(entidad.ClrType).Property(prop.Name);
                    if (prop.PropertyType == typeof(byte[]))
                    {
                        // Comparador por referencia y tamaño: el archivo no se modifica en su lugar, se reemplaza.
                        var comparador = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<byte[]>(
                            (a, b) => ReferenceEquals(a, b) || (a != null && b != null && a.SequenceEqual(b)),
                            v => v.Length, v => v);
                        p.HasConversion(bytes, comparador).HasColumnType("bytea");
                    }
                    else
                    {
                        p.HasConversion(texto).HasColumnType("text");
                        p.Metadata.SetMaxLength(null);
                    }
                }
            }
        }
    }
}
