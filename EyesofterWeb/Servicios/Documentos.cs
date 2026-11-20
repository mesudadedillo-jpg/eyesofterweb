using ClosedXML.Excel;
using EyesofterWeb.Helpers;
using EyesofterWeb.Models;
using Microsoft.Extensions.Options;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EyesofterWeb.Servicios
{
    /// <summary>
    /// Documentos descargables:
    ///  - Pase médico con QR y resumen del tamizaje en PDF para imprimir (W-RI-04, C-RFN-05/06).
    ///  - Dashboard y lista de casos en PDF y Excel (W-RI-03).
    /// </summary>
    public class Documentos
    {
        private const string Marino = "#0A1A3F";
        private const string Azul = "#0B47A8";
        private readonly OpcionesEyesofter _opciones;

        public Documentos(IOptions<OpcionesEyesofter> opciones) => _opciones = opciones.Value;

        /// <summary>El QR lleva la dirección del portal: cualquier cámara lo abre en la página de validación.</summary>
        public string UrlPase(string codigo) => $"{_opciones.UrlWeb.TrimEnd('/')}/Pases/Validar?codigo={codigo}";

        public byte[] QrPng(string contenido, int pixelesPorModulo = 10)
        {
            using var generador = new QRCodeGenerator();
            using var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
            return new PngByteQRCode(datos).GetGraphic(pixelesPorModulo);
        }

        /// <summary>Código en grupos de 4 para dictarlo o escribirlo a mano.</summary>
        public static string CodigoLegible(string codigo) =>
            string.Join("-", Enumerable.Range(0, (codigo.Length + 3) / 4).Select(i => codigo.Substring(i * 4, Math.Min(4, codigo.Length - i * 4))));

        public byte[] PdfPase(Pase pase, Caso caso)
        {
            var qr = QrPng(UrlPase(pase.Codigo), 12);
            var alumno = caso.Alumno!;
            return Document.Create(doc => doc.Page(p =>
            {
                Pagina(p);
                p.Content().Column(c =>
                {
                    c.Spacing(10);
                    Encabezado(c, "Pase médico", "Programa de salud visual escolar");
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Column(d =>
                        {
                            d.Spacing(4);
                            Dato(d, "Alumno", alumno.NombreCompleto);
                            Dato(d, "Grado y grupo", alumno.GradoGrupo);
                            Dato(d, "Folio del caso", caso.Folio);
                            Dato(d, "Fecha del tamizaje", caso.FechaDeteccion.ToString("dd/MM/yyyy"));
                            Dato(d, "Resultado", $"{TextosSemaforo.Titulo(caso.Semaforo)} ({caso.Semaforo})");
                            Dato(d, "Clínica asociada", pase.Clinica?.Nombre ?? "");
                            Dato(d, "Dirección", pase.Clinica?.Direccion ?? "");
                            Dato(d, "Válido hasta", pase.ExpiraEn.ToLocalTime().ToString("dd/MM/yyyy"));
                        });
                        r.ConstantItem(170).Column(q =>
                        {
                            q.Item().Image(qr);
                            q.Item().AlignCenter().Text(CodigoLegible(pase.Codigo)).FontSize(10).Bold().LetterSpacing(0.05f);
                        });
                    });
                    c.Item().PaddingTop(8).Background(Colors.Grey.Lighten4).Padding(10).Text(t =>
                    {
                        t.Span("Para la clínica: ").Bold();
                        t.Span("escanee el código con la cámara o escriba el código en el portal EYESOFTER (Pases → Validar). " +
                               "El pase es de un solo uso. Después de la consulta registre la atención en el mismo portal.");
                    });
                    c.Item().Text("Este documento no es un diagnóstico médico.").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            })).GeneratePdf();
        }

        /// <summary>C-RFN-06: fecha, semáforo y observaciones, sin diagnóstico ni QR.</summary>
        public byte[] PdfResumen(Caso caso)
        {
            var alumno = caso.Alumno!;
            return Document.Create(doc => doc.Page(p =>
            {
                Pagina(p);
                p.Content().Column(c =>
                {
                    c.Spacing(10);
                    Encabezado(c, "Resumen del tamizaje visual", "Para presentar en la consulta con el especialista");
                    Dato(c, "Alumno", alumno.NombreCompleto);
                    Dato(c, "Grado y grupo", alumno.GradoGrupo);
                    Dato(c, "Fecha del tamizaje", caso.FechaDeteccion.ToString("dd/MM/yyyy"));
                    Dato(c, "Resultado", $"{TextosSemaforo.Titulo(caso.Semaforo)} ({caso.Semaforo})");
                    Dato(c, "Agudeza visual (Snellen)", $"Ojo derecho {caso.AgudezaOjoDerecho} · Ojo izquierdo {caso.AgudezaOjoIzquierdo}");
                    if (!string.IsNullOrWhiteSpace(caso.Observaciones)) Dato(c, "Observaciones", caso.Observaciones!);
                    if (!string.IsNullOrWhiteSpace(caso.ClinicaDestinoManual) || caso.Clinica is not null)
                        Dato(c, "Clínica de destino", caso.Clinica?.Nombre ?? caso.ClinicaDestinoManual!);
                    c.Item().PaddingTop(8).Text(TextosSemaforo.Explicacion(caso.Semaforo));
                    c.Item().Text("Este resumen es preventivo y no sustituye la valoración de un especialista. " +
                                  "La clínica no está asociada al programa, por lo que no se emite pase con QR.")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            })).GeneratePdf();
        }

        public byte[] PdfTablero(TableroIndicadores t)
        {
            return Document.Create(doc => doc.Page(p =>
            {
                Pagina(p);
                p.Content().Column(c =>
                {
                    c.Spacing(8);
                    Encabezado(c, "Dashboard institucional", t.Alcance);
                    c.Item().Row(r =>
                    {
                        Kpi(r, "Alumnos evaluados", $"{t.PorcentajeEvaluados}%", $"{t.AlumnosEvaluados} de {t.TotalAlumnos}");
                        Kpi(r, "Casos resueltos", $"{t.TasaResueltos}%", $"{t.CasosResueltos} de {t.CasosTotales}");
                        Kpi(r, "Días a la atención", t.PromedioDiasAtencion?.ToString("0.#") ?? "—", "promedio");
                        Kpi(r, "Rojos abiertos", t.CasosRojosAbiertos.ToString(), "urgentes");
                    });
                    Subtitulo(c, "Alumnos evaluados por grado y grupo");
                    c.Item().Table(tb =>
                    {
                        tb.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        Encabezados(tb, "Grado y grupo", "Alumnos", "Evaluados", "%");
                        foreach (var f in t.EvaluadosPorGrupo)
                            Fila(tb, $"{f.Grado}° {f.Grupo} - {f.Nivel}", f.Total.ToString(), f.Evaluados.ToString(), $"{f.Porcentaje}%");
                    });
                    Subtitulo(c, "Severidad: primaria contra secundaria");
                    c.Item().Table(tb =>
                    {
                        tb.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        Encabezados(tb, "Nivel", "Verde", "Amarillo", "Rojo");
                        foreach (var s in t.Severidad)
                            Fila(tb, s.Nivel.ToString(), $"{s.Verde} ({s.Pct(s.Verde)}%)", $"{s.Amarillo} ({s.Pct(s.Amarillo)}%)", $"{s.Rojo} ({s.Pct(s.Rojo)}%)");
                    });
                    if (t.Comparativo.Count > 0)
                    {
                        Subtitulo(c, "Comparativo entre campañas");
                        c.Item().Table(tb =>
                        {
                            tb.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                            Encabezados(tb, "Campaña", "Evaluados", "Con indicios", "Resueltos", "Días prom.");
                            foreach (var k in t.Comparativo)
                                Fila(tb, $"{k.Nombre} ({k.Ciclo})", k.Evaluados.ToString(), $"{k.PctConIndicios}%", $"{k.TasaResueltos}%", k.PromedioDias?.ToString("0.#") ?? "—");
                        });
                    }
                });
            })).GeneratePdf();
        }

        public byte[] ExcelTablero(TableroIndicadores t)
        {
            using var libro = new XLWorkbook();
            var h = libro.AddWorksheet("Resumen");
            h.Cell(1, 1).Value = "Dashboard institucional — " + t.Alcance;
            h.Cell(1, 1).Style.Font.Bold = true;
            var filas = new (string, object)[]
            {
                ("Alumnos", t.TotalAlumnos), ("Evaluados", t.AlumnosEvaluados), ("% evaluados", t.PorcentajeEvaluados),
                ("Casos", t.CasosTotales), ("Casos abiertos", t.CasosAbiertos), ("Casos resueltos", t.CasosResueltos),
                ("Tasa de resueltos (%)", t.TasaResueltos), ("Días promedio a la atención", t.PromedioDiasAtencion ?? 0),
                ("Rojos abiertos", t.CasosRojosAbiertos), ("Comprobantes pendientes", t.ComprobantesPendientes)
            };
            for (var i = 0; i < filas.Length; i++)
            {
                h.Cell(i + 3, 1).Value = filas[i].Item1;
                h.Cell(i + 3, 2).Value = XLCellValue.FromObject(filas[i].Item2);
            }
            h.Columns().AdjustToContents();

            var g = libro.AddWorksheet("Evaluados por grupo");
            g.Cell(1, 1).InsertTable(t.EvaluadosPorGrupo.Select(f => new { f.Nivel, f.Grado, f.Grupo, Alumnos = f.Total, f.Evaluados, Porcentaje = f.Porcentaje }));
            g.Columns().AdjustToContents();

            var s = libro.AddWorksheet("Severidad");
            s.Cell(1, 1).InsertTable(t.Severidad.Select(x => new { Nivel = x.Nivel.ToString(), x.Verde, x.Amarillo, x.Rojo }));
            s.Columns().AdjustToContents();

            var c = libro.AddWorksheet("Comparativo");
            c.Cell(1, 1).InsertTable(t.Comparativo.Select(k => new
            {
                Campana = k.Nombre, Ciclo = k.Ciclo, k.Evaluados, k.Amarillo, k.Rojo, k.Casos, k.Resueltos,
                TasaResueltos = k.TasaResueltos, DiasPromedio = k.PromedioDias ?? 0
            }));
            c.Columns().AdjustToContents();
            return Guardar(libro);
        }

        public byte[] ExcelCasos(IEnumerable<Caso> casos)
        {
            using var libro = new XLWorkbook();
            var h = libro.AddWorksheet("Casos");
            h.Cell(1, 1).InsertTable(casos.Select(c => new
            {
                c.Folio,
                Alumno = c.Alumno?.NombreCompleto,
                Nivel = c.Alumno?.Nivel.ToString(),
                Grado = c.Alumno?.Grado,
                Grupo = c.Alumno?.Grupo,
                Semaforo = c.Semaforo.ToString(),
                Estado = c.Estado.ToString(),
                Deteccion = c.FechaDeteccion,
                Atencion = c.FechaAtencion,
                Cierre = c.FechaCierre,
                Clinica = c.Clinica?.Nombre ?? c.ClinicaDestinoManual
            }));
            h.Columns().AdjustToContents();
            return Guardar(libro);
        }

        public byte[] PdfCasos(IEnumerable<Caso> casos, string filtro)
        {
            return Document.Create(doc => doc.Page(p =>
            {
                Pagina(p);
                p.Size(PageSizes.Letter.Landscape());
                p.Content().Column(c =>
                {
                    Encabezado(c, "Casos de salud visual", filtro);
                    c.Item().Table(tb =>
                    {
                        tb.ColumnsDefinition(cd => { cd.RelativeColumn(1.3f); cd.RelativeColumn(2.5f); cd.RelativeColumn(1.4f); cd.RelativeColumn(); cd.RelativeColumn(1.2f); cd.RelativeColumn(); cd.RelativeColumn(2); });
                        Encabezados(tb, "Folio", "Alumno", "Grado", "Semáforo", "Estado", "Detección", "Clínica");
                        foreach (var x in casos)
                            Fila(tb, x.Folio, x.Alumno?.NombreCompleto ?? "", x.Alumno?.GradoGrupo ?? "", x.Semaforo.ToString(), x.Estado.ToString(),
                                x.FechaDeteccion.ToString("dd/MM/yy"), x.Clinica?.Nombre ?? x.ClinicaDestinoManual ?? "—");
                    });
                });
            })).GeneratePdf();
        }

        // ------------------------------------------------------------------

        private static byte[] Guardar(XLWorkbook libro)
        {
            using var ms = new MemoryStream();
            libro.SaveAs(ms);
            return ms.ToArray();
        }

        private static void Pagina(PageDescriptor p)
        {
            p.Size(PageSizes.Letter);
            p.Margin(36);
            p.DefaultTextStyle(x => x.FontSize(11).FontColor(Marino));
            p.Footer().AlignCenter().Text(t =>
            {
                t.Span("EYESOFTER Suite · Escuela Primaria-Secundaria METANOIA · ").FontSize(8).FontColor(Colors.Grey.Darken1);
                t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }

        private static void Encabezado(ColumnDescriptor c, string titulo, string subtitulo)
        {
            c.Item().Background(Marino).Padding(12).Column(h =>
            {
                h.Item().Text("EYESOFTER").FontColor(Colors.White).FontSize(10).Bold();
                h.Item().Text(titulo).FontColor(Colors.White).FontSize(18).Bold();
                h.Item().Text(subtitulo).FontColor("#C9DCF8").FontSize(10);
            });
        }

        private static void Subtitulo(ColumnDescriptor c, string texto) =>
            c.Item().PaddingTop(6).Text(texto).FontSize(12).Bold().FontColor(Azul);

        private static void Dato(ColumnDescriptor c, string etiqueta, string valor) =>
            c.Item().Text(t => { t.Span(etiqueta + ": ").Bold(); t.Span(valor); });

        private static void Kpi(RowDescriptor r, string titulo, string valor, string pie) =>
            r.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(k =>
            {
                k.Item().Text(titulo).FontSize(9).FontColor(Colors.Grey.Darken2);
                k.Item().Text(valor).FontSize(18).Bold();
                k.Item().Text(pie).FontSize(8).FontColor(Colors.Grey.Darken1);
            });

        private static void Encabezados(TableDescriptor tb, params string[] textos) =>
            tb.Header(h => { foreach (var t in textos) h.Cell().Background("#E3EDFC").Padding(4).Text(t).Bold().FontSize(9); });

        private static void Fila(TableDescriptor tb, params string[] textos)
        {
            foreach (var t in textos) tb.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(t).FontSize(9);
        }
    }
}
