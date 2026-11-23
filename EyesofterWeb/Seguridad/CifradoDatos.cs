using System.Security.Cryptography;
using System.Text;

namespace EyesofterWeb.Seguridad
{
    /// <summary>
    /// Cifra los datos sensibles de los alumnos antes de guardarlos (C-RC-04) con AES-256-GCM.
    ///
    /// Formato (el MISMO que usa el servidor Ktor, eyesofter-backend/.../seguridad/CifradoDatos.kt):
    ///   texto:  "v1:" + Base64( nonce[12] || cifrado || etiqueta[16] )
    ///   bytes:  0x45 0x31 ("E1") + nonce[12] || cifrado || etiqueta[16]
    /// GCM además de ocultar el dato detecta si alguien lo modificó en la base.
    ///
    /// La llave (32 bytes en Base64) se lee de EYESOFTER_CLAVE_DATOS y debe ser la misma en la
    /// web y en Ktor. Si se pierde, los datos cifrados ya no se pueden leer: guárdala aparte.
    /// </summary>
    public sealed class CifradoDatos
    {
        public const string VariableEntorno = "EYESOFTER_CLAVE_DATOS";
        private const string PrefijoTexto = "v1:";
        private static readonly byte[] PrefijoBytes = { 0x45, 0x31 };
        private const int TamNonce = 12;
        private const int TamEtiqueta = 16;

        /// <summary>Llave fija SOLO para desarrollo local y pruebas. En producción se exige la variable.</summary>
        public const string ClaveDesarrollo = "ZGVzYXJyb2xsby1zb2xvLW5vLXVzYXItZW4tcHJvZC0=";

        private readonly byte[] _clave;

        public CifradoDatos(string claveBase64)
        {
            _clave = Convert.FromBase64String(claveBase64);
            if (_clave.Length != 32)
                throw new ArgumentException($"{VariableEntorno} debe ser de 32 bytes en Base64 (genera una con: openssl rand -base64 32).");
        }

        public string? Cifrar(string? texto)
        {
            if (texto is null) return null;
            var datos = Encoding.UTF8.GetBytes(texto);
            return PrefijoTexto + Convert.ToBase64String(CifrarBytes(datos));
        }

        /// <summary>Si el valor no tiene el prefijo es un dato anterior al cifrado: se devuelve tal cual.</summary>
        public string? Descifrar(string? valor)
        {
            if (valor is null || !valor.StartsWith(PrefijoTexto, StringComparison.Ordinal)) return valor;
            var paquete = Convert.FromBase64String(valor[PrefijoTexto.Length..]);
            return Encoding.UTF8.GetString(DescifrarBytes(paquete));
        }

        public byte[] CifrarArchivo(byte[] datos) => PrefijoBytes.Concat(CifrarBytes(datos)).ToArray();

        public byte[] DescifrarArchivo(byte[] valor)
        {
            if (valor.Length < 2 || valor[0] != PrefijoBytes[0] || valor[1] != PrefijoBytes[1]) return valor;
            return DescifrarBytes(valor.AsSpan(2).ToArray());
        }

        private byte[] CifrarBytes(byte[] datos)
        {
            var nonce = RandomNumberGenerator.GetBytes(TamNonce);
            var cifrado = new byte[datos.Length];
            var etiqueta = new byte[TamEtiqueta];
            using var aes = new AesGcm(_clave, TamEtiqueta);
            aes.Encrypt(nonce, datos, cifrado, etiqueta);
            return nonce.Concat(cifrado).Concat(etiqueta).ToArray();
        }

        private byte[] DescifrarBytes(byte[] paquete)
        {
            var nonce = paquete.AsSpan(0, TamNonce);
            var etiqueta = paquete.AsSpan(paquete.Length - TamEtiqueta);
            var cifrado = paquete.AsSpan(TamNonce, paquete.Length - TamNonce - TamEtiqueta);
            var claro = new byte[cifrado.Length];
            using var aes = new AesGcm(_clave, TamEtiqueta);
            aes.Decrypt(nonce, cifrado, etiqueta, claro);
            return claro;
        }

        /// <summary>
        /// Huella HMAC-SHA256 (hex en minúsculas) de <paramref name="texto"/> con la llave de datos.
        /// Sirve para guardar algo que solo hay que COMPARAR, como los códigos de recuperación:
        /// aunque alguien lea la base, sin la llave no puede probar los 10^6 códigos posibles.
        /// El servidor Ktor calcula lo mismo (CifradoDatos.firma): "proposito\ntexto".
        /// </summary>
        public string Firma(string proposito, string texto)
        {
            var mensaje = Encoding.UTF8.GetBytes(proposito + "\n" + texto);
            return Convert.ToHexStringLower(HMACSHA256.HashData(_clave, mensaje));
        }

        /// <summary>Lee la llave de la configuración. En producción, sin llave no arranca.</summary>
        public static CifradoDatos DesdeConfiguracion(IConfiguration config, IHostEnvironment entorno, ILogger? log = null)
        {
            var clave = config[VariableEntorno];
            if (string.IsNullOrWhiteSpace(clave))
            {
                if (entorno.IsProduction())
                    throw new InvalidOperationException($"Falta {VariableEntorno}: sin ella no se pueden proteger los datos de los alumnos.");
                log?.LogWarning("{Var} no está definida: se usa la llave de DESARROLLO. No la uses con datos reales.", VariableEntorno);
                clave = ClaveDesarrollo;
            }
            return new CifradoDatos(clave);
        }
    }
}
