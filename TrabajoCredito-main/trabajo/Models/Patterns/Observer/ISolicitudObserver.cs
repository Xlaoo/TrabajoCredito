using trabajo.Models;

namespace trabajo.Models.Patterns.Observer
{
    public interface ISolicitudObserver
    {
        void Actualizar(SolicitudCredito solicitud, string motivoCambio);
    }
}