using EyesofterWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Data
{
    /// <summary>
    /// Prepara la base al arrancar:
    ///  1. Aplica las migraciones pendientes (MIGRAR_AL_INICIAR=false para desactivarlo).
    ///  2. Crea los 5 roles del documento.
    ///  3. Crea la cuenta inicial de Dirección con ADMIN_CORREO / ADMIN_PASSWORD si todavía no hay ninguna.
    ///     Si nadie es administrador global, marca como tal la cuenta ADMIN_GLOBAL_CORREO (o
    ///     ADMIN_CORREO): es quien da de alta escuelas.
    ///  4. SOLO en desarrollo (o con SEMBRAR_DEMO=true): usuarios y datos ficticios de ejemplo.
    ///     En producción NO se crean cuentas con contraseñas conocidas.
    /// </summary>
    public static class DbSeeder
    {
        public const string PasswordDemo = "Eyesofter123!";

        public static async Task SeedAsync(IServiceProvider services, ILogger logger, IHostEnvironment entorno, IConfiguration config)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (!string.Equals(config["MIGRAR_AL_INICIAR"], "false", StringComparison.OrdinalIgnoreCase))
            {
                var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();
                if (pendientes.Count > 0)
                {
                    logger.LogInformation("Aplicando {Total} migración(es): {Lista}", pendientes.Count, string.Join(", ", pendientes));
                    await db.Database.MigrateAsync();
                }
            }
            else if ((await db.Database.GetPendingMigrationsAsync()).Any())
            {
                logger.LogWarning("Hay migraciones pendientes. Ejecuta: dotnet ef database update");
                return;
            }

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var rol in Roles.Todos)
            {
                if (!await roleManager.RoleExistsAsync(rol))
                {
                    await roleManager.CreateAsync(new IdentityRole(rol));
                    logger.LogInformation("Rol creado: {Rol}", rol);
                }
            }

            var demo = entorno.IsDevelopment() || string.Equals(config["SEMBRAR_DEMO"], "true", StringComparison.OrdinalIgnoreCase);

            // Cuenta inicial de Dirección.
            if ((await userManager.GetUsersInRoleAsync(Roles.Administrador)).Count == 0)
            {
                var correo = config["ADMIN_CORREO"] ?? (demo ? "admin@eyesofter.mx" : null);
                var clave = config["ADMIN_PASSWORD"] ?? (demo ? PasswordDemo : null);
                if (correo is null || clave is null)
                    logger.LogWarning("No hay ninguna cuenta de Dirección. Define ADMIN_CORREO y ADMIN_PASSWORD y reinicia.");
                else
                    await CrearUsuarioAsync(userManager, logger, correo, "Dirección General", clave, Roles.Administrador);
            }

            // Multiescuela: alguien debe poder dar de alta escuelas y ver todas.
            if (!await db.Users.AnyAsync(u => u.AdministradorGlobal))
            {
                var correoGlobal = config["ADMIN_GLOBAL_CORREO"] ?? config["ADMIN_CORREO"] ?? (demo ? "admin@eyesofter.mx" : null);
                var global = correoGlobal is null ? null : await userManager.FindByEmailAsync(correoGlobal);
                if (global is not null && await userManager.IsInRoleAsync(global, Roles.Administrador))
                {
                    global.AdministradorGlobal = true;
                    await userManager.UpdateAsync(global);
                    logger.LogInformation("{Correo} es administrador global (todas las escuelas).", correoGlobal);
                }
                else
                {
                    logger.LogWarning("No hay administrador global. Define ADMIN_GLOBAL_CORREO con el correo de una cuenta de Dirección y reinicia.");
                }
            }

            // El repositorio es público y el README publica las cuentas demo con su contraseña.
            // En producción, cualquier cuenta demo que todavía la tenga se desactiva: si no,
            // cualquiera que lea el README podría entrar.
            if (!demo) await DesactivarCuentasDemoAsync(userManager, logger);

            if (!demo || await db.Tutores.AnyAsync()) return;
            await SembrarDemoAsync(db, userManager, logger);
        }

        private static readonly string[] CorreosDemo =
        {
            "admin@eyesofter.mx", "enfermeria@eyesofter.mx", "enfermeria2@eyesofter.mx",
            "tutor@eyesofter.mx", "alumno@eyesofter.mx", "especialista@eyesofter.mx"
        };

        private static async Task DesactivarCuentasDemoAsync(UserManager<ApplicationUser> userManager, ILogger logger)
        {
            foreach (var correo in CorreosDemo)
            {
                var u = await userManager.FindByEmailAsync(correo);
                if (u is null || !u.Activo || !await userManager.CheckPasswordAsync(u, PasswordDemo)) continue;
                u.Activo = false;
                await userManager.UpdateAsync(u);
                await userManager.UpdateSecurityStampAsync(u);
                logger.LogCritical("La cuenta demo {Correo} tenía la contraseña pública del README y se DESACTIVÓ. " +
                                   "Si la necesitas, cámbiale la contraseña y reactívala en Usuarios.", correo);
            }
        }

        private static async Task SembrarDemoAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger logger)
        {
            var hoy = DateTime.Today;
            var anterior = new Campana { Nombre = "Campaña de otoño", CicloEscolar = $"{hoy.Year - 1}-{hoy.Year}", FechaInicio = hoy.AddYears(-1).AddDays(-30), FechaFin = hoy.AddYears(-1) };
            var actual = new Campana { Nombre = "Campaña de otoño", CicloEscolar = $"{hoy.Year}-{hoy.Year + 1}", FechaInicio = hoy.AddDays(-60) };
            db.Campanas.AddRange(anterior, actual);

            var tutores = new List<Tutor>
            {
                new() { Nombre = "María", Apellidos = "López Hernández", Telefono = "7751234567", Correo = "tutor@eyesofter.mx", Parentesco = "Madre" },
                new() { Nombre = "José", Apellidos = "García Pérez", Telefono = "7759876543", Correo = "jose.garcia@ejemplo.com", Parentesco = "Padre" },
                new() { Nombre = "Ana", Apellidos = "Martínez Soto", Telefono = "7755551234", Parentesco = "Abuela" }
            };
            db.Tutores.AddRange(tutores);

            var clinicas = new List<Clinica>
            {
                new() { Nombre = "Óptica Tulancingo Centro", Tipo = TipoClinica.Optica, Direccion = "Av. Juárez 102, Centro", Ciudad = "Tulancingo", CodigoPostal = "43600", Latitud = 20.0825, Longitud = -98.3622, Telefono = "7757531234", EsAsociada = true, Horario = "Lun a Sáb 9:00-19:00" },
                new() { Nombre = "Optometría Visión Real", Tipo = TipoClinica.Optica, Direccion = "Calle Morelos 45", Ciudad = "Tulancingo", CodigoPostal = "43600", Latitud = 20.0841, Longitud = -98.3601, Telefono = "7757535678", EsAsociada = false },
                new() { Nombre = "Óptica Devlyn Tulancingo", Tipo = TipoClinica.Optica, Direccion = "Plaza Bella Local 12", Ciudad = "Tulancingo", CodigoPostal = "43600", Latitud = 20.0890, Longitud = -98.3580, Telefono = "7757539999", EsAsociada = false },
                new() { Nombre = "Centro Oftalmológico Hidalgo", Tipo = TipoClinica.Oftalmologia, Direccion = "Blvd. Felipe Ángeles 45", Ciudad = "Pachuca", CodigoPostal = "42080", Latitud = 20.1011, Longitud = -98.7591, Telefono = "7717180022", Correo = "citas@cohidalgo.ejemplo", EsAsociada = true },
                new() { Nombre = "Centro de Salud Tulancingo", Tipo = TipoClinica.CentroDeSalud, Direccion = "Calle Hidalgo 300", Ciudad = "Tulancingo", CodigoPostal = "43600", Latitud = 20.0861, Longitud = -98.3655, Telefono = "7757530099", EsAsociada = false }
            };
            db.Clinicas.AddRange(clinicas);
            await db.SaveChangesAsync();

            var alumnos = new List<Alumno>
            {
                new() { Matricula = "PRI2026001", Nombre = "Sofía", Apellidos = "López Ramírez", FechaNacimiento = new DateTime(2018, 3, 14), Nivel = NivelEducativo.Primaria, Grado = 2, Grupo = "A", TutorId = tutores[0].Id },
                new() { Matricula = "PRI2026002", Nombre = "Diego", Apellidos = "López Ramírez", FechaNacimiento = new DateTime(2016, 9, 2), Nivel = NivelEducativo.Primaria, Grado = 4, Grupo = "B", UsaLentes = true, TutorId = tutores[0].Id },
                new() { Matricula = "PRI2026003", Nombre = "Camila", Apellidos = "Ortiz Luna", FechaNacimiento = new DateTime(2017, 5, 9), Nivel = NivelEducativo.Primaria, Grado = 2, Grupo = "A", TutorId = tutores[2].Id },
                new() { Matricula = "SEC2026001", Nombre = "Valeria", Apellidos = "García Torres", FechaNacimiento = new DateTime(2012, 6, 21), Nivel = NivelEducativo.Secundaria, Grado = 2, Grupo = "A", TutorId = tutores[1].Id },
                new() { Matricula = "SEC2026002", Nombre = "Mateo", Apellidos = "Martínez Cruz", FechaNacimiento = new DateTime(2013, 1, 30), Nivel = NivelEducativo.Secundaria, Grado = 1, Grupo = "C", TutorId = tutores[2].Id }
            };
            db.Alumnos.AddRange(alumnos);
            await db.SaveChangesAsync();

            Tamizaje T(Alumno a, Campana c, int dias, Semaforo s, string od, string oi, string? obs = null) => new()
            {
                Folio = $"DEMO-{c.CicloEscolar}-{a.Matricula}",
                AlumnoId = a.Id, CampanaId = c.Id, Fecha = hoy.AddDays(-dias), Semaforo = s,
                AgudezaOjoDerecho = od, AgudezaOjoIzquierdo = oi, Observaciones = obs, Origen = "escritorio"
            };
            var tamizajes = new List<Tamizaje>
            {
                T(alumnos[0], anterior, 400, Semaforo.Amarillo, "20/40", "20/30", "Se acerca mucho al cuaderno."),
                T(alumnos[1], actual, 20, Semaforo.Rojo, "20/70", "20/50", "Entrecierra los ojos al leer el pizarrón."),
                T(alumnos[2], actual, 18, Semaforo.Verde, "20/20", "20/20"),
                T(alumnos[3], actual, 5, Semaforo.Amarillo, "20/30", "20/40", "Repetir tamizaje en 30 días."),
                T(alumnos[0], actual, 15, Semaforo.Verde, "20/20", "20/25"),
                T(alumnos[3], anterior, 390, Semaforo.Verde, "20/20", "20/20"),
                T(alumnos[4], anterior, 395, Semaforo.Rojo, "20/80", "20/60", "Dolor de cabeza frecuente.")
            };
            db.Tamizajes.AddRange(tamizajes);
            await db.SaveChangesAsync();

            var casos = new List<Caso>
            {
                new() { Folio = $"EYS-{hoy.Year}-0001", AlumnoId = alumnos[1].Id, TamizajeId = tamizajes[1].Id, ClinicaId = clinicas[0].Id, Semaforo = Semaforo.Rojo, Estado = EstadoCaso.EnSeguimiento,
                        FechaDeteccion = tamizajes[1].Fecha, AgudezaOjoDerecho = "20/70", AgudezaOjoIzquierdo = "20/50", Observaciones = tamizajes[1].Observaciones, RegistradoPor = "escritorio" },
                new() { Folio = $"EYS-{hoy.Year}-0002", AlumnoId = alumnos[3].Id, TamizajeId = tamizajes[3].Id, Semaforo = Semaforo.Amarillo, Estado = EstadoCaso.Abierto,
                        FechaDeteccion = tamizajes[3].Fecha, AgudezaOjoDerecho = "20/30", AgudezaOjoIzquierdo = "20/40", Observaciones = tamizajes[3].Observaciones, RegistradoPor = "escritorio" },
                new() { Folio = $"EYS-{hoy.Year - 1}-0001", AlumnoId = alumnos[0].Id, TamizajeId = tamizajes[0].Id, ClinicaId = clinicas[3].Id, Semaforo = Semaforo.Amarillo, Estado = EstadoCaso.Cerrado,
                        FechaDeteccion = tamizajes[0].Fecha, FechaAtencion = tamizajes[0].Fecha.AddDays(12), FechaCierre = tamizajes[0].Fecha.AddDays(20),
                        AgudezaOjoDerecho = "20/40", AgudezaOjoIzquierdo = "20/30", Observaciones = "Atendida; se le recetaron lentes.", RegistradoPor = "escritorio" },
                new() { Folio = $"EYS-{hoy.Year - 1}-0002", AlumnoId = alumnos[4].Id, TamizajeId = tamizajes[6].Id, ClinicaId = clinicas[4].Id, Semaforo = Semaforo.Rojo, Estado = EstadoCaso.Cerrado,
                        FechaDeteccion = tamizajes[6].Fecha, FechaAtencion = tamizajes[6].Fecha.AddDays(25), FechaCierre = tamizajes[6].Fecha.AddDays(40),
                        AgudezaOjoDerecho = "20/80", AgudezaOjoIzquierdo = "20/60", RegistradoPor = "escritorio" }
            };
            db.Casos.AddRange(casos);
            await db.SaveChangesAsync();

            db.EventosCaso.AddRange(
                new EventoCaso { CasoId = casos[0].Id, Tipo = TipoEvento.Tamizaje, Fecha = casos[0].FechaDeteccion, Descripcion = "Tamizaje recibido del sistema de escritorio: OD 20/70, OI 20/50. Semáforo rojo.", RegistradoPor = "escritorio" },
                new EventoCaso { CasoId = casos[0].Id, Tipo = TipoEvento.AvisoTutor, Fecha = casos[0].FechaDeteccion, Descripcion = "Se notificó el resultado al tutor por la app.", RegistradoPor = "sistema" },
                new EventoCaso { CasoId = casos[0].Id, Tipo = TipoEvento.Destino, Fecha = hoy.AddDays(-15), Descripcion = "Destino: Óptica Tulancingo Centro (asociada). Se generó el pase médico con QR.", RegistradoPor = "tutor@eyesofter.mx" },
                new EventoCaso { CasoId = casos[1].Id, Tipo = TipoEvento.Tamizaje, Fecha = casos[1].FechaDeteccion, Descripcion = "Tamizaje recibido del sistema de escritorio: OD 20/30, OI 20/40. Semáforo amarillo.", RegistradoPor = "escritorio" },
                new EventoCaso { CasoId = casos[2].Id, Tipo = TipoEvento.Tamizaje, Fecha = casos[2].FechaDeteccion, Descripcion = "Tamizaje: OD 20/40, OI 20/30.", RegistradoPor = "escritorio" },
                new EventoCaso { CasoId = casos[2].Id, Tipo = TipoEvento.Atencion, Fecha = casos[2].FechaAtencion!.Value, Descripcion = "Atención en Centro Oftalmológico Hidalgo (pase con QR). Se indicó corrección óptica.", RegistradoPor = "especialista" },
                new EventoCaso { CasoId = casos[2].Id, Tipo = TipoEvento.Cierre, Fecha = casos[2].FechaCierre!.Value, Descripcion = "Caso cerrado: alumna con lentes.", RegistradoPor = "enfermeria@eyesofter.mx" },
                new EventoCaso { CasoId = casos[3].Id, Tipo = TipoEvento.Tamizaje, Fecha = casos[3].FechaDeteccion, Descripcion = "Tamizaje: OD 20/80, OI 20/60.", RegistradoPor = "escritorio" },
                new EventoCaso { CasoId = casos[3].Id, Tipo = TipoEvento.Cierre, Fecha = casos[3].FechaCierre!.Value, Descripcion = "Caso cerrado tras atención en Centro de Salud.", RegistradoPor = "enfermeria@eyesofter.mx" }
            );
            // El caso 1 (rojo, clínica asociada) ya tiene su pase médico con QR vigente.
            db.Pases.Add(new Pase { CasoId = casos[0].Id, ClinicaId = clinicas[0].Id, Codigo = Servicios.Expedientes.NuevoCodigoPase(), ExpiraEn = DateTime.UtcNow.AddDays(45) });
            db.Atenciones.AddRange(
                new Atencion { CasoId = casos[2].Id, ClinicaId = clinicas[3].Id, Fecha = casos[2].FechaAtencion!.Value, Valoracion = "Miopía leve en ambos ojos.", Indicaciones = "Usar lentes en clase.", CorreccionOptica = true, Origen = OrigenAtencion.PaseQr, RegistradoPor = "especialista" },
                new Atencion { CasoId = casos[3].Id, ClinicaId = clinicas[4].Id, Fecha = casos[3].FechaAtencion!.Value, Valoracion = "Astigmatismo.", CorreccionOptica = true, Origen = OrigenAtencion.Manual, RegistradoPor = "enfermeria@eyesofter.mx" }
            );
            await db.SaveChangesAsync();

            await CrearUsuarioAsync(userManager, logger, "enfermeria@eyesofter.mx", "Laura Méndez (Enfermería)", PasswordDemo, Roles.Enfermeria,
                u => u.Planteles.Add(new UsuarioPlantel { UsuarioId = u.Id, PlantelId = 1 }));
            await CrearUsuarioAsync(userManager, logger, "tutor@eyesofter.mx", "María López Hernández", PasswordDemo, Roles.Tutor, u => u.TutorId = tutores[0].Id);
            await CrearUsuarioAsync(userManager, logger, "alumno@eyesofter.mx", "Diego López Ramírez", PasswordDemo, Roles.Alumno, u => u.AlumnoId = alumnos[1].Id);
            await CrearUsuarioAsync(userManager, logger, "especialista@eyesofter.mx", "Dra. Elena Ruiz (Óptica Tulancingo Centro)", PasswordDemo, Roles.Especialista, u => u.ClinicaId = clinicas[0].Id);

            // Una segunda escuela con su propia enfermera, para ver que ninguna ve a los alumnos de la otra.
            var otra = new Plantel { Nombre = "Escuela Secundaria Técnica 5 (demo)", Clave = "13DST0005Z", Activo = true };
            db.Planteles.Add(otra);
            await db.SaveChangesAsync();
            var tutorOtra = new Tutor { Nombre = "Rosa", Apellidos = "Vargas Díaz", Telefono = "7751112233", Parentesco = "Madre", PlantelId = otra.Id };
            db.Tutores.Add(tutorOtra);
            db.Campanas.Add(new Campana { Nombre = "Campaña de otoño", CicloEscolar = actual.CicloEscolar, FechaInicio = hoy.AddDays(-30), PlantelId = otra.Id });
            await db.SaveChangesAsync();
            // Misma matrícula que un alumno de METANOIA: es válido, la matrícula es única por escuela.
            db.Alumnos.Add(new Alumno { Matricula = "SEC2026001", Nombre = "Andrés", Apellidos = "Vargas Díaz", FechaNacimiento = new DateTime(2012, 2, 11), Nivel = NivelEducativo.Secundaria, Grado = 2, Grupo = "B", TutorId = tutorOtra.Id, PlantelId = otra.Id });
            db.Convenios.Add(new Convenio { PlantelId = 1, ClinicaId = clinicas[0].Id, Descuento = 20, Beneficio = "Examen de la vista sin costo y 20 % de descuento en armazones para alumnos de METANOIA.", Activo = true });
            await db.SaveChangesAsync();
            await CrearUsuarioAsync(userManager, logger, "enfermeria2@eyesofter.mx", "Jorge Salas (Enfermería, Secundaria Técnica 5)", PasswordDemo, Roles.Enfermeria,
                u => u.Planteles.Add(new UsuarioPlantel { UsuarioId = u.Id, PlantelId = otra.Id }));

            logger.LogInformation("Datos de ejemplo creados. Contraseña de las cuentas demo: {Clave}", PasswordDemo);
        }

        private static async Task CrearUsuarioAsync(UserManager<ApplicationUser> userManager, ILogger logger,
            string correo, string nombre, string password, string rol, Action<ApplicationUser>? vincular = null)
        {
            var usuario = await userManager.FindByEmailAsync(correo);
            if (usuario is null)
            {
                usuario = new ApplicationUser { UserName = correo, Email = correo, EmailConfirmed = true, NombreCompleto = nombre };
                vincular?.Invoke(usuario);
                var resultado = await userManager.CreateAsync(usuario, password);
                if (!resultado.Succeeded)
                {
                    logger.LogError("No se pudo crear {Correo}: {Errores}", correo, string.Join("; ", resultado.Errors.Select(e => e.Description)));
                    return;
                }
                logger.LogInformation("Usuario creado: {Correo} ({Rol})", correo, rol);
            }
            if (!await userManager.IsInRoleAsync(usuario, rol)) await userManager.AddToRoleAsync(usuario, rol);
        }
    }
}
