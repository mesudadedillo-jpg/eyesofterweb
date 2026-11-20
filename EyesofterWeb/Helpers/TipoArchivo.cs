namespace EyesofterWeb.Helpers
{
    /// <summary>
    /// Reconoce el tipo del archivo por sus primeros bytes ("número mágico"), no por la
    /// extensión ni por lo que diga el navegador (C-RFNN-05). Mismo criterio que el servidor Ktor.
    /// </summary>
    public static class TipoArchivo
    {
        public const int MaximoBytes = 10 * 1024 * 1024;

        public static string? Detectar(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
            if (b.Length >= 8 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G') return "image/png";
            if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
            if (b.Length >= 5 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F' && b[4] == '-') return "application/pdf";
            return null;
        }
    }
}
