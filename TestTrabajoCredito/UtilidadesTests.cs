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

            runner.VerificarTodosAprobados();
        }
    }
}
