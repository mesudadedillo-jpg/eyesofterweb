using System.ComponentModel.DataAnnotations;

namespace EyesofterWeb.Models.ViewModels
{
    public class CerrarCasoViewModel
    {
        public int CasoId { get; set; }
        public string Folio { get; set; } = string.Empty;
        public string Alumno { get; set; } = string.Empty;

        [Required(ErrorMessage = "Escribe el motivo o resultado del cierre.")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "El comentario debe tener entre {2} y {1} caracteres.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Resultado / motivo del cierre")]
        public string Comentario { get; set; } = string.Empty;
    }

    /// <summary>Filtros del panel de casos (W-RFN-03).</summary>
    public class FiltroCasos
    {
        public string? Buscar { get; set; }
        public EstadoCaso? Estado { get; set; }
        public Semaforo? Semaforo { get; set; }
        public NivelEducativo? Nivel { get; set; }
        public int? Grado { get; set; }
        public string? Grupo { get; set; }
        public int? CampanaId { get; set; }
        /// <summary>"abiertos" (no cerrados) o "resueltos" (cerrados).</summary>
        public string? Vista { get; set; }

        public string Descripcion()
        {
            var partes = new List<string>();
            if (Vista == "abiertos") partes.Add("abiertos");
            if (Vista == "resueltos") partes.Add("resueltos");
            if (Estado is not null) partes.Add(Estado.ToString()!);
            if (Semaforo is not null) partes.Add("semáforo " + Semaforo);
            if (Nivel is not null) partes.Add(Nivel.ToString()!);
            if (Grado is not null) partes.Add($"{Grado}°");
            if (!string.IsNullOrEmpty(Grupo)) partes.Add("grupo " + Grupo);
            if (!string.IsNullOrEmpty(Buscar)) partes.Add($"\"{Buscar}\"");
            return partes.Count == 0 ? "Todos los casos" : "Casos: " + string.Join(", ", partes);
        }
    }

    public class DestinoViewModel
    {
        public int CasoId { get; set; }
        public string Folio { get; set; } = string.Empty;
        public string Alumno { get; set; } = string.Empty;

        [Display(Name = "Clínica del directorio")]
        public int? ClinicaId { get; set; }

        [StringLength(160)]
        [Display(Name = "O escribe el nombre de otra clínica")]
        public string? NombreManual { get; set; }
    }

    /// <summary>W-RFN-10: atención en clínica NO asociada a partir del comprobante validado.</summary>
    public class AtencionManualViewModel
    {
        public int CasoId { get; set; }
        public string Folio { get; set; } = string.Empty;
        public string Alumno { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona el comprobante validado.")]
        [Display(Name = "Comprobante validado")]
        public Guid? ComprobanteId { get; set; }

        [Display(Name = "Clínica del directorio")]
        public int? ClinicaId { get; set; }

        [StringLength(160)]
        [Display(Name = "Clínica (si no está en el directorio)")]
        public string? ClinicaNombre { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de atención")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Describe lo que indica el comprobante.")]
        [StringLength(1000, MinimumLength = 5, ErrorMessage = "Escribe entre {2} y {1} caracteres.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Valoración (según la receta o comprobante)")]
        public string Valoracion { get; set; } = string.Empty;

        [StringLength(1000)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Indicaciones para la familia")]
        public string? Indicaciones { get; set; }

        [Display(Name = "¿Se indicó corrección óptica (lentes)?")]
        public bool CorreccionOptica { get; set; } = true;
    }

    /// <summary>Metadatos de un comprobante (sin el archivo, que puede pesar varios MB).</summary>
    public record ComprobanteResumen(Guid Id, TipoComprobante Tipo, string ContentType, int TamanoBytes, DateTime SubidoEn,
        EstadoComprobante Estado, string? RevisadoPor, DateTime? RevisadoEn, string? MotivoRechazo);

    public class CasoDetalleViewModel
    {
        public Caso Caso { get; set; } = null!;
        public List<ComprobanteResumen> Comprobantes { get; set; } = new();
        public Pase? PaseVigente { get; set; }
        public bool PuedeCerrar { get; set; }
        public string? MotivoNoCierre { get; set; }
        public string? UrlPase { get; set; }
    }

    /// <summary>Atención registrada por el especialista al leer el QR (W-RFN-11).</summary>
    public class AtencionQrViewModel
    {
        [Required] public string Codigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de atención")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Describe la valoración.")]
        [StringLength(1000, MinimumLength = 5, ErrorMessage = "Escribe entre {2} y {1} caracteres.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Valoración")]
        public string Valoracion { get; set; } = string.Empty;

        [StringLength(1000)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Indicaciones para la familia (en lenguaje claro)")]
        public string? Indicaciones { get; set; }

        [Display(Name = "¿Se indicó corrección óptica (lentes)?")]
        public bool CorreccionOptica { get; set; }
    }
}
