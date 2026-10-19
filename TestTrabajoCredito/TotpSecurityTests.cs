using System;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using trabajo.Controllers;

namespace TestTrabajoCredito
{
    public class TotpSecurityTests
    {
        private readonly ITestOutputHelper _output;

        public TotpSecurityTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string InvocarConvertirBase32(byte[] datos)
        {
            var metodo = typeof(LoginController).GetMethod(
                "ConvertirBase32",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(metodo);
            return (string)metodo.Invoke(null, new object[] { datos })!;
        }

        private static byte[] InvocarDecodificarBase32(string texto)
        {
            var metodo = typeof(LoginController).GetMethod(
                "DecodificarBase32",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(metodo);
            return (byte[])metodo.Invoke(null, new object[] { texto })!;
        }

        private static string InvocarGenerarCodigoTotp(byte[] secreto, long contador)
        {
            var metodo = typeof(LoginController).GetMethod(
                "GenerarCodigoTotp",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(metodo);
            return (string)metodo.Invoke(null, new object[] { secreto, contador })!;
        }

        private static bool InvocarValidarCodigoTotp(string secretoBase32, string codigoIngresado)
        {
            var metodo = typeof(LoginController).GetMethod(
                "ValidarCodigoTotp",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(metodo);
            return (bool)metodo.Invoke(null, new object[] { secretoBase32, codigoIngresado })!;
        }

        private static string InvocarFormatearClaveTotp(string clave)
        {
            var metodo = typeof(LoginController).GetMethod(
                "FormatearClaveTotp",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(metodo);
            return (string)metodo.Invoke(null, new object[] { clave })!;
        }

        [Fact]
        public void ProbarTodasLasValidacionesDeTotp()
        {
            var runner = new EscenarioRunner(_output, "Seguridad Criptográfica TOTP (RFC 6238 / Base32)");

            byte[] secretoBytes = Encoding.ASCII.GetBytes("ClaveSecretaCrediPlus20");
            string secretoBase32 = InvocarConvertirBase32(secretoBytes);
            long tiempoActual = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

            // 1. Codificación y decodificación Base32 (Roundtrip)
            runner.Evaluar(
                nombreEscenario: "Codificación y decodificación Base32 (Integridad de datos)",
                datoEntrada: "Texto: 'CrediPlusSeguridad2026'",
                esperado: true,
                accion: () =>
                {
                    byte[] original = Encoding.UTF8.GetBytes("CrediPlusSeguridad2026");
                    string b32 = InvocarConvertirBase32(original);
                    byte[] decodificado = InvocarDecodificarBase32(b32);
                    return Convert.ToBase64String(original) == Convert.ToBase64String(decodificado);
                });

            // 2. Decodificación Base32 con tolerancia a minúsculas y espacios
            runner.Evaluar(
                nombreEscenario: "Decodificación Base32 tolerante a minúsculas y espacios extremos",
                datoEntrada: $"Con espacios y minúsculas",
                esperado: true,
                accion: () =>
                {
                    byte[] muestra = new byte[] { 10, 20, 30, 40, 50 };
                    string b32 = InvocarConvertirBase32(muestra);
                    string alterada = "  " + b32.ToLowerInvariant() + "  ";
                    byte[] decod = InvocarDecodificarBase32(alterada);
                    return Convert.ToBase64String(muestra) == Convert.ToBase64String(decod);
                });

            // 3. Generación de código TOTP: longitud exacta de 6 dígitos numéricos
            runner.Evaluar(
                nombreEscenario: "Generación de código TOTP (Exactamente 6 dígitos numéricos)",
                datoEntrada: "Contador temporal: 1000",
                esperado: true,
                accion: () =>
                {
                    string cod = InvocarGenerarCodigoTotp(secretoBytes, 1000);
                    return cod.Length == 6 && long.TryParse(cod, out _);
                });

            // 4. Código actual en ventana válida
            runner.Evaluar(
                nombreEscenario: "Validación de código TOTP generado en el tiempo actual (Ventana T0)",
                datoEntrada: "Código TOTP para timestamp actual",
                esperado: true,
                accion: () =>
                {
                    string codActual = InvocarGenerarCodigoTotp(secretoBytes, tiempoActual);
                    return InvocarValidarCodigoTotp(secretoBase32, codActual);
                });

            // 5. Tolerancia de desfase de reloj - Ventana previa T-1 (-30 segundos)
            runner.Evaluar(
                nombreEscenario: "Tolerancia de reloj: Código de ventana anterior (T - 1 / 30s de retraso)",
                datoEntrada: "Código de timestamp T - 1",
                esperado: true,
                accion: () =>
                {
                    string codPrevio = InvocarGenerarCodigoTotp(secretoBytes, tiempoActual - 1);
                    return InvocarValidarCodigoTotp(secretoBase32, codPrevio);
                });

            // 6. Tolerancia de desfase de reloj - Ventana posterior T+1 (+30 segundos)
            runner.Evaluar(
                nombreEscenario: "Tolerancia de reloj: Código de ventana siguiente (T + 1 / 30s de adelanto)",
                datoEntrada: "Código de timestamp T + 1",
                esperado: true,
                accion: () =>
                {
                    string codSiguiente = InvocarGenerarCodigoTotp(secretoBytes, tiempoActual + 1);
                    return InvocarValidarCodigoTotp(secretoBase32, codSiguiente);
                });

            // 7. Código fuera de ventana temporal: 2 períodos atrás (-60 segundos)
            runner.Evaluar(
                nombreEscenario: "Rechazo de código TOTP fuera de ventana temporal (T - 2 / 60s atrás)",
                datoEntrada: "Código generado para T - 2",
                esperado: false,
                accion: () =>
                {
                    string codExpirado = InvocarGenerarCodigoTotp(secretoBytes, tiempoActual - 2);
                    return InvocarValidarCodigoTotp(secretoBase32, codExpirado);
                });

            // 8. Código fuera de ventana temporal: 2 períodos adelante (+60 segundos)
            runner.Evaluar(
                nombreEscenario: "Rechazo de código TOTP fuera de ventana temporal futura (T + 2 / 60s adelante)",
                datoEntrada: "Código generado para T + 2",
                esperado: false,
                accion: () =>
                {
                    string codFuturo = InvocarGenerarCodigoTotp(secretoBytes, tiempoActual + 2);
                    return InvocarValidarCodigoTotp(secretoBase32, codFuturo);
                });

            // 9. Código con longitud incorrecta (5 dígitos)
            runner.Evaluar(
                nombreEscenario: "Rechazo de código con longitud insuficiente (5 dígitos)",
                datoEntrada: "12345",
                esperado: false,
                accion: () => InvocarValidarCodigoTotp(secretoBase32, "12345"));

            // 10. Código con longitud incorrecta (7 dígitos)
            runner.Evaluar(
                nombreEscenario: "Rechazo de código con longitud sobrante (7 dígitos)",
                datoEntrada: "1234567",
                esperado: false,
                accion: () => InvocarValidarCodigoTotp(secretoBase32, "1234567"));

            // 11. Código con caracteres no numéricos
            runner.Evaluar(
                nombreEscenario: "Rechazo de código con caracteres alfanuméricos",
                datoEntrada: "12345A",
                esperado: false,
                accion: () => InvocarValidarCodigoTotp(secretoBase32, "12345A"));

            // 12. Formateo de clave secreta Base32 en grupos de 4 caracteres
            runner.Evaluar(
                nombreEscenario: "Formateo legible de clave TOTP (Espaciado cada 4 caracteres)",
                datoEntrada: "JBSWY3DPEE2G64TF (16 caracteres)",
                esperado: "JBSW Y3DP EE2G 64TF",
                accion: () => InvocarFormatearClaveTotp("JBSWY3DPEE2G64TF"));

            runner.VerificarTodosAprobados();
        }
    }
}
