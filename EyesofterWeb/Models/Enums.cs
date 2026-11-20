using System.ComponentModel.DataAnnotations;

namespace EyesofterWeb.Models
{
    public enum NivelEducativo
    {
        Primaria,
        Secundaria
    }

    public enum TipoClinica
    {
        [Display(Name = "Óptica")]
        Optica,
        [Display(Name = "Oftalmología")]
        Oftalmologia,
        [Display(Name = "Centro de salud")]
        CentroDeSalud
    }

    /// <summary>Nivel de riesgo visual detectado en el tamizaje.</summary>
    public enum Semaforo
    {
        [Display(Name = "Verde (sin riesgo)")]
        Verde,
        [Display(Name = "Amarillo (vigilar)")]
        Amarillo,
        [Display(Name = "Rojo (canalizar)")]
        Rojo
    }

    public enum EstadoCaso
    {
        Abierto,
        [Display(Name = "En seguimiento")]
        EnSeguimiento,
        Cerrado
    }

    public enum TipoEvento
    {
        Tamizaje,
        [Display(Name = "Aviso al tutor")]
        AvisoTutor,
        Cita,
        [Display(Name = "Comprobante de atención")]
        Comprobante,
        Seguimiento,
        Cierre,
        [Display(Name = "Reapertura")]
        Reapertura
    }
}
