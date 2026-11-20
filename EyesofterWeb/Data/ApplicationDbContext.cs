using EyesofterWeb.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Data
{
    /// <summary>
    /// Contexto único de Entity Framework Core (Code First).
    /// Hereda de IdentityDbContext, así que las migraciones crean tanto las tablas
    /// de Identity (AspNetUsers, AspNetRoles, ...) como las 5 tablas del dominio.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Tutor> Tutores => Set<Tutor>();
        public DbSet<Alumno> Alumnos => Set<Alumno>();
        public DbSet<Clinica> Clinicas => Set<Clinica>();
        public DbSet<Caso> Casos => Set<Caso>();
        public DbSet<EventoCaso> EventosCaso => Set<EventoCaso>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Primero la configuración de Identity.
            base.OnModelCreating(builder);

            builder.Entity<Tutor>(e =>
            {
                e.ToTable("Tutores");
                e.HasIndex(t => t.Telefono);
            });

            builder.Entity<Alumno>(e =>
            {
                e.ToTable("Alumnos");
                e.HasIndex(a => a.Matricula).IsUnique();
                e.Property(a => a.Nivel).HasConversion<string>().HasMaxLength(20);
                e.Property(a => a.FechaNacimiento).HasColumnType("date");

                // No se puede borrar un tutor que todavía tiene alumnos.
                e.HasOne(a => a.Tutor)
                 .WithMany(t => t.Alumnos)
                 .HasForeignKey(a => a.TutorId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Clinica>(e =>
            {
                e.ToTable("Clinicas");
                e.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
            });

            builder.Entity<Caso>(e =>
            {
                e.ToTable("Casos");
                e.HasIndex(c => c.Folio).IsUnique();
                e.Property(c => c.Semaforo).HasConversion<string>().HasMaxLength(10);
                e.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
                e.Property(c => c.FechaDeteccion).HasColumnType("date");
                e.Property(c => c.FechaCierre).HasColumnType("date");

                // No se puede borrar un alumno con historial de casos.
                e.HasOne(c => c.Alumno)
                 .WithMany(a => a.Casos)
                 .HasForeignKey(c => c.AlumnoId)
                 .OnDelete(DeleteBehavior.Restrict);

                // No se puede borrar una clínica que tiene casos canalizados.
                e.HasOne(c => c.Clinica)
                 .WithMany(cl => cl.Casos)
                 .HasForeignKey(c => c.ClinicaId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<EventoCaso>(e =>
            {
                e.ToTable("EventosCaso");
                e.Property(ev => ev.Tipo).HasConversion<string>().HasMaxLength(20);
                e.Property(ev => ev.Fecha).HasColumnType("date");

                // Al borrar un caso se borra su línea de tiempo.
                e.HasOne(ev => ev.Caso)
                 .WithMany(c => c.Eventos)
                 .HasForeignKey(ev => ev.CasoId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
