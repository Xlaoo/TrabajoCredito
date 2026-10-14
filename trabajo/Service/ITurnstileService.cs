using System.Threading.Tasks;

namespace trabajo.Service
{
    public class TurnstileVerificationResult
    {
        public bool Valido { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string[] CodigosError { get; set; } = System.Array.Empty<string>();
        public bool ModoSimuladoOFallback { get; set; }
    }

    public interface ITurnstileService
    {
        Task<TurnstileVerificationResult> ValidarTokenAsync(string? token, string? ipRemota = null);
        string ObtenerSiteKey();
        bool EstaConfigurado();
    }
}
