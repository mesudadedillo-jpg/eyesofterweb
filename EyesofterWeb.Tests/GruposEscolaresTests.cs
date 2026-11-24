using EyesofterWeb.Data;
using EyesofterWeb.Models;
using EyesofterWeb.Seguridad;
using EyesofterWeb.Servicios;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EyesofterWeb.Tests
{
    /// <summary>Grupos escolares: inscribir y pasar de año sin editar alumno por alumno.</summary>
    public class GruposEscolaresTests
    {
        private static readonly DateTime Julio2027 = new(2027, 7, 10, 18, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// La migración escrita debe describir exactamente el modelo: si no, la app no arranca
        /// (Migrate() se niega con cambios pendientes). No se conecta a ninguna base.
        /// </summary>
        [Fact]
        public void Las_migraciones_coinciden_con_el_modelo()
        {
            var opciones = new DbContextOptionsBuilder<ApplicationDbContext>();
            BaseDeDatos.Configurar(opciones, "Host=localhost;Database=eyesofter;Username=x;Password=x");
            using var db = new ApplicationDbContext(opciones.Options, new CifradoDatos(CifradoDatos.ClaveDesarrollo),
                new AlcanceEscuela(new HttpContextAccessor()));
            Assert.False(db.Database.HasPendingModelChanges(), "Falta una migración para los cambios del modelo: " + Diferencias(db));
        }

        [Theory]
        [InlineData(2026, 8, "2026-2027")]
        [InlineData(2027, 7, "2026-2027")]
        [InlineData(2027, 1, "2026-2027")]
        public void El_ciclo_escolar_empieza_en_agosto(int anio, int mes, string esperado) =>
            Assert.Equal(esperado, GrupoEscolar.CicloActual(new DateTime(anio, mes, 15)));

        [Fact]
        public void El_ciclo_siguiente_avanza_un_anio() =>
            Assert.Equal("2027-2028", GrupoEscolar.CicloSiguiente("2026-2027"));

        [Fact]
        public async Task Inscribir_copia_nivel_grado_y_letra_del_grupo()
        {
            using var e = new Entorno();
            var (grupo, _) = await CrearGrupoAsync(e, NivelEducativo.Primaria, 2, "B", alumnos: 0);
            var suelto = await CrearAlumnoAsync(e, "SUELTO01", NivelEducativo.Secundaria, 1, "A", grupo: null, plantelId: grupo.PlantelId);

            var n = await new GruposEscolares(e.Db).InscribirAsync(grupo, new[] { suelto.Id });

            Assert.Equal(1, n);
            var a = await e.Db.Alumnos.AsNoTracking().SingleAsync(x => x.Id == suelto.Id);
            Assert.Equal((grupo.Id, NivelEducativo.Primaria, 2, "B"), (a.GrupoEscolarId!.Value, a.Nivel, a.Grado, a.Grupo));
        }

        [Fact]
        public async Task Pasar_de_anio_sube_al_grupo_y_a_todos_sus_alumnos()
        {
            using var e = new Entorno();
            var (grupo, alumnos) = await CrearGrupoAsync(e, NivelEducativo.Primaria, 3, "A", alumnos: 3);

            var r = await new GruposEscolares(e.Db).PromoverAsync(grupo.Id, Julio2027);

            Assert.True(r.Exito, string.Join(" ", r.Avisos));
            Assert.Equal(3, r.AlumnosMovidos);
            var g = await e.Db.GruposEscolares.AsNoTracking().SingleAsync(x => x.Id == grupo.Id);
            Assert.Equal(4, g.Grado);
            Assert.Equal("2027-2028", g.CicloEscolar);
            Assert.All(await e.Db.Alumnos.AsNoTracking().Where(a => a.GrupoEscolarId == grupo.Id).ToListAsync(), a => Assert.Equal(4, a.Grado));
        }

        [Fact]
        public async Task No_pasa_de_anio_dos_veces_seguidas()
        {
            using var e = new Entorno();
            var (grupo, _) = await CrearGrupoAsync(e, NivelEducativo.Primaria, 1, "A", alumnos: 1);
            var servicio = new GruposEscolares(e.Db);

            Assert.True((await servicio.PromoverAsync(grupo.Id, Julio2027)).Exito);
            var segunda = await servicio.PromoverAsync(grupo.Id, Julio2027.AddMinutes(1));

            Assert.False(segunda.Exito);
            Assert.Equal(2, (await e.Db.GruposEscolares.AsNoTracking().SingleAsync(x => x.Id == grupo.Id)).Grado);
        }

        [Fact]
        public async Task El_ultimo_grado_egresa_y_conserva_a_los_alumnos()
        {
            using var e = new Entorno();
            var (grupo, alumnos) = await CrearGrupoAsync(e, NivelEducativo.Secundaria, 3, "C", alumnos: 2);

            var r = await new GruposEscolares(e.Db).PromoverAsync(grupo.Id, Julio2027);

            Assert.True(r.Exito);
            Assert.Equal(1, r.GruposEgresados);
            Assert.False((await e.Db.GruposEscolares.AsNoTracking().SingleAsync(x => x.Id == grupo.Id)).Activo);
            var guardados = await e.Db.Alumnos.AsNoTracking().Where(a => a.GrupoEscolarId == grupo.Id).ToListAsync();
            Assert.Equal(2, guardados.Count);
            Assert.All(guardados, a => Assert.True(a.Egresado));
            Assert.All(guardados, a => Assert.Equal(3, a.Grado));
        }

        [Fact]
        public async Task Un_grupo_no_puede_ocupar_el_lugar_de_otro_activo()
        {
            using var e = new Entorno();
            var (primero, _) = await CrearGrupoAsync(e, NivelEducativo.Primaria, 1, "A", alumnos: 1);
            await CrearGrupoAsync(e, NivelEducativo.Primaria, 2, "A", alumnos: 1, plantelId: primero.PlantelId);

            var r = await new GruposEscolares(e.Db).PromoverAsync(primero.Id, Julio2027);

            Assert.False(r.Exito);
            Assert.Equal(1, (await e.Db.GruposEscolares.AsNoTracking().SingleAsync(x => x.Id == primero.Id)).Grado);
        }

        [Fact]
        public async Task Toda_la_escuela_pasa_de_anio_de_arriba_hacia_abajo()
        {
            using var e = new Entorno();
            var (sexto, _) = await CrearGrupoAsync(e, NivelEducativo.Primaria, 6, "A", alumnos: 2);
            var plantel = sexto.PlantelId;
            var (quinto, _) = await CrearGrupoAsync(e, NivelEducativo.Primaria, 5, "A", alumnos: 2, plantelId: plantel);
            var (primero, _) = await CrearGrupoAsync(e, NivelEducativo.Primaria, 1, "A", alumnos: 1, plantelId: plantel);
            var (secundaria, _) = await CrearGrupoAsync(e, NivelEducativo.Secundaria, 2, "A", alumnos: 1, plantelId: plantel);

            var r = await new GruposEscolares(e.Db).PromoverEscuelaAsync(plantel, ahora: Julio2027);

            Assert.True(r.Exito, string.Join(" ", r.Avisos));
            Assert.Equal(3, r.GruposPromovidos);
            Assert.Equal(1, r.GruposEgresados);
            Assert.Equal(2, r.AlumnosEgresados);
            var grupos = await e.Db.GruposEscolares.AsNoTracking().ToDictionaryAsync(g => g.Id);
            Assert.False(grupos[sexto.Id].Activo);
            Assert.Equal(6, grupos[quinto.Id].Grado);
            Assert.Equal(2, grupos[primero.Id].Grado);
            Assert.Equal(3, grupos[secundaria.Id].Grado);
        }

        [Fact]
        public async Task Si_un_grupo_ya_paso_de_anio_no_se_cambia_ninguno()
        {
            using var e = new Entorno();
            var (cuarto, _) = await CrearGrupoAsync(e, NivelEducativo.Primaria, 4, "B", alumnos: 1);
            var (segundo, _) = await CrearGrupoAsync(e, NivelEducativo.Primaria, 2, "B", alumnos: 1, plantelId: cuarto.PlantelId);
            var servicio = new GruposEscolares(e.Db);
            Assert.True((await servicio.PromoverAsync(segundo.Id, Julio2027)).Exito);

            var r = await servicio.PromoverEscuelaAsync(cuarto.PlantelId, ahora: Julio2027.AddDays(1));

            Assert.False(r.Exito);
            Assert.Equal(4, (await e.Db.GruposEscolares.AsNoTracking().SingleAsync(x => x.Id == cuarto.Id)).Grado);
        }

        /// <summary>Qué difiere entre la última migración y el modelo (para que el mensaje diga qué falta).</summary>
#pragma warning disable EF1001 // API interna de EF: solo para el diagnóstico de la prueba
        private static string Diferencias(ApplicationDbContext db)
        {
            var snapshot = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>().ModelSnapshot?.Model;
            if (snapshot is null) return "sin snapshot";
            snapshot = db.GetService<Microsoft.EntityFrameworkCore.Infrastructure.IModelRuntimeInitializer>().Initialize(snapshot, designTime: true, validationLogger: null);
            var ops = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsModelDiffer>().GetDifferences(
                snapshot.GetRelationalModel(), db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetRelationalModel());
            return string.Join(" | ", ops.Select(o => o switch
            {
                Microsoft.EntityFrameworkCore.Migrations.Operations.AlterColumnOperation c => $"AlterColumn {c.Table}.{c.Name} ({c.OldColumn.ColumnType}/{c.OldColumn.IsNullable}/{c.OldColumn.DefaultValue} -> {c.ColumnType}/{c.IsNullable}/{c.DefaultValue})",
                Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation c => $"AddColumn {c.Table}.{c.Name}",
                Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation c => $"CreateIndex {c.Table}.{c.Name} filtro={c.Filter}",
                Microsoft.EntityFrameworkCore.Migrations.Operations.DropIndexOperation c => $"DropIndex {c.Table}.{c.Name}",
                Microsoft.EntityFrameworkCore.Migrations.Operations.RenameIndexOperation c => $"RenameIndex {c.Table}.{c.Name}->{c.NewName}",
                Microsoft.EntityFrameworkCore.Migrations.Operations.AddForeignKeyOperation c => $"AddFK {c.Table}.{c.Name}",
                Microsoft.EntityFrameworkCore.Migrations.Operations.DropForeignKeyOperation c => $"DropFK {c.Table}.{c.Name}",
                Microsoft.EntityFrameworkCore.Migrations.Operations.InsertDataOperation c => $"InsertData {c.Table}",
                Microsoft.EntityFrameworkCore.Migrations.Operations.UpdateDataOperation c => $"UpdateData {c.Table}",
                _ => o.GetType().Name
            }));
        }
#pragma warning restore EF1001

        // ---------- datos de prueba ----------

        private static async Task<(GrupoEscolar Grupo, List<Alumno> Alumnos)> CrearGrupoAsync(
            Entorno e, NivelEducativo nivel, int grado, string letra, int alumnos, int? plantelId = null)
        {
            var plantel = plantelId ?? await CrearPlantelAsync(e);
            var grupo = new GrupoEscolar { PlantelId = plantel, Nivel = nivel, Grado = grado, Letra = letra, CicloEscolar = "2026-2027" };
            e.Db.GruposEscolares.Add(grupo);
            await e.Db.SaveChangesAsync();

            var lista = new List<Alumno>();
            for (var i = 0; i < alumnos; i++)
                lista.Add(await CrearAlumnoAsync(e, $"G{grupo.Id}A{i}X", nivel, grado, letra, grupo));
            e.Db.ChangeTracker.Clear();
            return (grupo, lista);
        }

        private static async Task<int> CrearPlantelAsync(Entorno e)
        {
            var p = new Plantel { Nombre = "Escuela de prueba " + Guid.NewGuid().ToString("N")[..6] };
            e.Db.Planteles.Add(p);
            await e.Db.SaveChangesAsync();
            return p.Id;
        }

        private static async Task<Alumno> CrearAlumnoAsync(Entorno e, string matricula, NivelEducativo nivel, int grado, string letra, GrupoEscolar? grupo, int? plantelId = null)
        {
            var plantel = grupo?.PlantelId ?? plantelId ?? await CrearPlantelAsync(e);
            var tutor = new Tutor { Nombre = "Laura", Apellidos = "Pérez", Telefono = "7751234567", Parentesco = "Madre", PlantelId = plantel };
            e.Db.Tutores.Add(tutor);
            var alumno = new Alumno
            {
                Matricula = matricula, Nombre = "Niño", Apellidos = "Prueba", Nivel = nivel, Grado = grado, Grupo = letra,
                Tutor = tutor, PlantelId = plantel, GrupoEscolarId = grupo?.Id
            };
            e.Db.Alumnos.Add(alumno);
            await e.Db.SaveChangesAsync();
            return alumno;
        }
    }
}
