using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using trabajo.Models;

namespace trabajo.Controllers
{
    [Authorize]
    public class ConexionController : Controller
    {
        private readonly UsuarioContext _context;

        public ConexionController(UsuarioContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Actualizar()
        {
            var correo =
                User.FindFirst(ClaimTypes.Email)?.Value ??
                User.FindFirst("Correo")?.Value ??
                User.FindFirst("correo")?.Value ??
                User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(correo))
            {
                return Unauthorized();
            }

            var usuario = await _context.Usuario
                .FirstOrDefaultAsync(x => x.Correo == correo);

            if (usuario == null)
            {
                return Unauthorized();
            }

            usuario.EstadoActivo = true;
            usuario.UltimaConexion = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                idUsuario = usuario.Id,
                rol = usuario.Rol,
                ultimaConexion = usuario.UltimaConexion
            });
        }
    }
}