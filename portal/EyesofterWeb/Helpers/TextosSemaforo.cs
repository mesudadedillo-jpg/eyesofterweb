using EyesofterWeb.Models;

namespace EyesofterWeb.Helpers
{
    /// <summary>
    /// Textos para el tutor en lenguaje claro, preventivo y sin diagnósticos (W-RFN-06, C-RC-09).
    /// Son los mismos de la app móvil (EyesPiezas.kt → infoSemaforo) para que el mensaje no cambie.
    /// </summary>
    public static class TextosSemaforo
    {
        public static string Titulo(Semaforo s) => s switch
        {
            Semaforo.Verde => "Saludable",
            Semaforo.Amarillo => "En observación",
            _ => "Atención urgente requerida"
        };

        public static string Explicacion(Semaforo s) => s switch
        {
            Semaforo.Verde => "En la revisión no encontramos señales que necesiten atención. ¡Buenas noticias! Sigan cuidando su vista.",
            Semaforo.Amarillo => "Encontramos algo que conviene revisar con calma. No es una urgencia, pero un especialista debe verlo en las próximas semanas.",
            _ => "Es importante que un especialista revise su vista pronto. Esto no es un diagnóstico: solo indica que no conviene esperar."
        };

        public static string[] Pasos(Semaforo s) => s switch
        {
            Semaforo.Verde => new[]
            {
                "Que descanse la vista de las pantallas cada 20 minutos (regla 20-20-20).",
                "Que lea y haga tareas con buena luz.",
                "Revisión de la vista una vez al año."
            },
            Semaforo.Amarillo => new[]
            {
                "Agenda una revisión con un especialista en las próximas semanas.",
                "Si eliges una clínica asociada, te atienden con el pase del programa.",
                "Cuando te den la receta o compres los lentes, sube la foto en la app o en este portal."
            },
            _ => new[]
            {
                "Agenda una cita en los próximos días.",
                "Busca una clínica asociada cerca de ti en el directorio.",
                "Cuando te den la receta o compres los lentes, sube la foto en la app o en este portal."
            }
        };

        public static string Icono(Semaforo s) => s switch
        {
            Semaforo.Verde => "bi-check-circle-fill",
            Semaforo.Amarillo => "bi-exclamation-triangle-fill",
            _ => "bi-exclamation-octagon-fill"
        };

        public static string Clase(Semaforo s) => s switch
        {
            Semaforo.Verde => "semaforo-verde",
            Semaforo.Amarillo => "semaforo-amarillo",
            _ => "semaforo-rojo"
        };
    }
}
