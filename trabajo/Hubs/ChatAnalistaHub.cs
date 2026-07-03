using Microsoft.AspNetCore.SignalR;
using trabajo.Models;

namespace trabajo.Hubs
{
    public class ChatAnalistaHub : Hub
    {
        private readonly UsuarioContext _context;

        public ChatAnalistaHub(UsuarioContext context)
        {
            _context = context;
        }

        public async Task UnirseConversacion(int idAdministrador, int idAnalista)
        {
            string grupo = ObtenerNombreGrupo(idAdministrador, idAnalista);
            await Groups.AddToGroupAsync(Context.ConnectionId, grupo);
        }

        public async Task EnviarMensaje(int idAdministrador, int idAnalista, string remitenteRol, string mensaje)
        {
            if (string.IsNullOrWhiteSpace(mensaje))
            {
                return;
            }

            var nuevoMensaje = new MensajeAdminAnalista
            {
                IdAdministrador = idAdministrador,
                IdAnalista = idAnalista,
                RemitenteRol = remitenteRol,
                Mensaje = mensaje.Trim(),
                FechaEnvio = DateTime.Now,
                Leido = false
            };

            _context.MENSAJE_ADMIN_ANALISTA.Add(nuevoMensaje);
            await _context.SaveChangesAsync();

            string grupo = ObtenerNombreGrupo(idAdministrador, idAnalista);

            await Clients.Group(grupo).SendAsync("RecibirMensaje", new
            {
                idMensaje = nuevoMensaje.IdMensaje,
                idAdministrador = nuevoMensaje.IdAdministrador,
                idAnalista = nuevoMensaje.IdAnalista,
                remitenteRol = nuevoMensaje.RemitenteRol,
                mensaje = nuevoMensaje.Mensaje,
                fechaEnvio = nuevoMensaje.FechaEnvio.ToString("dd/MM/yyyy hh:mm tt")
            });
        }

        private static string ObtenerNombreGrupo(int idAdministrador, int idAnalista)
        {
            return $"admin_{idAdministrador}_analista_{idAnalista}";
        }
    }
}