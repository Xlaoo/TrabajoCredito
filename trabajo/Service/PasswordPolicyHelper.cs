using System;
using System.Collections.Generic;
using System.Linq;

namespace trabajo.Service
{
    public class PasswordValidationResult
    {
        public bool EsValida { get; set; }
        public bool LongitudMinima { get; set; }
        public bool TieneMayuscula { get; set; }
        public bool TieneMinuscula { get; set; }
        public bool TieneNumero { get; set; }
        public bool TieneSimbolo { get; set; }
        public bool EsComunOComprometida { get; set; }
        public string MensajeError { get; set; } = string.Empty;
    }

    public static class PasswordPolicyHelper
    {
        public const int MinimoCaracteres = 12;

        private static readonly HashSet<string> ContrasenasComunes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Password1234!",
            "Contrasena123!",
            "123456789012!",
            "CrediPlus123!",
            "CrediPlus1234!",
            "Admin1234567!",
            "Bienvenido123!",
            "Password2026!"
        };

        public static PasswordValidationResult Validar(string? clave)
        {
            var resultado = new PasswordValidationResult();

            if (string.IsNullOrWhiteSpace(clave))
            {
                resultado.EsValida = false;
                resultado.MensajeError = "La contraseña no puede estar vacía.";
                return resultado;
            }

            resultado.LongitudMinima = clave.Length >= MinimoCaracteres;
            resultado.TieneMayuscula = clave.Any(char.IsUpper);
            resultado.TieneMinuscula = clave.Any(char.IsLower);
            resultado.TieneNumero = clave.Any(char.IsDigit);
            resultado.TieneSimbolo = clave.Any(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
            resultado.EsComunOComprometida = ContrasenasComunes.Contains(clave.Trim());

            if (!resultado.LongitudMinima)
            {
                resultado.EsValida = false;
                resultado.MensajeError = $"La contraseña debe tener mínimo {MinimoCaracteres} caracteres.";
                return resultado;
            }

            if (!resultado.TieneMayuscula)
            {
                resultado.EsValida = false;
                resultado.MensajeError = "La contraseña debe incluir al menos una letra mayúscula.";
                return resultado;
            }

            if (!resultado.TieneMinuscula)
            {
                resultado.EsValida = false;
                resultado.MensajeError = "La contraseña debe incluir al menos una letra minúscula.";
                return resultado;
            }

            if (!resultado.TieneNumero)
            {
                resultado.EsValida = false;
                resultado.MensajeError = "La contraseña debe incluir al menos un número.";
                return resultado;
            }

            if (!resultado.TieneSimbolo)
            {
                resultado.EsValida = false;
                resultado.MensajeError = "La contraseña debe incluir al menos un carácter especial o símbolo (!@#$%&*?_-+).";
                return resultado;
            }

            if (resultado.EsComunOComprometida)
            {
                resultado.EsValida = false;
                resultado.MensajeError = "Esta contraseña es demasiado común o insegura. Por favor elige otra combinación.";
                return resultado;
            }

            resultado.EsValida = true;
            return resultado;
        }
    }
}
