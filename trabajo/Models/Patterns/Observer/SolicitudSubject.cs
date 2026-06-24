using trabajo.Models;

namespace trabajo.Models.Patterns.Observer
{
    public class SolicitudSubject
    {
        private readonly List<ISolicitudObserver> _observadores = new();

        public void AgregarObservador(ISolicitudObserver observador)
        {
            _observadores.Add(observador);
        }

        public void Notificar(SolicitudCredito solicitud, string motivoCambio)
        {
            foreach (var observador in _observadores)
            {
                observador.Actualizar(solicitud, motivoCambio);
            }
        }
    }
}