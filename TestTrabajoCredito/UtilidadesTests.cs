using System;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using trabajo.Service;

namespace TestTrabajoCredito
{
    public class UtilidadesTests
    {
        private readonly ITestOutputHelper _output;

        public UtilidadesTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ProbarTodasLasFuncionesCriptograficasSha256()
        {
            var runner = new EscenarioRunner(_output, "Funciones Criptográficas y Hashing Seguro SHA-256 (utilidades)");

            // 1. Hash SHA-256 de cadena vacía (constante RFC 6234 estándar)
            runner.Evaluar(
                nombreEscenario: "Hash SHA-256 de cadena vacía (Estándar RFC 6234)",
                datoEntrada: "\"\"",
                esperado: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                accion: () => utilidades.EncriptarClave(""));

            // 2-4. Formato de 64 caracteres hexadecimales en minúsculas
            string[] muestrasEntrada = new[]
            {
                "123456",
                "MiPassword123!",
                "CrediPlusSeguridad2026"
            };

            foreach (var entrada in muestrasEntrada)
            {
                runner.Evaluar(
                    nombreEscenario: $"Estructura hexadecimal 64 caracteres: '{entrada}'",
                    datoEntrada: entrada,
                    esperado: true,
                    accion: () =>
                    {
                        string hash = utilidades.EncriptarClave(entrada);
                        return hash.Length == 64 && Regex.IsMatch(hash, "^[a-f0-9]{64}$");
                    });
            }

            // 5. Determinismo criptográfico: Misma entrada produce siempre el mismo hash
            runner.Evaluar(
                nombreEscenario: "Determinismo criptográfico (Misma entrada genera hash idéntico)",
                datoEntrada: "ClaveDeterminista#99",
                esperado: true,
                accion: () =>
                {
                    string h1 = utilidades.EncriptarClave("ClaveDeterminista#99");
                    string h2 = utilidades.EncriptarClave("ClaveDeterminista#99");
                    return h1 == h2;
                });

            // 6. Efecto avalancha / No colisión: Entradas diferentes producen hashes distintos
            runner.Evaluar(
                nombreEscenario: "Efecto avalancha (Diferentes entradas generan hashes distintos)",
                datoEntrada: "'ClaveA#2026!' vs 'ClaveB#2026!'",
                esperado: true,
                accion: () =>
                {
                    string hA = utilidades.EncriptarClave("ClaveA#2026!");
                    string hB = utilidades.EncriptarClave("ClaveB#2026!");
                    return hA != hB;
                });

            // 7. BCrypt Hashing: Longitud 60, Prefijo $2a$12$, y Sal Aleatoria
            runner.Evaluar(
                nombreEscenario: "BCrypt: Formato correcto, factor de costo 12 y sal aleatoria independiente",
                datoEntrada: "PasswordSegura#2026!",
                esperado: true,
                accion: () =>
                {
                    string h1 = utilidades.HashPassword("PasswordSegura#2026!");
                    string h2 = utilidades.HashPassword("PasswordSegura#2026!");

                    bool formatoCorrecto = h1.Length == 60 && h1.StartsWith("$2a$12$");
                    bool salAleatoria = h1 != h2; // Dos llamadas nunca deben dar el mismo hash
                    bool esValido = utilidades.EsHashBCrypt(h1) && utilidades.EsHashBCrypt(h2);

                    return formatoCorrecto && salAleatoria && esValido;
                });

            // 8. Detección de Hashes BCrypt vs SHA-256 vs Texto Plano
            runner.Evaluar(
                nombreEscenario: "EsHashBCrypt: Identificación precisa de BCrypt vs SHA-256 y texto plano",
                datoEntrada: "Varios formatos de prueba",
                esperado: true,
                accion: () =>
                {
                    string bcryptHash = utilidades.HashPassword("TestPass123!");
                    string sha256Hash = utilidades.EncriptarClave("TestPass123!");

                    bool validaBcrypt = utilidades.EsHashBCrypt(bcryptHash);
                    bool rechazaSha256 = !utilidades.EsHashBCrypt(sha256Hash);
                    bool rechazaPlano = !utilidades.EsHashBCrypt("TestPass123!");
                    bool rechazaVacio = !utilidades.EsHashBCrypt("") && !utilidades.EsHashBCrypt(null);

                    return validaBcrypt && rechazaSha256 && rechazaPlano && rechazaVacio;
                });

            // 9. Verificación Dual: VerifyPassword con BCrypt y Legacy SHA-256
            runner.Evaluar(
                nombreEscenario: "VerifyPassword: Doble compatibilidad transparente con BCrypt y SHA-256",
                datoEntrada: "Clave de prueba con ambos esquemas",
                esperado: true,
                accion: () =>
                {
                    string claveReal = "CrediPlus@Secure2026";
                    string claveFalsa = "ClaveIncorrecta#99";

                    string bcryptHash = utilidades.HashPassword(claveReal);
                    string sha256Hash = utilidades.EncriptarClave(claveReal);

                    // BCrypt
                    bool bcryptOk = utilidades.VerifyPassword(claveReal, bcryptHash);
                    bool bcryptFalla = !utilidades.VerifyPassword(claveFalsa, bcryptHash);

                    // SHA-256 Legacy
                    bool shaOk = utilidades.VerifyPassword(claveReal, sha256Hash);
                    bool shaFalla = !utilidades.VerifyPassword(claveFalsa, sha256Hash);

                    // Nulos o vacíos
                    bool nuloFalla = !utilidades.VerifyPassword(claveReal, null) && !utilidades.VerifyPassword(claveReal, "");

                    return bcryptOk && bcryptFalla && shaOk && shaFalla && nuloFalla;
                });

            // 10. Generación y Verificación de Hashes de Producción para Administrador y Analista
            runner.Evaluar(
                nombreEscenario: "Generación de hashes independientes para Administrador y Analista (Costo 12)",
                datoEntrada: "CrediPlus#2026",
                esperado: true,
                accion: () =>
                {
                    string hashAdmin = utilidades.HashPassword("CrediPlus#2026");
                    string hashAnalista = utilidades.HashPassword("CrediPlus#2026");

                    bool distintos = hashAdmin != hashAnalista;
                    bool ambosBcrypt = utilidades.EsHashBCrypt(hashAdmin) && utilidades.EsHashBCrypt(hashAnalista);
                    bool verifyAdmin = utilidades.VerifyPassword("CrediPlus#2026", hashAdmin);
                    bool verifyAnalista = utilidades.VerifyPassword("CrediPlus#2026", hashAnalista);

                    _output.WriteLine($"HASH_ADMIN: {hashAdmin}");
                    _output.WriteLine($"HASH_ANALISTA: {hashAnalista}");

                    return distintos && ambosBcrypt && verifyAdmin && verifyAnalista;
                });

            runner.VerificarTodosAprobados();
        }
    }
}
