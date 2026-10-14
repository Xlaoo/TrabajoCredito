using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace trabajo.Service
{
    public class TurnstileService : ITurnstileService
    {
        private const string SiteverifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TurnstileService> _logger;

        private readonly string _siteKey;
        private readonly string _secretKey;

        public TurnstileService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<TurnstileService> logger)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(6);
            _configuration = configuration;
            _logger = logger;

            // Leer de variables de entorno (Northflank) o de appsettings.json
            _siteKey = _configuration["TURNSTILE_SITE_KEY"] 
                       ?? _configuration["Turnstile:SiteKey"] 
                       ?? string.Empty;

            _secretKey = _configuration["TURNSTILE_SECRET_KEY"] 
                         ?? _configuration["Turnstile:SecretKey"] 
                         ?? string.Empty;
        }

        public string ObtenerSiteKey()
        {
            return _siteKey;
        }

        public bool EstaConfigurado()
        {
            return !string.IsNullOrWhiteSpace(_siteKey) && !string.IsNullOrWhiteSpace(_secretKey);
        }

        public async Task<TurnstileVerificationResult> ValidarTokenAsync(string? token, string? ipRemota = null)
        {
            var resultado = new TurnstileVerificationResult();

            // 1. Si no hay credenciales configuradas, operar en modo seguro fallback para no bloquear el sistema
            if (!EstaConfigurado())
            {
                _logger.LogWarning("Turnstile no está configurado (faltan TURNSTILE_SITE_KEY o TURNSTILE_SECRET_KEY). Operando en modo fallback permisivo.");
                resultado.Valido = true;
                resultado.ModoSimuladoOFallback = true;
                return resultado;
            }

            // 2. Si las credenciales están configuradas pero el token viene vacío
            if (string.IsNullOrWhiteSpace(token))
            {
                resultado.Valido = false;
                resultado.Mensaje = "Por favor completa la verificación de seguridad (CAPTCHA).";
                return resultado;
            }

            // 3. Validar contra la API oficial de Cloudflare Turnstile
            try
            {
                var parametros = new Dictionary<string, string>
                {
                    { "secret", _secretKey.Trim() },
                    { "response", token.Trim() }
                };

                if (!string.IsNullOrWhiteSpace(ipRemota))
                {
                    parametros.Add("remoteip", ipRemota.Trim());
                }

                using var contenido = new FormUrlEncodedContent(parametros);
                var respuesta = await _httpClient.PostAsync(SiteverifyUrl, contenido);

                if (!respuesta.IsSuccessStatusCode)
                {
                    _logger.LogError("La API de Cloudflare Turnstile devolvió un código HTTP no exitoso: {StatusCode}", respuesta.StatusCode);
                    resultado.Valido = false;
                    resultado.Mensaje = "Error temporal al conectar con el servicio de verificación de seguridad. Intenta nuevamente.";
                    return resultado;
                }

                var cuerpoJson = await respuesta.Content.ReadAsStringAsync();
                var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var respuestaApi = JsonSerializer.Deserialize<TurnstileApiResponse>(cuerpoJson, opciones);

                if (respuestaApi != null && respuestaApi.Success)
                {
                    resultado.Valido = true;
                    return resultado;
                }

                // Error en la verificación
                resultado.Valido = false;
                resultado.CodigosError = respuestaApi?.ErrorCodes ?? Array.Empty<string>();

                if (Array.Exists(resultado.CodigosError, err => err == "timeout-or-duplicate"))
                {
                    resultado.Mensaje = "El token de seguridad ha expirado o ya fue utilizado. Por favor intenta de nuevo.";
                }
                else
                {
                    resultado.Mensaje = "Verificación de seguridad fallida. Por favor confirma que no eres un robot.";
                }

                _logger.LogWarning("Verificación Turnstile rechazada: {Errores}", string.Join(", ", resultado.CodigosError));
                return resultado;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Tiempo de espera agotado al conectar con Cloudflare Turnstile.");
                // En timeout de red, no bloquear indefinidamente al usuario pero registrar advertencia
                resultado.Valido = true;
                resultado.ModoSimuladoOFallback = true;
                resultado.Mensaje = "Verificación omitida por timeout de conexión con el proveedor.";
                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al validar token de Turnstile.");
                resultado.Valido = false;
                resultado.Mensaje = "Ocurrió un error inesperado al procesar la verificación de seguridad.";
                return resultado;
            }
        }

        private class TurnstileApiResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("error-codes")]
            public string[] ErrorCodes { get; set; } = Array.Empty<string>();

            [JsonPropertyName("challenge_ts")]
            public string? ChallengeTs { get; set; }

            [JsonPropertyName("hostname")]
            public string? Hostname { get; set; }
        }
    }
}
