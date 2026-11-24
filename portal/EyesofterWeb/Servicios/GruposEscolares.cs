using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Servicios
{
    /// <summary>
    /// Reglas de los grupos escolares: inscribir alumnos y pasar de año (un grupo o toda la escuela).
    /// Mantiene copiadas en cada alumno las columnas nivel / grado / grupo, que siguen usando el
    /// servidor de la app y el escritorio.
    /// </summary>
    public class GruposEscolares
    {
        /// <summary>Un grupo no puede pasar de año dos veces en menos de este tiempo (evita dobles clics y repeticiones).</summary>
        public static readonly TimeSpan EsperaEntrePromociones = TimeSpan.FromDays(180);

        private readonly ApplicationDbContext _db;

        public GruposEscolares(ApplicationDbContext db) => _db = db;

        /// <summary>Resultado de pasar de año: cuántos grupos subieron, cuántos egresaron y qué no se pudo.</summary>
        public record Resultado(int GruposPromovidos, int GruposEgresados, int AlumnosMovidos, int AlumnosEgresados, List<string> Avisos)
        {
            public bool Exito => Avisos.Count == 0;
        }

        /// <summary>Copia nivel, grado y letra del grupo al alumno.</summary>
        public static void Sincronizar(Alumno alumno, GrupoEscolar grupo)
        {
            alumno.GrupoEscolarId = grupo.Id == 0 ? alumno.GrupoEscolarId : grupo.Id;
            alumno.GrupoEscolar = grupo;
            alumno.Nivel = grupo.Nivel;
            alumno.Grado = grupo.Grado;
            alumno.Grupo = grupo.Letra;
            alumno.PlantelId = grupo.PlantelId;
            alumno.Egresado = false;
        }

        /// <summary>¿Existe otro grupo ACTIVO con el mismo nivel, grado y letra en la escuela?</summary>
        public Task<GrupoEscolar?> BuscarDuplicadoAsync(int plantelId, NivelEducativo nivel, int grado, string letra, int excluirId = 0) =>
            // Sin filtro por escuela: el índice único es de toda la base, no de lo que ve quien pregunta.
            _db.GruposEscolares.IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Activo && g.Id != excluirId && g.PlantelId == plantelId
                                          && g.Nivel == nivel && g.Grado == grado && g.Letra == letra);

        /// <summary>Inscribe alumnos en el grupo (si venían de otro grupo, se cambian). Devuelve cuántos se movieron.</summary>
        public async Task<int> InscribirAsync(GrupoEscolar grupo, IEnumerable<int> alumnoIds)
        {
            var ids = alumnoIds.Distinct().ToList();
            if (!grupo.Activo || ids.Count == 0) return 0;
            var alumnos = await _db.Alumnos.Where(a => ids.Contains(a.Id) && a.PlantelId == grupo.PlantelId).ToListAsync();
            foreach (var a in alumnos) Sincronizar(a, grupo);
            await _db.SaveChangesAsync();
            return alumnos.Count;
        }

        /// <summary>Aplica a los alumnos del grupo un cambio de nivel, grado o letra hecho al editar el grupo.</summary>
        public async Task SincronizarAlumnosAsync(GrupoEscolar grupo)
        {
            var alumnos = await _db.Alumnos.Where(a => a.GrupoEscolarId == grupo.Id && !a.Egresado).ToListAsync();
            foreach (var a in alumnos) Sincronizar(a, grupo);
        }

        /// <summary>
        /// Pasa de año un grupo: sube un grado con todos sus alumnos y avanza el ciclo escolar.
        /// En el último grado del nivel, el grupo egresa (se cierra y sus alumnos quedan como egresados).
        /// </summary>
        public async Task<Resultado> PromoverAsync(int grupoId, DateTime? ahora = null)
        {
            var avisos = new List<string>();
            var grupo = await _db.GruposEscolares.FirstOrDefaultAsync(g => g.Id == grupoId);
            if (grupo is null) return new Resultado(0, 0, 0, 0, new() { "El grupo no existe." });

            var r = await PromoverUnoAsync(grupo, ahora ?? DateTime.UtcNow, avisos);
            if (avisos.Count == 0) await _db.SaveChangesAsync();
            return r with { Avisos = avisos };
        }

        /// <summary>
        /// Pasa de año TODOS los grupos activos de una escuela (opcionalmente de un solo nivel).
        /// Va del grado más alto al más bajo: así el 6° A egresa antes de que el 5° A ocupe su lugar.
        /// Es todo o nada: si un grupo no puede pasar, no se cambia ninguno.
        /// </summary>
        public async Task<Resultado> PromoverEscuelaAsync(int plantelId, NivelEducativo? nivel = null, DateTime? ahora = null)
        {
            var momento = ahora ?? DateTime.UtcNow;
            var grupos = await _db.GruposEscolares
                .Where(g => g.PlantelId == plantelId && g.Activo && (nivel == null || g.Nivel == nivel))
                .OrderBy(g => g.Nivel).ThenByDescending(g => g.Grado).ThenBy(g => g.Letra)
                .ToListAsync();

            var avisos = new List<string>();
            if (grupos.Count == 0) return new Resultado(0, 0, 0, 0, new() { "No hay grupos activos para pasar de año." });

            // Revisión previa: nada se toca si algún grupo ya pasó de año hace poco.
            foreach (var g in grupos.Where(g => YaPromovido(g, momento)))
                avisos.Add($"{g.Nombre} ya pasó de año el {g.PromovidoEn!.Value.ToLocalTime():dd/MM/yyyy}.");
            if (avisos.Count > 0) return new Resultado(0, 0, 0, 0, avisos);

            var relacional = _db.Database.IsRelational();
            await using var tx = relacional ? await _db.Database.BeginTransactionAsync() : null;

            int promovidos = 0, egresados = 0, movidos = 0, alumnosEgresados = 0;
            foreach (var g in grupos)
            {
                var r = await PromoverUnoAsync(g, momento, avisos);
                if (avisos.Count > 0) break;
                // Se guarda grupo por grupo (en orden) para que el índice único nunca vea dos "5° A" activos.
                await _db.SaveChangesAsync();
                promovidos += r.GruposPromovidos;
                egresados += r.GruposEgresados;
                movidos += r.AlumnosMovidos;
                alumnosEgresados += r.AlumnosEgresados;
            }

            if (avisos.Count > 0)
            {
                if (tx is not null) await tx.RollbackAsync();
                _db.ChangeTracker.Clear();
                return new Resultado(0, 0, 0, 0, avisos);
            }

            if (tx is not null) await tx.CommitAsync();
            return new Resultado(promovidos, egresados, movidos, alumnosEgresados, avisos);
        }

        private static bool YaPromovido(GrupoEscolar g, DateTime ahora) =>
            g.PromovidoEn is { } antes && ahora - antes < EsperaEntrePromociones;

        private async Task<Resultado> PromoverUnoAsync(GrupoEscolar grupo, DateTime ahora, List<string> avisos)
        {
            if (!grupo.Activo)
            {
                avisos.Add($"{grupo.Nombre} ya egresó.");
                return new Resultado(0, 0, 0, 0, avisos);
            }
            if (YaPromovido(grupo, ahora))
            {
                avisos.Add($"{grupo.Nombre} ya pasó de año el {grupo.PromovidoEn!.Value.ToLocalTime():dd/MM/yyyy}. Si fue un error, corrige el grado editando el grupo.");
                return new Resultado(0, 0, 0, 0, avisos);
            }

            var alumnos = await _db.Alumnos.Where(a => a.GrupoEscolarId == grupo.Id && !a.Egresado).ToListAsync();

            if (grupo.EsUltimoGrado)
            {
                grupo.Activo = false;
                grupo.PromovidoEn = ahora;
                foreach (var a in alumnos) a.Egresado = true;
                return new Resultado(0, 1, 0, alumnos.Count, avisos);
            }

            var siguiente = grupo.Grado + 1;
            var ocupado = await BuscarDuplicadoAsync(grupo.PlantelId, grupo.Nivel, siguiente, grupo.Letra, grupo.Id);
            if (ocupado is not null)
            {
                avisos.Add($"Ya existe el grupo {ocupado.Nombre}. Pásalo de año primero (o usa \"Pasar de año a toda la escuela\").");
                return new Resultado(0, 0, 0, 0, avisos);
            }

            grupo.Grado = siguiente;
            grupo.CicloEscolar = GrupoEscolar.CicloSiguiente(grupo.CicloEscolar);
            grupo.PromovidoEn = ahora;
            foreach (var a in alumnos) Sincronizar(a, grupo);
            return new Resultado(1, 0, alumnos.Count, 0, avisos);
        }
    }
}
