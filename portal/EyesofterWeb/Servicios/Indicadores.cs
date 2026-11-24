using EyesofterWeb.Data;
using EyesofterWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace EyesofterWeb.Servicios
{
    public record FilaEvaluados(NivelEducativo Nivel, int Grado, string Grupo, int Total, int Evaluados)
    {
        public double Porcentaje => Total == 0 ? 0 : Math.Round(100.0 * Evaluados / Total, 1);
    }

    public record SeveridadNivel(NivelEducativo Nivel, int Verde, int Amarillo, int Rojo)
    {
        public int Total => Verde + Amarillo + Rojo;
        public double Pct(int n) => Total == 0 ? 0 : Math.Round(100.0 * n / Total, 1);
    }

    public record IndicadorCampana(string Nombre, string Ciclo, int Evaluados, int Amarillo, int Rojo, int Casos, int Resueltos, double? PromedioDias)
    {
        public double TasaResueltos => Casos == 0 ? 0 : Math.Round(100.0 * Resueltos / Casos, 1);
        public double PctConIndicios => Evaluados == 0 ? 0 : Math.Round(100.0 * (Amarillo + Rojo) / Evaluados, 1);
    }

    public class TableroIndicadores
    {
        public int? CampanaId { get; set; }
        public string Alcance { get; set; } = "Todas las campañas";
        public int TotalAlumnos { get; set; }
        public int AlumnosEvaluados { get; set; }
        public double PorcentajeEvaluados => TotalAlumnos == 0 ? 0 : Math.Round(100.0 * AlumnosEvaluados / TotalAlumnos, 1);
        public List<FilaEvaluados> EvaluadosPorGrupo { get; set; } = new();
        public List<SeveridadNivel> Severidad { get; set; } = new();
        public int CasosTotales { get; set; }
        public int CasosAbiertos { get; set; }
        public int CasosResueltos { get; set; }
        public int CasosRojosAbiertos { get; set; }
        /// <summary>W-RFN-18.</summary>
        public double TasaResueltos => CasosTotales == 0 ? 0 : Math.Round(100.0 * CasosResueltos / CasosTotales, 1);
        /// <summary>W-RFN-17: días promedio entre la detección y la primera atención.</summary>
        public double? PromedioDiasAtencion { get; set; }
        public int ComprobantesPendientes { get; set; }
        public int AvisosSinLeer { get; set; }
        public List<IndicadorCampana> Comparativo { get; set; } = new();
        public List<Campana> Campanas { get; set; } = new();
    }

    /// <summary>Indicadores del dashboard institucional (W-RFN-14 a W-RFN-18).</summary>
    public class Indicadores
    {
        private readonly ApplicationDbContext _db;
        public Indicadores(ApplicationDbContext db) => _db = db;

        public async Task<TableroIndicadores> CalcularAsync(int? campanaId)
        {
            var t = new TableroIndicadores { CampanaId = campanaId };
            t.Campanas = await _db.Campanas.AsNoTracking().OrderByDescending(c => c.FechaInicio).ToListAsync();
            if (campanaId is not null)
            {
                var c = t.Campanas.FirstOrDefault(x => x.Id == campanaId);
                t.Alcance = c is null ? t.Alcance : $"{c.Nombre} ({c.CicloEscolar})";
            }

            var alumnos = await _db.Alumnos.AsNoTracking()
                .Select(a => new { a.Id, a.Nivel, a.Grado, a.Grupo }).ToListAsync();
            var tamizajes = await _db.Tamizajes.AsNoTracking()
                .Where(x => campanaId == null || x.CampanaId == campanaId)
                .Select(x => new { x.Id, x.AlumnoId, x.Semaforo, x.Fecha, x.CampanaId }).ToListAsync();

            // Último tamizaje de cada alumno dentro del alcance.
            var ultimo = tamizajes.GroupBy(x => x.AlumnoId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Fecha).First());

            t.TotalAlumnos = alumnos.Count;
            t.AlumnosEvaluados = alumnos.Count(a => ultimo.ContainsKey(a.Id));
            t.EvaluadosPorGrupo = alumnos
                .GroupBy(a => new { a.Nivel, a.Grado, a.Grupo })
                .OrderBy(g => g.Key.Nivel).ThenBy(g => g.Key.Grado).ThenBy(g => g.Key.Grupo)
                .Select(g => new FilaEvaluados(g.Key.Nivel, g.Key.Grado, g.Key.Grupo, g.Count(), g.Count(a => ultimo.ContainsKey(a.Id))))
                .ToList();

            var nivelDe = alumnos.ToDictionary(a => a.Id, a => a.Nivel);
            t.Severidad = Enum.GetValues<NivelEducativo>().Select(n =>
            {
                var del = ultimo.Values.Where(u => nivelDe.TryGetValue(u.AlumnoId, out var nv) && nv == n).ToList();
                return new SeveridadNivel(n, del.Count(x => x.Semaforo == Semaforo.Verde), del.Count(x => x.Semaforo == Semaforo.Amarillo), del.Count(x => x.Semaforo == Semaforo.Rojo));
            }).ToList();

            var idsTamizajes = tamizajes.Select(x => x.Id).ToHashSet();
            var casos = await _db.Casos.AsNoTracking()
                .Select(c => new { c.Estado, c.Semaforo, c.FechaDeteccion, c.FechaAtencion, c.TamizajeId })
                .ToListAsync();
            if (campanaId is not null) casos = casos.Where(c => c.TamizajeId is int id && idsTamizajes.Contains(id)).ToList();

            t.CasosTotales = casos.Count;
            t.CasosAbiertos = casos.Count(c => c.Estado != EstadoCaso.Cerrado);
            t.CasosResueltos = casos.Count(c => c.Estado == EstadoCaso.Cerrado);
            t.CasosRojosAbiertos = casos.Count(c => c.Estado != EstadoCaso.Cerrado && c.Semaforo == Semaforo.Rojo);
            var dias = casos.Where(c => c.FechaAtencion != null).Select(c => (c.FechaAtencion!.Value - c.FechaDeteccion).TotalDays).ToList();
            t.PromedioDiasAtencion = dias.Count == 0 ? null : Math.Round(dias.Average(), 1);

            t.ComprobantesPendientes = await _db.Comprobantes.CountAsync(c => c.Estado == EstadoComprobante.Pendiente);
            var tutoresVisibles = _db.Tutores.Select(x => x.Id);
            var cuentasTutores = _db.Users.Where(u => u.TutorId != null && tutoresVisibles.Contains(u.TutorId.Value)).Select(u => u.Id);
            t.AvisosSinLeer = await _db.Notificaciones.CountAsync(n => n.Tipo == "RESULTADO_TAMIZAJE" && n.LeidaEn == null && cuentasTutores.Contains(n.UsuarioId));

            t.Comparativo = await ComparativoAsync();
            return t;
        }

        /// <summary>W-RFN-16: los mismos indicadores por campaña, para comparar ciclos escolares.</summary>
        private async Task<List<IndicadorCampana>> ComparativoAsync()
        {
            var campanas = await _db.Campanas.AsNoTracking().OrderBy(c => c.FechaInicio).ToListAsync();
            var tam = await _db.Tamizajes.AsNoTracking().Where(x => x.CampanaId != null)
                .Select(x => new { x.Id, x.AlumnoId, x.CampanaId, x.Semaforo }).ToListAsync();
            var casos = await _db.Casos.AsNoTracking().Where(c => c.TamizajeId != null)
                .Select(c => new { c.TamizajeId, c.Estado, c.FechaDeteccion, c.FechaAtencion }).ToListAsync();

            return campanas.Select(c =>
            {
                var delC = tam.Where(x => x.CampanaId == c.Id).ToList();
                var ids = delC.Select(x => x.Id).ToHashSet();
                var casosC = casos.Where(x => ids.Contains(x.TamizajeId!.Value)).ToList();
                var dias = casosC.Where(x => x.FechaAtencion != null).Select(x => (x.FechaAtencion!.Value - x.FechaDeteccion).TotalDays).ToList();
                return new IndicadorCampana(c.Nombre, c.CicloEscolar,
                    delC.Select(x => x.AlumnoId).Distinct().Count(),
                    delC.Count(x => x.Semaforo == Semaforo.Amarillo), delC.Count(x => x.Semaforo == Semaforo.Rojo),
                    casosC.Count, casosC.Count(x => x.Estado == EstadoCaso.Cerrado),
                    dias.Count == 0 ? null : Math.Round(dias.Average(), 1));
            }).ToList();
        }
    }
}
