using trabajo.Models;

namespace trabajo.Models.Patterns.Observer
{
    public class HistorialEstadoObserver : ISolicitudObserver
    {
        private readonly UsuarioContext _context;

        public HistorialEstadoObserver(UsuarioContext context)
        {
            _context = context;
        }

        public void Actualizar(SolicitudCredito solicitud, string motivoCambio)
        {
            var historialExistente = _context.HISTORIAL_ESTADO
                .FirstOrDefault(x => x.SOLICITUD_CREDITO_Id_Solicitud == solicitud.Id_Solicitud);

            if (historialExistente != null)
            {
                historialExistente.EstadoActual = solicitud.Estado;
                historialExistente.MotivoCambio = motivoCambio;
                historialExistente.FechaCambio = DateTime.Now;
            }
            else
            {
                _context.HISTORIAL_ESTADO.Add(new HistorialEstado
                {
                    EstadoActual = solicitud.Estado,
                    MotivoCambio = motivoCambio,
                    FechaCambio = DateTime.Now,
                    SOLICITUD_CREDITO_Id_Solicitud = solicitud.Id_Solicitud
                });
            }
        }
    }
}