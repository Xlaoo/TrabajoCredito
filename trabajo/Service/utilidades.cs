using System;
using System.Security.Cryptography;
using System.Text;

namespace trabajo.Service
{
    public class utilidades
    {
        private const int BCRYPT_WORK_FACTOR = 12;

        /// <summary>
        /// Genera un hash seguro utilizando BCrypt con factor de costo 12 y sal aleatoria independiente.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("La contraseña no puede estar vacía.", nameof(password));
            }
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: BCRYPT_WORK_FACTOR);
        }

        /// <summary>
        /// Determina si un hash almacenado corresponde al formato modular de BCrypt ($2a$, $2b$, $2y$, $2x$).
        /// </summary>
        public static bool EsHashBCrypt(string? hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }
            return hash.Length == 60 && (hash.StartsWith("$2a$") || hash.StartsWith("$2b$") || hash.StartsWith("$2y$") || hash.StartsWith("$2x$"));
        }

        /// <summary>
        /// Comprueba la contraseña contra el hash almacenado.
        /// Soporta tanto BCrypt moderno como SHA-256 legado para permitir migración transparente.
        /// </summary>
        public static bool VerifyPassword(string password, string? hashAlmacenado)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashAlmacenado))
            {
                return false;
            }

            // 1. Si es un hash BCrypt legítimo
            if (EsHashBCrypt(hashAlmacenado))
            {
                try
                {
                    return BCrypt.Net.BCrypt.Verify(password, hashAlmacenado);
                }
                catch
                {
                    return false;
                }
            }

            // 2. Compatibilidad con SHA-256 legado (64 caracteres hexadecimales)
            if (hashAlmacenado.Length == 64)
            {
                string hashSha256 = EncriptarClave(password);
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(hashSha256),
                    Encoding.UTF8.GetBytes(hashAlmacenado)
                );
            }

            return false;
        }

        /// <summary>
        /// Función heredada para generar hash SHA-256.
        /// Se conserva para soporte de tokens OTP temporales de sesión y verificación de hashes legados.
        /// </summary>
        public static string EncriptarClave(string clave)
        {
            if (clave == null) return string.Empty;
            StringBuilder sb = new StringBuilder();

            using (SHA256 hash = SHA256.Create())
            {
                Encoding enc = Encoding.UTF8;

                byte[] result = hash.ComputeHash(enc.GetBytes(clave));

                foreach (byte b in result)
                {
                    sb.Append(b.ToString("x2"));
                }
            }

            return sb.ToString();
        }
    }
}
