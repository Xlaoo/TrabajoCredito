using System;
using Xunit;
using Xunit.Abstractions;
using trabajo.Service;

namespace TestTrabajoCredito
{
    public class PasswordPolicyHelperTests
    {
        private readonly ITestOutputHelper _output;

        public PasswordPolicyHelperTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ProbarTodasLasValidacionesDeContrasena()
        {
            var runner = new EscenarioRunner(_output, "Política y Seguridad de Contraseñas (PasswordPolicyHelper)");

            // 1. Contraseña nula
            runner.Evaluar(
                nombreEscenario: "Contraseña nula",
                datoEntrada: "[null]",
                esperado: "Inválida: La contraseña no puede estar vacía.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar(null);
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 2. Contraseña vacía
            runner.Evaluar(
                nombreEscenario: "Contraseña vacía",
                datoEntrada: "\"\"",
                esperado: "Inválida: La contraseña no puede estar vacía.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 3. Contraseña con solo espacios en blanco
            runner.Evaluar(
                nombreEscenario: "Contraseña con solo espacios",
                datoEntrada: "\"   \"",
                esperado: "Inválida: La contraseña no puede estar vacía.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("   ");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 4. Longitud insuficiente (< 12 caracteres) - 4 caracteres
            runner.Evaluar(
                nombreEscenario: "Contraseña corta (4 caracteres)",
                datoEntrada: "Ab1! (longitud 4)",
                esperado: "Inválida: La contraseña debe tener mínimo 12 caracteres.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("Ab1!");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 5. Longitud insuficiente (< 12 caracteres) - 10 caracteres
            runner.Evaluar(
                nombreEscenario: "Contraseña corta (10 caracteres)",
                datoEntrada: "Abcdefgh1! (longitud 10)",
                esperado: "Inválida: La contraseña debe tener mínimo 12 caracteres.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("Abcdefgh1!");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 6. Longitud insuficiente (< 12 caracteres) - 11 caracteres
            runner.Evaluar(
                nombreEscenario: "Contraseña corta en el límite inferior (11 caracteres)",
                datoEntrada: "Abcdefghi1! (longitud 11)",
                esperado: "Inválida: La contraseña debe tener mínimo 12 caracteres.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("Abcdefghi1!");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 7. Longitud exacta de 12 caracteres y válida
            runner.Evaluar(
                nombreEscenario: "Contraseña en el límite exacto válido (12 caracteres)",
                datoEntrada: "Abcdefgh12!# (longitud 12)",
                esperado: "Válida",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("Abcdefgh12!#");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 8. Contraseña sin mayúsculas
            runner.Evaluar(
                nombreEscenario: "Contraseña sin letras mayúsculas",
                datoEntrada: "password1234!seguro",
                esperado: "Inválida: La contraseña debe incluir al menos una letra mayúscula.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("password1234!seguro");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 9. Contraseña sin minúsculas
            runner.Evaluar(
                nombreEscenario: "Contraseña sin letras minúsculas",
                datoEntrada: "PASSWORD1234!SEGURO",
                esperado: "Inválida: La contraseña debe incluir al menos una letra minúscula.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("PASSWORD1234!SEGURO");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 10. Contraseña sin números
            runner.Evaluar(
                nombreEscenario: "Contraseña sin números",
                datoEntrada: "PasswordSeguroSinNum!",
                esperado: "Inválida: La contraseña debe incluir al menos un número.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("PasswordSeguroSinNum!");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 11. Contraseña sin símbolos
            runner.Evaluar(
                nombreEscenario: "Contraseña sin caracteres especiales o símbolos",
                datoEntrada: "PasswordSeguro123456",
                esperado: "Inválida: La contraseña debe incluir al menos un carácter especial o símbolo (!@#$%&*?_-+).",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("PasswordSeguro123456");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 12-18. Contraseñas comunes o comprometidas que cumplen complejidad
            string[] clavesComunes = new[]
            {
                "Password1234!",
                "Contrasena123!",
                "CrediPlus123!",
                "CrediPlus1234!",
                "Admin1234567!",
                "Bienvenido123!",
                "Password2026!"
            };

            foreach (var claveComun in clavesComunes)
            {
                runner.Evaluar(
                    nombreEscenario: $"Contraseña común bloqueada: {claveComun}",
                    datoEntrada: claveComun,
                    esperado: "Rechazada por común",
                    accion: () =>
                    {
                        var res = PasswordPolicyHelper.Validar(claveComun);
                        return (!res.EsValida && res.EsComunOComprometida)
                            ? "Rechazada por común"
                            : (res.EsValida ? "Aceptada erróneamente" : $"Error: {res.MensajeError}");
                    });
            }

            // 19-20. Contraseñas comunes que además carecen de reglas (debe priorizar regla faltante)
            runner.Evaluar(
                nombreEscenario: "Contraseña común sin mayúscula (prioriza regla de mayúscula)",
                datoEntrada: "password1234!",
                esperado: "Inválida: La contraseña debe incluir al menos una letra mayúscula.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("password1234!");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            runner.Evaluar(
                nombreEscenario: "Contraseña común numérica con símbolo (prioriza regla de mayúscula)",
                datoEntrada: "123456789012!",
                esperado: "Inválida: La contraseña debe incluir al menos una letra mayúscula.",
                accion: () =>
                {
                    var res = PasswordPolicyHelper.Validar("123456789012!");
                    return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                });

            // 21-24. Contraseñas robustas válidas
            string[] clavesValidas = new[]
            {
                "MiClave#2026_Segura",
                "Credi#Plus$2026XyZ",
                "SuperPassword*9876_",
                "AlphaBetaGamma!999"
            };

            foreach (var claveValida in clavesValidas)
            {
                runner.Evaluar(
                    nombreEscenario: $"Contraseña válida y segura: {claveValida.Substring(0, 5)}***",
                    datoEntrada: $"{claveValida.Substring(0, 5)}*** (longitud {claveValida.Length})",
                    esperado: "Válida",
                    accion: () =>
                    {
                        var res = PasswordPolicyHelper.Validar(claveValida);
                        return res.EsValida ? "Válida" : $"Inválida: {res.MensajeError}";
                    });
            }

            runner.VerificarTodosAprobados();
        }
    }
}
