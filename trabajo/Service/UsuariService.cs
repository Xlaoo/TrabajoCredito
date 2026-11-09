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

        public async Task<Usuario?> GetUsuario(string Correo, string Clave)
        {
            var usuario = await _Context.Usuario
                .FirstOrDefaultAsync(u => u.Correo == Correo);

            if (usuario != null && utilidades.VerifyPassword(Clave, usuario.clave))
            {
                // Migración transparente si aún es SHA-256
                if (!utilidades.EsHashBCrypt(usuario.clave))
                {
                    try
                    {
                        usuario.clave = utilidades.HashPassword(Clave);
                        await _Context.SaveChangesAsync();
                    }
                    catch
                    {
                    }
                }
                return usuario;
            }

            return null;
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
