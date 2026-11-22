namespace EyesofterWeb.Servicios
{
    /// <summary>Sección "Eyesofter" de appsettings.json (o variables Eyesofter__UrlWeb, etc.).</summary>
    public class OpcionesEyesofter
    {
        /// <summary>URL pública del portal; va dentro del QR del pase para abrirlo con cualquier cámara.</summary>
        public string UrlWeb { get; set; } = "http://localhost:5080";

        /// <summary>Servidor de la app móvil (Ktor). Solo informativo en el portal.</summary>
        public string UrlApi { get; set; } = string.Empty;

        /// <summary>Si cambia, todos los tutores vuelven a aceptar el aviso.</summary>
        public string VersionAvisoPrivacidad { get; set; } = "2026-11";

        public int DiasVigenciaPase { get; set; } = 60;
        public int MinutosInactividad { get; set; } = 15;

        /// <summary>
        /// Dirección y Enfermería deben activar la verificación en dos pasos antes de ver datos
        /// (ven información de salud de muchos menores). Eyesofter__Exigir2FAPersonal=false la vuelve opcional.
        /// </summary>
        public bool Exigir2FAPersonal { get; set; } = true;

        /// <summary>
        /// Instalador de la app para Android (APK). Por defecto, el último que publicó el flujo
        /// "Publicar APK" del repositorio de la app en GitHub Releases. Eyesofter__UrlApk lo cambia.
        /// </summary>
        public string UrlApk { get; set; } = "https://github.com/mesudadedillo-jpg/eyesofterandroidfront/releases/latest/download/eyesofter.apk";
    }
}
