using Microsoft.EntityFrameworkCore;
using trabajo.Models;

namespace trabajo.Service
{
    public class UsuariService : IusuarioServices
    {
        private readonly UsuarioContext _Context;

        public UsuariService(UsuarioContext context)
        {
            _Context = context;
        }

        public async Task<Usuario> GetUsuario(string Correo, string Clave)
        {
            // Cambiado a _Context.Usuario (singular)
            Usuario usuario = await _Context.Usuario
                .Where(u => u.Correo == Correo && u.clave == Clave)
                .FirstOrDefaultAsync();
            return usuario;
        }

        public async Task<Usuario> SaveUsuario(Usuario usuario)
        {
            // Cambiado a _Context.Usuario (singular)
            _Context.Usuario.Add(usuario);
            await _Context.SaveChangesAsync();
            return usuario;
        }
    }
}
