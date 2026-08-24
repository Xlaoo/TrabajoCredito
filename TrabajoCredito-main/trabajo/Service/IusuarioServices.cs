using trabajo.Models;

namespace trabajo.Service
{
    public interface IusuarioServices
    {
        Task<Usuario> GetUsuario(String Correo, String Clave);
        Task<Usuario> SaveUsuario(Usuario usuario);
       
    }
}
