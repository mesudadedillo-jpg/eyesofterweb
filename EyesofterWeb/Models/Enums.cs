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
        CentroDeSalud,
        [Display(Name = "Consultorio")]
        Consultorio
    }

    /// <summary>Nivel de riesgo visual detectado en el tamizaje (M-RFN-04 / W-RFN-07).</summary>
    public enum Semaforo
    {
        [Display(Name = "Verde (saludable)")]
        Verde,
        [Display(Name = "Amarillo (en observación)")]
        Amarillo,
        [Display(Name = "Rojo (atención urgente)")]
        Rojo
    }

    public enum EstadoCaso
    {
        Abierto,
        [Display(Name = "En seguimiento")]
        EnSeguimiento,
        Cerrado
    }

    /// <summary>Etapas de la línea de tiempo (W-RFN-02).</summary>
    public enum TipoEvento
    {
        Tamizaje,
        [Display(Name = "Alerta al tutor")]
        AvisoTutor,
        [Display(Name = "Cita agendada")]
        Cita,
        [Display(Name = "Comprobante o receta")]
        Comprobante,
        [Display(Name = "Clínica de destino")]
        Destino,
        [Display(Name = "Atención médica")]
        Atencion,
        Seguimiento,
        [Display(Name = "Caso cerrado")]
        Cierre,
        [Display(Name = "Reapertura")]
        Reapertura
    }

    public enum TipoComprobante
    {
        [Display(Name = "Receta")]
        Receta,
        [Display(Name = "Compra de lentes")]
        ComprobanteLentes
    }

    public enum EstadoComprobante
    {
        Pendiente,
        Aprobado,
        Rechazado
    }

    public enum EstadoPase
    {
        Vigente,
        Usado,
        Cancelado
    }

    public enum OrigenAtencion
    {
        /// <summary>El especialista escaneó el QR en una clínica asociada (W-RFN-11).</summary>
        [Display(Name = "Pase con QR")]
        PaseQr,
        /// <summary>Enfermería la registró a partir del comprobante validado (W-RFN-10).</summary>
        [Display(Name = "Registro manual")]
        Manual
    }

    public enum EstadoCita
    {
        Solicitada,
        Confirmada,
        Atendida,
        Cancelada
    }

    public enum EstadoReporte
    {
        Nuevo,
        [Display(Name = "En revisión")]
        EnRevision,
        Resuelto
    }
}
