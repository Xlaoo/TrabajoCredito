using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using trabajo.Models;

namespace trabajo.Hubs
{
    [Authorize(Roles = "Analista,Administrador")]
    public class ChatAnalistaHub : Hub
    {
        private readonly UsuarioContext _context;

        public ChatAnalistaHub(UsuarioContext context)
        {
            _context = context;
        }

        public async Task UnirseConversacion(int idAdministrador, int idAnalista)
        {
            if (idAdministrador <= 0)
            {
                idAdministrador = _context.Usuario.FirstOrDefault(x => x.Rol == "Administrador")?.Id ?? 0;
            }
            if (idAnalista <= 0)
            {
                var dni = Context.User?.FindFirst("Dni")?.Value;
                idAnalista = _context.Usuario.FirstOrDefault(x => x.Dni == dni && x.Rol == "Analista")?.Id
                             ?? _context.Usuario.FirstOrDefault(x => x.Rol == "Analista")?.Id ?? 0;
            }

            string grupo = $"admin_{idAdministrador}_analista_{idAnalista}";
            await Groups.AddToGroupAsync(Context.ConnectionId, grupo);
        }

        // ==========================================
        // SEGURIDAD: remitenteRol se determina en el SERVIDOR
        // desde ClaimTypes.Role. No se acepta como parámetro del cliente
        // para evitar suplantación de rol en mensajes del Hub.
        // ==========================================
        public async Task EnviarMensaje(int idAdministrador, int idAnalista, string mensaje)
        {
            if (string.IsNullOrWhiteSpace(mensaje)) return;

            if (idAdministrador <= 0)
            {
                idAdministrador = _context.Usuario.FirstOrDefault(x => x.Rol == "Administrador")?.Id ?? 0;
            }
            if (idAnalista <= 0)
            {
                var dni = Context.User?.FindFirst("Dni")?.Value;
                idAnalista = _context.Usuario.FirstOrDefault(x => x.Dni == dni && x.Rol == "Analista")?.Id
                             ?? _context.Usuario.FirstOrDefault(x => x.Rol == "Analista")?.Id ?? 0;
            }

            // El rol del remitente lo determina el servidor, no el cliente.
            string remitenteRol = Context.User?.FindFirst(ClaimTypes.Role)?.Value
                                  ?? Context.User?.FindFirst("Rol")?.Value
                                  ?? (Context.User?.IsInRole("Administrador") == true ? "Administrador" : "Analista");

            if (string.IsNullOrEmpty(remitenteRol) || remitenteRol == "Desconocido")
            {
                var dniClaim = Context.User?.FindFirst("Dni")?.Value;
                var correoClaim = Context.User?.FindFirst(ClaimTypes.Email)?.Value ?? Context.User?.FindFirst("Correo")?.Value;
                var usuarioDb = _context.Usuario.FirstOrDefault(x => (dniClaim != null && x.Dni == dniClaim) || (correoClaim != null && x.Correo == correoClaim));
                if (usuarioDb != null)
                {
                    remitenteRol = usuarioDb.Rol;
                }
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

            string grupo = $"admin_{idAdministrador}_analista_{idAnalista}";

            await Clients.Group(grupo).SendAsync("RecibirMensaje", new
            {
                idMensaje = nuevoMensaje.IdMensaje,
                remitenteRol = nuevoMensaje.RemitenteRol,
                mensaje = nuevoMensaje.Mensaje,
                fechaEnvio = nuevoMensaje.FechaEnvio.ToString("dd/MM/yyyy hh:mm tt")
            });
        }
    }
}