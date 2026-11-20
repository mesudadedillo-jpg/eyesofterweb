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

    public class TableroViewModel
    {
        public int Tutores { get; set; }
        public int Alumnos { get; set; }
        public int Clinicas { get; set; }
        public int CasosAbiertos { get; set; }
        public int CasosRojos { get; set; }
        public int CasosCerrados { get; set; }
        public List<Caso> UltimosCasos { get; set; } = new();
        public bool BaseDeDatosLista { get; set; } = true;
    }
}
