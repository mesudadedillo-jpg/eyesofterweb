using EyesofterWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Data
{
    /// <summary>
    /// Crea los roles, tres usuarios de prueba (uno por rol) y datos ficticios de ejemplo.
    /// Solo se ejecuta cuando la base de datos ya existe y no tiene migraciones pendientes,
    /// es decir, después de "dotnet ef database update".
    /// </summary>
    public static class DbSeeder
    {
        public const string PasswordAdmin = "Admin123!";
        public const string PasswordEnfermeria = "Enfermeria123!";
        public const string PasswordDocente = "Docente123!";

        public static async Task SeedAsync(IServiceProvider services, ILogger logger)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (!db.Database.GetMigrations().Any())
            {
                logger.LogWarning("No hay migraciones en el proyecto. Ejecuta: dotnet ef migrations add Inicial");
                return;
            }

            if (!await db.Database.CanConnectAsync())
            {
                logger.LogWarning("La base de datos aún no existe. Ejecuta: dotnet ef database update");
                return;
            }

            var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pendientes.Count > 0)
            {
                logger.LogWarning("Hay {Total} migración(es) pendiente(s): {Lista}. Ejecuta: dotnet ef database update",
                    pendientes.Count, string.Join(", ", pendientes));
                return;
            }

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1) Roles
            foreach (var rol in Roles.Todos)
            {
                if (!await roleManager.RoleExistsAsync(rol))
                {
                    await roleManager.CreateAsync(new IdentityRole(rol));
                    logger.LogInformation("Rol creado: {Rol}", rol);
                }
            }

            // 2) Un usuario de prueba por rol
            await CrearUsuarioAsync(userManager, logger, "admin@eyesofter.mx", "Administrador del sistema", PasswordAdmin, Roles.Administrador);
            await CrearUsuarioAsync(userManager, logger, "enfermeria@eyesofter.mx", "Laura Méndez (Enfermería)", PasswordEnfermeria, Roles.Enfermeria);
            await CrearUsuarioAsync(userManager, logger, "docente@eyesofter.mx", "Carlos Ruiz (Docente)", PasswordDocente, Roles.Docente);

            // 3) Datos ficticios del dominio (solo si la base está vacía)
            if (await db.Tutores.AnyAsync()) return;

            var tutores = new List<Tutor>
            {
                new() { Nombre = "María", Apellidos = "López Hernández", Telefono = "7751234567", Correo = "maria.lopez@ejemplo.com", Parentesco = "Madre" },
                new() { Nombre = "José", Apellidos = "García Pérez", Telefono = "7759876543", Correo = "jose.garcia@ejemplo.com", Parentesco = "Padre" },
                new() { Nombre = "Ana", Apellidos = "Martínez Soto", Telefono = "7755551234", Parentesco = "Abuela" }
            };
            db.Tutores.AddRange(tutores);

            var clinicas = new List<Clinica>
            {
                new() { Nombre = "Óptica Visión Clara", Tipo = TipoClinica.Optica, Direccion = "Av. 21 de Marzo 120, Centro, Tulancingo", Telefono = "7757530011", EsAsociada = true },
                new() { Nombre = "Centro Oftalmológico Hidalgo", Tipo = TipoClinica.Oftalmologia, Direccion = "Blvd. Felipe Ángeles 45, Pachuca", Telefono = "7717180022", Correo = "citas@cohidalgo.ejemplo", EsAsociada = true },
                new() { Nombre = "Centro de Salud Tulancingo", Tipo = TipoClinica.CentroDeSalud, Direccion = "Calle Hidalgo 300, Tulancingo", Telefono = "7757530099", EsAsociada = false }
            };
            db.Clinicas.AddRange(clinicas);
            await db.SaveChangesAsync();

            var alumnos = new List<Alumno>
            {
                new() { Matricula = "PRI2026001", Nombre = "Sofía", Apellidos = "López Ramírez", FechaNacimiento = new DateTime(2018, 3, 14), Nivel = NivelEducativo.Primaria, Grado = 2, Grupo = "A", TutorId = tutores[0].Id },
                new() { Matricula = "PRI2026002", Nombre = "Diego", Apellidos = "López Ramírez", FechaNacimiento = new DateTime(2016, 9, 2), Nivel = NivelEducativo.Primaria, Grado = 4, Grupo = "B", UsaLentes = true, TutorId = tutores[0].Id },
                new() { Matricula = "SEC2026001", Nombre = "Valeria", Apellidos = "García Torres", FechaNacimiento = new DateTime(2012, 6, 21), Nivel = NivelEducativo.Secundaria, Grado = 2, Grupo = "A", TutorId = tutores[1].Id },
                new() { Matricula = "SEC2026002", Nombre = "Mateo", Apellidos = "Martínez Cruz", FechaNacimiento = new DateTime(2013, 1, 30), Nivel = NivelEducativo.Secundaria, Grado = 1, Grupo = "C", TutorId = tutores[2].Id }
            };
            db.Alumnos.AddRange(alumnos);
            await db.SaveChangesAsync();

            var hoy = DateTime.Today;
            var casos = new List<Caso>
            {
                new() { Folio = "EYS-2026-0001", AlumnoId = alumnos[1].Id, ClinicaId = clinicas[0].Id, Semaforo = Semaforo.Rojo, Estado = EstadoCaso.EnSeguimiento,
                        FechaDeteccion = hoy.AddDays(-20), AgudezaOjoDerecho = "20/70", AgudezaOjoIzquierdo = "20/50",
                        Observaciones = "Entrecierra los ojos al leer el pizarrón.", RegistradoPor = "enfermeria@eyesofter.mx" },
                new() { Folio = "EYS-2026-0002", AlumnoId = alumnos[2].Id, Semaforo = Semaforo.Amarillo, Estado = EstadoCaso.Abierto,
                        FechaDeteccion = hoy.AddDays(-5), AgudezaOjoDerecho = "20/30", AgudezaOjoIzquierdo = "20/40",
                        Observaciones = "Repetir tamizaje en 30 días.", RegistradoPor = "enfermeria@eyesofter.mx" },
                new() { Folio = "EYS-2026-0003", AlumnoId = alumnos[0].Id, ClinicaId = clinicas[1].Id, Semaforo = Semaforo.Amarillo, Estado = EstadoCaso.Cerrado,
                        FechaDeteccion = hoy.AddDays(-60), FechaCierre = hoy.AddDays(-15), AgudezaOjoDerecho = "20/40", AgudezaOjoIzquierdo = "20/30",
                        Observaciones = "Atendida; se le recetaron lentes.", RegistradoPor = "admin@eyesofter.mx" }
            };
            db.Casos.AddRange(casos);
            await db.SaveChangesAsync();

            db.EventosCaso.AddRange(
                new EventoCaso { CasoId = casos[0].Id, Tipo = TipoEvento.Tamizaje, Fecha = hoy.AddDays(-20), Descripcion = "Tamizaje Snellen: OD 20/70, OI 20/50.", RegistradoPor = "enfermeria@eyesofter.mx" },
                new EventoCaso { CasoId = casos[0].Id, Tipo = TipoEvento.AvisoTutor, Fecha = hoy.AddDays(-19), Descripcion = "Se llamó a la madre para informar el resultado.", RegistradoPor = "enfermeria@eyesofter.mx" },
                new EventoCaso { CasoId = casos[0].Id, Tipo = TipoEvento.Cita, Fecha = hoy.AddDays(-10), Descripcion = "Cita agendada en Óptica Visión Clara.", RegistradoPor = "enfermeria@eyesofter.mx" },
                new EventoCaso { CasoId = casos[1].Id, Tipo = TipoEvento.Tamizaje, Fecha = hoy.AddDays(-5), Descripcion = "Tamizaje Snellen: OD 20/30, OI 20/40.", RegistradoPor = "enfermeria@eyesofter.mx" },
                new EventoCaso { CasoId = casos[2].Id, Tipo = TipoEvento.Tamizaje, Fecha = hoy.AddDays(-60), Descripcion = "Tamizaje Snellen: OD 20/40, OI 20/30.", RegistradoPor = "admin@eyesofter.mx" },
                new EventoCaso { CasoId = casos[2].Id, Tipo = TipoEvento.Comprobante, Fecha = hoy.AddDays(-16), Descripcion = "La familia entregó receta de lentes.", RegistradoPor = "admin@eyesofter.mx" },
                new EventoCaso { CasoId = casos[2].Id, Tipo = TipoEvento.Cierre, Fecha = hoy.AddDays(-15), Descripcion = "Caso cerrado: alumna atendida.", RegistradoPor = "admin@eyesofter.mx" }
            );
            await db.SaveChangesAsync();

            logger.LogInformation("Datos de ejemplo creados.");
        }

        private static async Task CrearUsuarioAsync(UserManager<ApplicationUser> userManager, ILogger logger,
            string correo, string nombre, string password, string rol)
        {
            var usuario = await userManager.FindByEmailAsync(correo);
            if (usuario is null)
            {
                usuario = new ApplicationUser
                {
                    UserName = correo,
                    Email = correo,
                    EmailConfirmed = true,
                    NombreCompleto = nombre
                };
                var resultado = await userManager.CreateAsync(usuario, password);
                if (!resultado.Succeeded)
                {
                    logger.LogError("No se pudo crear {Correo}: {Errores}", correo,
                        string.Join("; ", resultado.Errors.Select(e => e.Description)));
                    return;
                }
                logger.LogInformation("Usuario de prueba creado: {Correo} ({Rol})", correo, rol);
            }

            if (!await userManager.IsInRoleAsync(usuario, rol))
            {
                await userManager.AddToRoleAsync(usuario, rol);
            }
        }
    }
}
