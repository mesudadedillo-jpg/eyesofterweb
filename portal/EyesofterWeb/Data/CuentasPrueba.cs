using EyesofterWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Data
{
    /// <summary>
    /// Cuentas de prueba permanentes, una por rol, para recorrer la interfaz de cada perfil en
    /// producción sin tocar datos de escuelas reales.
    ///
    /// Se activan con CUENTAS_PRUEBA_PASSWORD (la decide quien despliega; no está en el código ni
    /// en el README). Los correos salen de CUENTAS_PRUEBA_CORREO (o ADMIN_CORREO) con "+rol":
    /// con micorreo@gmail.com quedan micorreo+direccion@gmail.com, micorreo+enfermeria@gmail.com,
    /// micorreo+tutor@gmail.com, micorreo+alumno@gmail.com y micorreo+especialista@gmail.com.
    /// Gmail y Outlook entregan esos correos al buzón original, así que recuperar la contraseña
    /// llega a quien las configuró.
    ///
    /// Viven en una escuela ficticia aparte ("Escuela de prueba"): la Dirección y la Enfermería de
    /// prueba solo ven esa escuela, y el tutor, el alumno y la clínica de prueba solo tienen datos
    /// ficticios. Dirección y Enfermería de prueba también deben activar la verificación en dos pasos.
    ///
    /// Se revisa en cada arranque: si falta algo se vuelve a crear, el pase de prueba se renueva
    /// cuando vence y, si cambia CUENTAS_PRUEBA_PASSWORD, las cuentas toman la nueva contraseña.
    /// </summary>
    public static class CuentasPrueba
    {
        public const string ClaveEscuela = "PRUEBA0001";
        public const string NombreEscuela = "Escuela de prueba (datos ficticios)";
        public const string NombreClinica = "Óptica de prueba EYESOFTER (ficticia)";
        private const string MatriculaHijo = "PRB0001";
        private const string MatriculaHija = "PRB0002";

        public static string Correo(string correoBase, string rol)
        {
            var arroba = correoBase.LastIndexOf('@');
            var usuario = correoBase[..arroba];
            var mas = usuario.IndexOf('+');
            if (mas >= 0) usuario = usuario[..mas];
            return $"{usuario}+{rol}{correoBase[arroba..]}".ToLowerInvariant();
        }

        public static async Task AsegurarAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger logger,
            string correoBase, string password)
        {
            var escuela = await db.Planteles.FirstOrDefaultAsync(p => p.Clave == ClaveEscuela);
            if (escuela is null)
            {
                escuela = new Plantel { Nombre = NombreEscuela, Clave = ClaveEscuela, Activo = true };
                db.Planteles.Add(escuela);
                await db.SaveChangesAsync();
                logger.LogInformation("Escuela de prueba creada (id {Id}).", escuela.Id);
            }

            var clinica = await db.Clinicas.FirstOrDefaultAsync(c => c.Nombre == NombreClinica);
            if (clinica is null)
            {
                clinica = new Clinica
                {
                    Nombre = NombreClinica, Tipo = TipoClinica.Optica, Direccion = "Clínica ficticia para pruebas", Ciudad = "Tulancingo",
                    CodigoPostal = "43600", Latitud = 20.0830, Longitud = -98.3630, Telefono = "0000000000", EsAsociada = true,
                    Horario = "Solo para pruebas del sistema"
                };
                db.Clinicas.Add(clinica);
                await db.SaveChangesAsync();
            }

            var correoTutor = Correo(correoBase, "tutor");
            var tutor = await db.Tutores.FirstOrDefaultAsync(t => t.PlantelId == escuela.Id);
            if (tutor is null)
            {
                tutor = new Tutor { Nombre = "Tutor", Apellidos = "De Prueba", Telefono = "0000000000", Correo = correoTutor, Parentesco = "Madre", PlantelId = escuela.Id };
                db.Tutores.Add(tutor);
                await db.SaveChangesAsync();
            }

            var hijo = await db.Alumnos.FirstOrDefaultAsync(a => a.PlantelId == escuela.Id && a.Matricula == MatriculaHijo);
            if (hijo is null)
            {
                var hoy = DateTime.Today;
                var campana = new Campana { Nombre = "Campaña de prueba", CicloEscolar = $"{hoy.Year}-{hoy.Year + 1}", FechaInicio = hoy.AddDays(-30), PlantelId = escuela.Id };
                db.Campanas.Add(campana);
                hijo = new Alumno { Matricula = MatriculaHijo, Nombre = "Alumno", Apellidos = "De Prueba", FechaNacimiento = new DateTime(2016, 4, 10), Nivel = NivelEducativo.Primaria, Grado = 4, Grupo = "A", TutorId = tutor.Id, PlantelId = escuela.Id };
                var hija = new Alumno { Matricula = MatriculaHija, Nombre = "Alumna", Apellidos = "De Prueba", FechaNacimiento = new DateTime(2018, 9, 3), Nivel = NivelEducativo.Primaria, Grado = 2, Grupo = "B", TutorId = tutor.Id, PlantelId = escuela.Id };
                db.Alumnos.AddRange(hijo, hija);
                await db.SaveChangesAsync();

                var rojo = new Tamizaje { Folio = $"PRB-{campana.CicloEscolar}-{MatriculaHijo}", AlumnoId = hijo.Id, CampanaId = campana.Id, Fecha = hoy.AddDays(-10), Semaforo = Semaforo.Rojo,
                                          AgudezaOjoDerecho = "20/70", AgudezaOjoIzquierdo = "20/50", Observaciones = "Dato ficticio de prueba.", Origen = "escritorio" };
                var amarillo = new Tamizaje { Folio = $"PRB-{campana.CicloEscolar}-{MatriculaHija}", AlumnoId = hija.Id, CampanaId = campana.Id, Fecha = hoy.AddDays(-5), Semaforo = Semaforo.Amarillo,
                                              AgudezaOjoDerecho = "20/30", AgudezaOjoIzquierdo = "20/40", Observaciones = "Dato ficticio de prueba.", Origen = "escritorio" };
                db.Tamizajes.AddRange(rojo, amarillo);
                await db.SaveChangesAsync();

                var casoRojo = new Caso { Folio = $"PRB-{hoy.Year}-0001", AlumnoId = hijo.Id, TamizajeId = rojo.Id, ClinicaId = clinica.Id, Semaforo = Semaforo.Rojo, Estado = EstadoCaso.EnSeguimiento,
                                          FechaDeteccion = rojo.Fecha, AgudezaOjoDerecho = rojo.AgudezaOjoDerecho, AgudezaOjoIzquierdo = rojo.AgudezaOjoIzquierdo, Observaciones = rojo.Observaciones, RegistradoPor = "escritorio" };
                var casoAmarillo = new Caso { Folio = $"PRB-{hoy.Year}-0002", AlumnoId = hija.Id, TamizajeId = amarillo.Id, Semaforo = Semaforo.Amarillo, Estado = EstadoCaso.Abierto,
                                              FechaDeteccion = amarillo.Fecha, AgudezaOjoDerecho = amarillo.AgudezaOjoDerecho, AgudezaOjoIzquierdo = amarillo.AgudezaOjoIzquierdo, Observaciones = amarillo.Observaciones, RegistradoPor = "escritorio" };
                db.Casos.AddRange(casoRojo, casoAmarillo);
                await db.SaveChangesAsync();
                db.EventosCaso.AddRange(
                    new EventoCaso { CasoId = casoRojo.Id, Tipo = TipoEvento.Tamizaje, Fecha = casoRojo.FechaDeteccion, Descripcion = "Tamizaje de prueba: OD 20/70, OI 20/50. Semáforo rojo.", RegistradoPor = "escritorio" },
                    new EventoCaso { CasoId = casoAmarillo.Id, Tipo = TipoEvento.Tamizaje, Fecha = casoAmarillo.FechaDeteccion, Descripcion = "Tamizaje de prueba: OD 20/30, OI 20/40. Semáforo amarillo.", RegistradoPor = "escritorio" });
                await db.SaveChangesAsync();
                logger.LogInformation("Datos ficticios de la escuela de prueba creados.");
            }

            if (!await db.Convenios.AnyAsync(c => c.PlantelId == escuela.Id && c.ClinicaId == clinica.Id))
            {
                db.Convenios.Add(new Convenio { PlantelId = escuela.Id, ClinicaId = clinica.Id, Descuento = 20, Beneficio = "Convenio ficticio de prueba: 20 % de descuento en armazones.", Activo = true });
                await db.SaveChangesAsync();
            }

            // El especialista de prueba siempre tiene un pase vigente que validar.
            var casoConPase = await db.Casos.FirstOrDefaultAsync(c => c.AlumnoId == hijo.Id && c.ClinicaId == clinica.Id && c.Estado != EstadoCaso.Cerrado);
            if (casoConPase is not null && !await db.Pases.AnyAsync(p => p.CasoId == casoConPase.Id && p.Estado == EstadoPase.Vigente && p.ExpiraEn > DateTime.UtcNow))
            {
                db.Pases.Add(new Pase { CasoId = casoConPase.Id, ClinicaId = clinica.Id, Codigo = Servicios.Expedientes.NuevoCodigoPase(), ExpiraEn = DateTime.UtcNow.AddDays(60) });
                await db.SaveChangesAsync();
            }

            var escuelaId = escuela.Id;
            await AsegurarCuentaAsync(userManager, logger, Correo(correoBase, "direccion"), "Dirección de prueba", password, Roles.Administrador,
                u => u.Planteles.Add(new UsuarioPlantel { UsuarioId = u.Id, PlantelId = escuelaId }));
            await AsegurarCuentaAsync(userManager, logger, Correo(correoBase, "enfermeria"), "Enfermería de prueba", password, Roles.Enfermeria,
                u => u.Planteles.Add(new UsuarioPlantel { UsuarioId = u.Id, PlantelId = escuelaId }));
            await AsegurarCuentaAsync(userManager, logger, correoTutor, "Tutor De Prueba", password, Roles.Tutor, u => u.TutorId = tutor.Id);
            await AsegurarCuentaAsync(userManager, logger, Correo(correoBase, "alumno"), "Alumno De Prueba", password, Roles.Alumno, u => u.AlumnoId = hijo.Id);
            await AsegurarCuentaAsync(userManager, logger, Correo(correoBase, "especialista"), "Especialista de prueba", password, Roles.Especialista, u => u.ClinicaId = clinica.Id);
        }

        private static async Task AsegurarCuentaAsync(UserManager<ApplicationUser> userManager, ILogger logger,
            string correo, string nombre, string password, string rol, Action<ApplicationUser> vincular)
        {
            var existente = await userManager.FindByEmailAsync(correo);
            if (existente is null)
            {
                await DbSeeder.CrearUsuarioAsync(userManager, logger, correo, nombre, password, rol, vincular);
                return;
            }
            if (!await userManager.IsInRoleAsync(existente, rol)) await userManager.AddToRoleAsync(existente, rol);
            // CUENTAS_PRUEBA_PASSWORD manda: si se cambió en Railway, la cuenta toma la nueva.
            if (!await userManager.CheckPasswordAsync(existente, password))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(existente);
                var r = await userManager.ResetPasswordAsync(existente, token, password);
                if (r.Succeeded) logger.LogInformation("Cuenta de prueba {Correo}: contraseña actualizada desde CUENTAS_PRUEBA_PASSWORD.", correo);
                else logger.LogError("No se pudo actualizar la contraseña de {Correo}: {Errores}", correo, string.Join("; ", r.Errors.Select(e => e.Description)));
            }
        }
    }
}
