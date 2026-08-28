using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using prestamoscreditos.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;
using trabajo.Models;
using trabajo.Models.Patterns.Observer;
using trabajo.Service;

namespace trabajo.Controllers
{
    [Authorize]
    public class LoginController : Controller
    {
        private readonly IusuarioServices _UsuarioService;
        private readonly UsuarioContext _Context;
        private readonly ServicioEmbeddingVoz _ServicioEmbeddingVoz;
        private static string codigoGlobal = "";
        private readonly EmailService _emailService = new EmailService();
        private static string codigoLogin = "";
        private static string dniLoginPendiente = "";

        // Hora en la que vence el código de inicio de sesión
        private static DateTime codigoLoginExpira = DateTime.MinValue;

        public LoginController(IusuarioServices usuarioService, UsuarioContext context, ServicioEmbeddingVoz servicioEmbeddingVoz)
        {
            _UsuarioService = usuarioService;
            _Context = context;
            _ServicioEmbeddingVoz = servicioEmbeddingVoz;
        }

        [AllowAnonymous]
        public IActionResult PantallaPrincipal()
        {
            var comentarios = (
                from c in _Context.ComentarioClientes
                join u in _Context.Usuario
                    on c.Usuario_Id equals u.Id
                orderby c.FechaComentario descending
                select new ComentarioClienteViewModel
                {
                    NombreCompleto = u.Nombre + " " + u.Apellido,
                    Comentario = c.Comentario,
                    Calificacion = c.Calificacion
                }
            ).ToList();

            return View(comentarios);
        }
        public async Task<IActionResult> CerrarSesion()
        {
            string dni = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (usuario != null)
            {
                usuario.EstadoActivo = false;
                usuario.UltimaConexion = DateTime.Now;
                _Context.SaveChanges();
            }
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            return RedirectToAction("PantallaPrincipal", "Login");
        }

        [AllowAnonymous]
        public IActionResult Registro()
        {
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Registro(
    Usuario usuario,
    string confirmarClave,
    string codigoVerificacion,
    IFormFile? audio)
        {
            if (!Regex.IsMatch(usuario.Dni, @"^\d{8}$"))
            {
                ViewData["mensaje"] = "El DNI debe tener exactamente 8 números.";
                return View(usuario);
            }

            if (!Regex.IsMatch(usuario.Celular, @"^9\d{8}$"))
            {
                ViewData["mensaje"] = "El celular debe tener 9 números y empezar con 9.";
                return View(usuario);
            }

            if (!Regex.IsMatch(usuario.Correo, @"^[A-Za-z0-9._%+-]+@gmail\.com$"))
            {
                ViewData["mensaje"] = "El correo debe ser Gmail.";
                return View(usuario);
            }
            if (string.IsNullOrWhiteSpace(usuario.Genero))
            {
                ViewData["mensaje"] = "Debe seleccionar un género.";
                return View(usuario);
            }

            if (!Regex.IsMatch(usuario.clave, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$"))
            {
                ViewData["mensaje"] = "La contraseña debe tener mínimo 6 caracteres, una mayúscula, una minúscula y un número.";
                return View(usuario);
            }

            if (usuario.clave != confirmarClave)
            {
                ViewData["mensaje"] = "Las contraseñas no coinciden.";
                return View(usuario);
            }

            bool existe = _Context.Usuario.Any(x =>
                x.Dni == usuario.Dni ||
                x.Celular == usuario.Celular ||
                x.Correo == usuario.Correo
            );

            if (existe)
            {
                ViewData["mensaje"] = "El DNI, celular o correo ya está registrado.";
                return View(usuario);
            }
            if (audio == null || audio.Length == 0)
            {
                ViewData["mensaje"] = "Debes realizar y enviar una grabación de audio por motivos de seguridad.";
                return View(usuario);
            }
            if (audio.Length > 5 * 1024 * 1024)
            {
                ViewData["mensaje"] = "El audio no puede superar los 5 MB.";
                return View(usuario);
            }
            var tiposAudioPermitidos = new[]
 {
    "audio/webm",
    "audio/wav",
    "audio/mpeg",
    "audio/mp4",
    "audio/ogg"
};

            bool audioValido = tiposAudioPermitidos.Any(tipo =>
                audio.ContentType.StartsWith(tipo, StringComparison.OrdinalIgnoreCase)
            );

            if (!audioValido)
            {
                ViewData["mensaje"] = "El archivo enviado no es un audio válido.";
                return View(usuario);
            }
            if (codigoVerificacion != codigoGlobal)
            {
                ViewData["mensaje"] = "El código de verificación es incorrecto.";
                return View(usuario);
            }
            string carpetaAudio = Path.Combine(
    Directory.GetCurrentDirectory(),
    "wwwroot",
    "audiosRegistro"
);

            if (!Directory.Exists(carpetaAudio))
            {
                Directory.CreateDirectory(carpetaAudio);
            }

            string nombreAudio =
                Guid.NewGuid().ToString() +
                Path.GetExtension(audio.FileName);

            string rutaAudio =
                Path.Combine(carpetaAudio, nombreAudio);

            using (var stream = new FileStream(rutaAudio, FileMode.Create))
            {
                await audio.CopyToAsync(stream);
            }
            usuario.AudioRegistro = "/audiosRegistro/" + nombreAudio;

            float[] embedding = await _ServicioEmbeddingVoz.GenerarEmbeddingDesdeAudio(audio);

            if (embedding == null || embedding.Length != 192)
            {
                ViewData["mensaje"] = "No se pudo generar correctamente la identificación de voz.";
                return View(usuario);
            }

            usuario.EmbeddingVoz = new byte[embedding.Length * sizeof(float)];

            Buffer.BlockCopy(
                embedding,
                0,
                usuario.EmbeddingVoz,
                0,
                usuario.EmbeddingVoz.Length
            );
            usuario.clave = utilidades.EncriptarClave(usuario.clave);
            usuario.Rol = "Cliente";
            usuario.FechaRegistro = DateTime.Now;
            usuario.EstadoActivo = false;
            usuario.UltimaConexion = DateTime.Now;

            Usuario usuarioCreado = await _UsuarioService.SaveUsuario(usuario);

            if (usuarioCreado.Id > 0)
            {
                TempData["Mensaje"] = "Usuario registrado exitosamente";
                return RedirectToAction("IniciarSesion", "Login");
            }

            ViewData["mensaje"] = "No se pudo crear el usuario.";
            return View(usuario);
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult IniciarSesion()
        {
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public IActionResult IniciarSesion(string dni, string clave)
        {
            try
            {
                // ==========================================
                // VALIDAR DNI
                // ==========================================

                if (string.IsNullOrWhiteSpace(dni) ||
                    !Regex.IsMatch(dni, @"^\d{8}$"))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El DNI debe tener exactamente 8 números."
                    });
                }

                // ==========================================
                // VERIFICAR SI EL DNI EXISTE
                // ==========================================

                bool dniExiste =
                    _Context.Usuario.Any(x => x.Dni == dni);

                if (!dniExiste)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No estás registrado. Primero debes crear una cuenta."
                    });
                }

                // ==========================================
                // VERIFICAR DNI + CONTRASEÑA
                // ==========================================

                string claveEncriptada =
                    utilidades.EncriptarClave(clave);

                Usuario usuarioEncontrado =
                    _Context.Usuario.FirstOrDefault(x =>
                        x.Dni == dni &&
                        x.clave == claveEncriptada
                    );

                if (usuarioEncontrado == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La contraseña es incorrecta."
                    });
                }

                // ==========================================
                // GUARDAR USUARIO PENDIENTE
                // ==========================================

                dniLoginPendiente = usuarioEncontrado.Dni;

                // ==========================================
                // VERIFICAR REGISTRO DE VOZ
                // ==========================================

                bool tieneRegistroVoz =
                    !string.IsNullOrWhiteSpace(usuarioEncontrado.AudioRegistro);

                // ==========================================
                // NO TIENE REGISTRO DE VOZ
                // ==========================================

                if (!tieneRegistroVoz)
                {
                    return Json(new
                    {
                        ok = true,
                        necesitaRegistroVoz = true,
                        tieneRegistroVoz = false,
                        mensaje =
                            "Para continuar debes registrar tu voz por motivos de seguridad."
                    });
                }

                // ==========================================
                // YA TIENE REGISTRO DE VOZ
                // ==========================================

                return Json(new
                {
                    ok = true,
                    necesitaRegistroVoz = false,
                    tieneRegistroVoz = true,
                    mostrarVerificacion = true,
                    correo = usuarioEncontrado.Correo,

                    // Frase de seguridad guardada en la BD
                    fraseVoz = usuarioEncontrado.FraseVoz
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "Error del servidor: " + ex.Message
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RegistrarVozLogin(IFormFile? audio)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dniLoginPendiente))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La sesión de seguridad ha expirado. Inicia sesión nuevamente."
                    });
                }

                if (audio == null || audio.Length == 0)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Debes realizar una grabación de voz."
                    });
                }

                if (audio.Length > 5 * 1024 * 1024)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El audio no puede superar los 5 MB."
                    });
                }

                var tiposAudioPermitidos = new[]
                {
            "audio/webm",
            "audio/wav",
            "audio/mpeg",
            "audio/mp4",
            "audio/ogg"
        };

                bool audioValido = tiposAudioPermitidos.Any(tipo =>
                    audio.ContentType.StartsWith(
                        tipo,
                        StringComparison.OrdinalIgnoreCase
                    )
                );

                if (!audioValido)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El archivo enviado no es un audio válido."
                    });
                }

                // ==========================================
                // BUSCAR USUARIO
                // ==========================================

                Usuario usuario = _Context.Usuario.FirstOrDefault(
                    x => x.Dni == dniLoginPendiente
                );

                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró el usuario."
                    });
                }

                // ==========================================
                // LEER AUDIO COMO BYTES
                // ==========================================

                byte[] bytesAudio;

                using (var memoryStream = new MemoryStream())
                {
                    await audio.CopyToAsync(memoryStream);
                    bytesAudio = memoryStream.ToArray();
                }

                // ==========================================
                // CREAR CARPETA
                // ==========================================

                string carpetaAudio = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "audiosRegistro"
                );

                if (!Directory.Exists(carpetaAudio))
                {
                    Directory.CreateDirectory(carpetaAudio);
                }

                // ==========================================
                // CREAR NOMBRE DEL AUDIO
                // ==========================================

                string nombreAudio =
                    Guid.NewGuid().ToString() + ".webm";

                string rutaAudio =
                    Path.Combine(carpetaAudio, nombreAudio);

                // ==========================================
                // GUARDAR AUDIO EN ARCHIVO
                // ==========================================

                await System.IO.File.WriteAllBytesAsync(
                    rutaAudio,
                    bytesAudio
                );

                // ==========================================
                // GUARDAR INFORMACIÓN EN BASE DE DATOS
                // ==========================================

                usuario.AudioRegistro =
                    "/audiosRegistro/" + nombreAudio;

                // IMPORTANTE
                // Guardamos los bytes del audio
                // en EmbeddingVoz.
                usuario.EmbeddingVoz = bytesAudio;

                _Context.Usuario.Update(usuario);

                await _Context.SaveChangesAsync();

                return Json(new
                {
                    ok = true,
                    mensaje = "Registro de voz realizado correctamente."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "No se pudo registrar la voz: " + ex.Message
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> VerificarVozLogin(IFormFile? audio)
        {
            try
            {
                // ==========================================
                // COMPROBAR SESIÓN PENDIENTE
                // ==========================================

                if (string.IsNullOrWhiteSpace(dniLoginPendiente))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La sesión de seguridad ha expirado. Inicia sesión nuevamente."
                    });
                }


                // ==========================================
                // COMPROBAR AUDIO
                // ==========================================

                if (audio == null || audio.Length == 0)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se recibió ninguna grabación de voz."
                    });
                }


                // ==========================================
                // TAMAÑO MÁXIMO
                // ==========================================

                if (audio.Length > 5 * 1024 * 1024)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El audio no puede superar los 5 MB."
                    });
                }


                // ==========================================
                // BUSCAR USUARIO
                // ==========================================

                Usuario usuario =
                    _Context.Usuario.FirstOrDefault(
                        x => x.Dni == dniLoginPendiente
                    );


                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró el usuario."
                    });
                }


                // ==========================================
                // COMPROBAR QUE TIENE VOZ REGISTRADA
                // ==========================================

                if (string.IsNullOrWhiteSpace(usuario.AudioRegistro))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No tienes una voz registrada."
                    });
                }
                if (usuario.EmbeddingVoz == null || usuario.EmbeddingVoz.Length == 0)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El usuario tiene registrada la ruta de voz, pero no tiene los datos de voz guardados."
                    });
                }

                // ==========================================
                // LEER AUDIO ACTUAL
                // ==========================================

                byte[] audioActual;

                using (var memoryStream = new MemoryStream())
                {
                    await audio.CopyToAsync(memoryStream);

                    audioActual = memoryStream.ToArray();
                }


                // ==========================================
                // COMPROBAR QUE SE RECIBIÓ AUDIO
                // ==========================================

                if (audioActual.Length == 0)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La grabación recibida está vacía."
                    });
                }


                // ==========================================
                // MOSTRAR INFORMACIÓN EN CONSOLA
                // PARA PRUEBAS
                // ==========================================

                Console.WriteLine(
                    "=========================================="
                );

                Console.WriteLine(
                    "VERIFICACIÓN DE VOZ"
                );

                Console.WriteLine(
                    "DNI: " + usuario.Dni
                );

                Console.WriteLine(
                    "Audio registrado: " + usuario.AudioRegistro
                );

                Console.WriteLine(
                    "Tamaño audio actual: " +
                    audioActual.Length +
                    " bytes"
                );

                Console.WriteLine(
                    "=========================================="
                );


                // ==========================================
                // IMPORTANTE
                // ==========================================
                //
                // AQUÍ TODAVÍA NO HACEMOS COMPARACIÓN
                // BIOMÉTRICA DE VOZ.
                //
                // Solo comprobamos que:
                //
                // 1. El usuario existe
                // 2. Tiene voz registrada
                // 3. Se recibió un audio nuevo
                //
                // ==========================================
                // ==========================================
                // PRUEBA DE AUDIO REGISTRADO
                // ==========================================

                Console.WriteLine("==========================================");
                Console.WriteLine("COMPARACIÓN DE AUDIO - PRUEBA");
                Console.WriteLine("DNI: " + usuario.Dni);

                Console.WriteLine(
                    "Audio registrado: " +
                    usuario.AudioRegistro
                );

                Console.WriteLine(
                    "Bytes del audio nuevo: " +
                    audioActual.Length
                );

                Console.WriteLine(
                    "Bytes guardados en EmbeddingVoz: " +
                    (usuario.EmbeddingVoz?.Length ?? 0)
                );

                Console.WriteLine("==========================================");

                // ==========================================
                // CREAR AUTENTICACIÓN
                // ==========================================

                List<Claim> claims =
                    new List<Claim>()
                    {
                new Claim(
                    ClaimTypes.Name,
                    usuario.Nombre
                ),

                new Claim(
                    "Apellido",
                    usuario.Apellido
                ),

                new Claim(
                    "Dni",
                    usuario.Dni
                ),

                new Claim(
                    "Celular",
                    usuario.Celular
                ),

                new Claim(
                    "Correo",
                    usuario.Correo
                ),

                new Claim(
                    ClaimTypes.Role,
                    usuario.Rol
                )
                    };


                ClaimsIdentity claimsIdentity =
                    new ClaimsIdentity(
                        claims,
                        CookieAuthenticationDefaults.AuthenticationScheme
                    );


                AuthenticationProperties properties =
                    new AuthenticationProperties
                    {
                        AllowRefresh = true
                    };


                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    properties
                );


                // ==========================================
                // ACTUALIZAR USUARIO
                // ==========================================

                usuario.EstadoActivo = true;

                usuario.UltimaConexion = DateTime.Now;


                await _Context.SaveChangesAsync();


                // ==========================================
                // LIMPIAR DNI PENDIENTE
                // ==========================================

                dniLoginPendiente = "";


                // ==========================================
                // REDIRECCIÓN
                // ==========================================

                string url;


                if (usuario.Rol == "Analista")
                {
                    url = Url.Action(
                        "ProgramaAnalista",
                        "Analista"
                    );
                }
                else if (usuario.Rol == "Administrador")
                {
                    url = Url.Action(
                        "ProgramaAdministrador",
                        "Administrador"
                    );
                }
                else
                {
                    url = Url.Action(
                        "DashboardCliente",
                        "Login"
                    );
                }


                return Json(new
                {
                    ok = true,

                    mensaje =
                        "Audio recibido correctamente.",

                    redirectUrl = url
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR VERIFICANDO VOZ: " +
                    ex.Message
                );


                return Json(new
                {
                    ok = false,

                    mensaje =
                        "No se pudo verificar la voz: " +
                        ex.Message
                });
            }
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult OlvideContrasena()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult OlvideContrasena(string dni, string nuevaClave, string confirmarClave)
        {
            if (string.IsNullOrWhiteSpace(dni) || !Regex.IsMatch(dni, @"^\d{8}$"))
            {
                ViewData["Mensaje"] = "El DNI debe tener exactamente 8 números.";
                return View();
            }

            if (!Regex.IsMatch(nuevaClave, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$"))
            {
                ViewData["Mensaje"] = "La contraseña debe tener mínimo 6 caracteres, una mayúscula, una minúscula y un número.";
                return View();
            }

            if (nuevaClave != confirmarClave)
            {
                ViewData["Mensaje"] = "Las contraseñas no coinciden.";
                return View();
            }

            Usuario usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (usuario == null)
            {
                ViewData["Mensaje"] = "No existe un usuario con ese DNI.";
                return View();
            }
            if (usuario.clave == utilidades.EncriptarClave(nuevaClave))
            {
                ViewData["Mensaje"] = "La nueva contraseña no puede ser igual a la contraseña actual.";
                return View();
            }

            usuario.clave = utilidades.EncriptarClave(nuevaClave);
            _Context.SaveChanges();

            TempData["Mensaje"] = "Contraseña actualizada correctamente";
            return RedirectToAction("IniciarSesion", "Login");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> EnviarCodigo(string correo)
        {
            try
            {
                string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                Random random = new Random();

                codigoGlobal = new string(Enumerable.Repeat(caracteres, 6)
                    .Select(s => s[random.Next(s.Length)]).ToArray());

                await _emailService.EnviarCodigoAsync(correo, codigoGlobal);

                return Json(new { ok = true, mensaje = "Código enviado correctamente" });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, mensaje = ex.ToString() });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> EnviarCodigoLogin()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dniLoginPendiente))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La sesión de verificación ha expirado."
                    });
                }

                Usuario usuario = _Context.Usuario
                    .FirstOrDefault(x => x.Dni == dniLoginPendiente);

                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró el usuario."
                    });
                }

                string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

                Random random = new Random();

                codigoLogin = new string(
                    Enumerable.Repeat(caracteres, 6)
                        .Select(s => s[random.Next(s.Length)])
                        .ToArray()
                );
                // El código solamente será válido durante 1 minuto
                codigoLoginExpira = DateTime.UtcNow.AddMinutes(1);
                await _emailService.EnviarCodigoAsync(
                    usuario.Correo,
                    codigoLogin
                );

                return Json(new
                {
                    ok = true,
                    mensaje = "Código enviado correctamente.",
                    correo = usuario.Correo
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "No se pudo enviar el código: " + ex.Message
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> VerificarCodigoLogin(string codigo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dniLoginPendiente))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La verificación ha expirado. Inicia sesión nuevamente."
                    });
                }

                if (string.IsNullOrWhiteSpace(codigo))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Ingresa el código de seguridad."
                    });
                }

                // ==========================================
                // VERIFICAR SI EL CÓDIGO YA EXPIRÓ
                // ==========================================

                if (DateTime.UtcNow > codigoLoginExpira)
                {
                    codigoLogin = "";

                    return Json(new
                    {
                        ok = false,
                        expirado = true,
                        mensaje = "El código de seguridad ha expirado. Solicita un nuevo código."
                    });
                }
                if (codigo != codigoLogin)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El código de seguridad es incorrecto."
                    });
                }

                Usuario usuarioEncontrado = _Context.Usuario
                    .FirstOrDefault(x => x.Dni == dniLoginPendiente);

                if (usuarioEncontrado == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró el usuario."
                    });
                }

                List<Claim> claims = new List<Claim>()
        {
            new Claim(ClaimTypes.Name, usuarioEncontrado.Nombre),
            new Claim("Apellido", usuarioEncontrado.Apellido),
            new Claim("Dni", usuarioEncontrado.Dni),
            new Claim("Celular", usuarioEncontrado.Celular),
            new Claim("Correo", usuarioEncontrado.Correo),
            new Claim(ClaimTypes.Role, usuarioEncontrado.Rol)
        };

                ClaimsIdentity claimsIdentity =
                    new ClaimsIdentity(
                        claims,
                        CookieAuthenticationDefaults.AuthenticationScheme
                    );

                AuthenticationProperties properties =
                    new AuthenticationProperties()
                    {
                        AllowRefresh = true,
                    };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    properties
                );

                usuarioEncontrado.EstadoActivo = true;
                usuarioEncontrado.UltimaConexion = DateTime.Now;

                _Context.SaveChanges();

                // Limpiar código utilizado
                codigoLogin = "";
                dniLoginPendiente = "";
                codigoLoginExpira = DateTime.MinValue;
                string url;

                if (usuarioEncontrado.Rol == "Analista")
                {
                    url = Url.Action(
                        "ProgramaAnalista",
                        "Analista"
                    );
                }
                else if (usuarioEncontrado.Rol == "Administrador")
                {
                    _Context.ACTIVIDAD_ADMINISTRADOR.Add(
                        new ActividadAdministrador
                        {
                            IdUsuario = usuarioEncontrado.Id,
                            Tipo = "Inicio de sesión",
                            Descripcion =
                                $"El administrador {usuarioEncontrado.Nombre} {usuarioEncontrado.Apellido} inició sesión correctamente.",
                            Fecha = DateTime.Now
                        }
                    );

                    _Context.SaveChanges();

                    url = Url.Action(
                        "ProgramaAdministrador",
                        "Administrador"
                    );
                }
                else
                {
                    url = Url.Action(
                        "DashboardCliente",
                        "Login"
                    );
                }

                return Json(new
                {
                    ok = true,
                    mensaje = "Verificación correcta.",
                    redirectUrl = url
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "Ocurrió un error al verificar el código: " + ex.Message
                });
            }
        }
        public IActionResult DashboardCliente()
        {
            string dni = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);
            if (usuario != null)
            {
                usuario.UltimaConexion = DateTime.Now;
                _Context.SaveChanges();
            }

            if (usuario != null)
            {
                var solicitudActiva = _Context.SOLICITUD_CREDITO
 .FirstOrDefault(x => x.Usuario_Id_Usuario == usuario.Id &&
     (x.Estado == "Pendiente" ||
      x.Estado == "En Evaluación" ||
      x.Estado == "Aprobado"));
                var solicitudRechazada = _Context.SOLICITUD_CREDITO
.FirstOrDefault(x => x.Usuario_Id_Usuario == usuario.Id &&
                     x.Estado == "Rechazado");

                ViewBag.SolicitudRechazada = solicitudRechazada;

                ViewBag.SolicitudActiva = solicitudActiva;

                if (solicitudActiva != null)
                {
                    Console.WriteLine("Solicitud: " + solicitudActiva.Id_Solicitud);
                    var perfil = _Context.PERFIL_FINANCIERO
                        .FirstOrDefault(x => x.SOLICITUD_CREDITO_Id_Solicitud == solicitudActiva.Id_Solicitud);

                    ViewBag.PerfilFinanciero = perfil;
                }
            }
            var comentarios = (
     from c in _Context.ComentarioClientes
     join u in _Context.Usuario
         on c.Usuario_Id equals u.Id
     select new ComentarioClienteViewModel
     {
         NombreCompleto = u.Nombre + " " + u.Apellido,
         Comentario = c.Comentario,
         Calificacion = c.Calificacion
     }
 ).ToList();
            bool yaTieneResena = _Context.ComentarioClientes
      .Any(x => x.Usuario_Id == usuario.Id);

            bool tieneSolicitudAprobada = _Context.SOLICITUD_CREDITO
                .Any(x => x.Usuario_Id_Usuario == usuario.Id && x.Estado == "Aprobado");

            ViewBag.MostrarResena = tieneSolicitudAprobada && !yaTieneResena;

            return View(comentarios);
        }
        public IActionResult SolicitarCredito()
        {
            string dni = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (usuario == null)
                return RedirectToAction("IniciarSesion", "Login");

            var solicitud = _Context.SOLICITUD_CREDITO
                .Where(x => x.Usuario_Id_Usuario == usuario.Id &&
                    (x.Estado == "Pendiente" ||
                     x.Estado == "En Evaluación" ||
                     x.Estado == "Aprobado" ||
                     x.Estado == "Rechazado"))
                .OrderByDescending(x => x.FechaSolicitud)
                .FirstOrDefault();

            if (solicitud != null)
            {
                if (solicitud.Estado == "Pendiente")
                {
                    ViewBag.MostrarConfirmacion = true;
                }
                else if (solicitud.Estado == "En Evaluación")
                {
                    ViewBag.MostrarModalBloqueo = true;
                    ViewBag.TituloBloqueo = "Solicitud en evaluación";
                    ViewBag.MensajeBloqueo = "Ya tienes una solicitud en evaluación. Debes esperar la respuesta del analista antes de solicitar un nuevo crédito.";
                }
                else if (solicitud.Estado == "Aprobado")
                {
                    ViewBag.MostrarModalBloqueo = true;
                    ViewBag.TituloBloqueo = "Solicitud aprobada activa";
                    ViewBag.MensajeBloqueo = "Tienes una solicitud aprobada activa. No puedes solicitar otro crédito mientras esta solicitud siga vigente.";
                }
                else if (solicitud.Estado == "Rechazado")
                {
                    ViewBag.MostrarModalBloqueo = true;
                    ViewBag.TituloBloqueo = "Solicitud rechazada";
                    ViewBag.MensajeBloqueo = "Tienes una solicitud rechazada. Primero debes eliminarla desde Mis Solicitudes para poder solicitar un nuevo crédito.";
                }
            }

            return View();
        }
        [HttpPost]
        public IActionResult RegistrarSolicitudCredito(
    decimal montoSolicitado,

    int plazoMeses,
    decimal ingresoMensual,
    decimal egresoMensual,
    string tieneOtrosCreditos,
    string motivoOtro,
    string ocupacion,
    string motivoPrestamo,
    string metodoPago,
string entidadPago,
string numeroCuentaPago,
string titularCuenta
            )
        {
            string dni = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (usuario == null)
            {
                return RedirectToAction("IniciarSesion", "Login");
            }

            var existeSolicitudActiva = _Context.SOLICITUD_CREDITO
 .Any(x => x.Usuario_Id_Usuario == usuario.Id &&
      (x.Estado == "Pendiente" ||
       x.Estado == "En Evaluación" ||
       x.Estado == "Aprobado" ||
       x.Estado == "Rechazado"));

            if (existeSolicitudActiva)
            {
                TempData["Mensaje"] = "Ya tienes una solicitud pendiente, aprobada o rechazada. Primero debes finalizarla o borrarla.";
                return RedirectToAction("DashboardCliente");
            }

            int cantidadSolicitudes = _Context.SOLICITUD_CREDITO.Count();

            string nuevoNumeroSolicitud = (cantidadSolicitudes + 1).ToString("D4");

            var solicitud = new SolicitudCredito
            {
                NumeroSolicitud = nuevoNumeroSolicitud,
                MontoSolicitado = montoSolicitado,
                PlazoMeses = plazoMeses,
                InteresEstimado = 10,
                FechaSolicitud = DateTime.Now,
                Estado = "Pendiente",
                Usuario_Id_Usuario = usuario.Id
            };

            _Context.SOLICITUD_CREDITO.Add(solicitud);
            _Context.SaveChanges();

            var metodo = new MetodoPagoSolicitud
            {
                MetodoPago = metodoPago,
                EntidadPago = entidadPago,
                NumeroCuentaPago = numeroCuentaPago,
                TitularCuenta = titularCuenta,
                SOLICITUD_CREDITO_Id_Solicitud = solicitud.Id_Solicitud
            };

            _Context.METODO_PAGO_SOLICITUD.Add(metodo);



            var perfil = new PerfilFinanciero
            {
                IngresoMensual = ingresoMensual,
                EgresoMensual = egresoMensual,
                OtrosCreditos = tieneOtrosCreditos == "Si",
                MotivoPrestamo = motivoPrestamo == "Otros" ? motivoOtro : motivoPrestamo,
                Ocupacion = ocupacion,
                NivelRiesgo = null,
                FechaRegistro = DateTime.Now,
                SOLICITUD_CREDITO_Id_Solicitud = solicitud.Id_Solicitud
            };

            _Context.PERFIL_FINANCIERO.Add(perfil);

            var historial = new HistorialEstado
            {
                EstadoActual = "Pendiente",
                MotivoCambio = "pendiente en evaluación.",
                FechaCambio = DateTime.Now,
                SOLICITUD_CREDITO_Id_Solicitud = solicitud.Id_Solicitud
            };

            _Context.HISTORIAL_ESTADO.Add(historial);

            _Context.SaveChanges();

            TempData["MostrarConfirmacion"] = "true";
            return RedirectToAction("SolicitarCredito");
        }

        [HttpGet]
        public IActionResult MisSolicitudes()
        {
            string dni = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (usuario == null)
                return RedirectToAction("IniciarSesion", "Login");

            var solicitudes = _Context.SOLICITUD_CREDITO
    .Where(x => x.Usuario_Id_Usuario == usuario.Id && x.Estado != "Cancelado")
                .Select(x => new SolicitudCreditoViewModel
                {
                    IdSolicitud = x.Id_Solicitud,
                    NumeroSolicitud = x.NumeroSolicitud,
                    MontoSolicitado = x.MontoSolicitado,
                    PlazoMeses = x.PlazoMeses,
                    InteresEstimado = x.InteresEstimado,
                    FechaSolicitud = x.FechaSolicitud,
                    Estado = x.Estado
                })
                .ToList();
            ViewBag.Evaluaciones = _Context.Evaluacion_Riesgo.ToList();
            return View(solicitudes);
        }
        [HttpPost]
        public IActionResult BorrarSolicitudRechazada(int idSolicitud)
        {
            var solicitud = _Context.SOLICITUD_CREDITO
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Estado == "Rechazado");

            if (solicitud == null)
            {
                return Json(new { mensaje = "No se encontró la solicitud rechazada." });
            }

            var perfil = _Context.PERFIL_FINANCIERO
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            var historial = _Context.HISTORIAL_ESTADO
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            var evaluacion = _Context.Evaluacion_Riesgo
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);
            var cuotas = _Context.CUOTA
    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            _Context.CUOTA.RemoveRange(cuotas);

            var cronogramas = _Context.CRONOGRAMA
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            _Context.CRONOGRAMA.RemoveRange(cronogramas);

            var propuestas = _Context.PROPUESTA_CREDITO
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            _Context.PROPUESTA_CREDITO.RemoveRange(propuestas);

            var mensajes = _Context.MENSAJE
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            _Context.MENSAJE.RemoveRange(mensajes);

            var historialCredito = _Context.HISTORIAL_CREDITO
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            _Context.HISTORIAL_CREDITO.RemoveRange(historialCredito);

            var pagosCancelacion = _Context.PAGO_CANCELACION
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            _Context.PAGO_CANCELACION.RemoveRange(pagosCancelacion);

            _Context.PERFIL_FINANCIERO.RemoveRange(perfil);
            _Context.HISTORIAL_ESTADO.RemoveRange(historial);
            _Context.Evaluacion_Riesgo.RemoveRange(evaluacion);
            _Context.SOLICITUD_CREDITO.Remove(solicitud);

            _Context.SaveChanges();

            return Json(new { mensaje = "Solicitud rechazada eliminada correctamente." });
        }
        [HttpPost]
        public async Task<IActionResult> EnviarCronograma(int idSolicitud)
        {
            try
            {
                string dni = User.FindFirst("Dni")?.Value;

                var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

                if (usuario == null)
                {
                    TempData["Mensaje"] = "No se encontró el usuario.";
                    return RedirectToAction("MisSolicitudes");
                }

                var solicitud = _Context.SOLICITUD_CREDITO
                    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Usuario_Id_Usuario == usuario.Id);

                if (solicitud == null)
                {
                    TempData["Mensaje"] = "No se encontró la solicitud.";
                    return RedirectToAction("MisSolicitudes");
                }

                var cuotas = _Context.CUOTA
                    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                    .OrderBy(x => x.NumeroCuota)
                    .ToList();

                if (cuotas.Count == 0)
                {
                    TempData["Mensaje"] = "No hay cuotas registradas para esta solicitud.";
                    return RedirectToAction("MisSolicitudes");
                }

                decimal monto = solicitud.MontoSolicitado;
                int nroCuotas = solicitud.PlazoMeses;
                decimal interes = solicitud.InteresEstimado;
                decimal totalPagar = cuotas.Sum(x => x.MontoCuota ?? 0);

                DateTime fechaSolicitud = solicitud.FechaSolicitud;
                DateTime primeraFechaPago = cuotas.First().FechaVencimiento;
                DateTime fechaLimite = cuotas.First().FechaLimitePago;

                string filas = "";

                decimal capitalMensual = Math.Round(monto / nroCuotas, 2);
                decimal cuotaMensual = cuotas.First().MontoCuota ?? Math.Round(totalPagar / nroCuotas, 2);
                decimal interesMensual = Math.Round(cuotaMensual - capitalMensual, 2);
                decimal saldo = monto;

                foreach (var c in cuotas)
                {
                    decimal montoBase =
                        c.Capital.GetValueOrDefault()
                        + c.Interes.GetValueOrDefault()
                        + c.Comisiones.GetValueOrDefault()
                        + c.Seguros.GetValueOrDefault();

                    int diasAtraso = 0;
                    decimal mora = 0;
                    decimal cuotaConMora = montoBase;

                    if (c.Estado == "Pendiente" &&
                        DateTime.Now.Date > c.FechaLimitePago.Date)
                    {
                        diasAtraso = (DateTime.Now.Date - c.FechaLimitePago.Date).Days;
                        mora = diasAtraso * 5;
                        cuotaConMora = montoBase + mora;
                    }

                    saldo -= c.Capital.GetValueOrDefault();
                    if (saldo < 0) saldo = 0;

                    filas += $@"
<tr>
    <td>{c.NumeroCuota}</td>
    <td>{c.FechaVencimiento:dd/MM/yyyy}</td>
    <td>{c.Dias}</td>
    <td>S/ {c.Capital.GetValueOrDefault():N2}</td>
    <td>S/ {c.Interes.GetValueOrDefault():N2}</td>
    <td>S/ {c.Comisiones.GetValueOrDefault():N2}</td>
    <td>S/ {c.Seguros.GetValueOrDefault():N2}</td>
    <td>S/ {mora:N2}</td>
    <td>S/ {cuotaConMora:N2}</td>
    <td>S/ {saldo:N2}</td>
</tr>";
                }

                string cuerpoHtml = $@"
<h2 style='color:#4c1d95;text-align:center;'>CREDIPLUS FINANCIERA</h2>
<h3 style='text-align:center;'>CRÉDITO PERSONAL - CRONOGRAMA REFERENCIAL</h3>

<p><b>Cliente:</b> {usuario.Nombre} {usuario.Apellido}</p>
<p><b>DNI:</b> {usuario.Dni}</p>
<p><b>Monto del préstamo:</b> S/ {monto:N2}</p>
<p><b>Nro de cuotas:</b> {nroCuotas}</p>
<p><b>Fecha de desembolso:</b> {fechaSolicitud:dd/MM/yyyy}</p>
<p><b>Interés estimado:</b> {interes}%</p>
<p><b>Total a pagar:</b> S/ {totalPagar:N2}</p>

<div style='background:#fff3cd;padding:15px;border-radius:8px;margin-top:15px;'>
    <b>Primera fecha de pago:</b> {primeraFechaPago:dd/MM/yyyy}
</div>

<br/>

<div style='background:#f8d7da;color:#842029;padding:15px;border-radius:8px;'>
    <b>Fecha límite de pago:</b> {fechaLimite:dd/MM/yyyy}. 
    Tiene hasta 15 días para pagar. Los domingos no se consideran dentro del plazo. 
    Si se retrasa, se aplicarán intereses por mora.
</div>

<br/>

<table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;width:100%;text-align:center;font-family:Arial;font-size:13px;'>
    <tr style='background:#4c1d95;color:white;'>
        <th>Cuota</th>
        <th>Fecha de vencimiento</th>
        <th>Días</th>
        <th>Capital</th>
        <th>Interés</th>
        <th>Comisiones</th>
        <th>Seguros</th>
        <th>Mora</th>
<th>Importe de cuota</th>
<th>Saldo pendiente</th>
    </tr>
    {filas}
</table>

<p style='margin-top:25px;'>
    Gracias por confiar en CrediPlus. Te recordamos realizar tus pagos dentro del plazo establecido para evitar intereses adicionales.
</p>

<p><b>Atentamente,<br/>CrediPlus</b></p>
";

                await _emailService.EnviarCorreoAsync(
                    usuario.Correo,
                    "Cronograma de pago - CrediPlus",
                    cuerpoHtml
                );

                TempData["Mensaje"] = "Cronograma enviado correctamente al correo registrado.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "Error al enviar cronograma: " + ex.Message;
            }

            return RedirectToAction("MisSolicitudes");
        }

        private DateTime CalcularFechaLimite(DateTime fechaInicio, int diasPlazo)
        {
            DateTime fecha = fechaInicio;
            int diasContados = 0;

            while (diasContados < diasPlazo)
            {
                fecha = fecha.AddDays(1);

                if (fecha.DayOfWeek != DayOfWeek.Sunday)
                {
                    diasContados++;
                }
            }

            return fecha;
        }

        [HttpGet]
        public IActionResult PerfilPersonal()
        {
            string dni = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (usuario == null)
            {
                return RedirectToAction("IniciarSesion", "Login");
            }

            return View(usuario);
        }

        [HttpPost]
        public async Task<IActionResult> PerfilPersonal(Usuario usuarioEditado, string codigoCorreo)
        {
            string dniActual = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dniActual);
            if (usuarioEditado.Correo != usuario.Correo)
            {
                var codigoGuardado = HttpContext.Session.GetString("CodigoPerfil");
                if (string.IsNullOrEmpty(codigoCorreo))
                {
                    ViewData["Mensaje"] = "Debes ingresar el código de verificación del nuevo correo.";
                    ViewData["ModoEditar"] = true;

                    ViewBag.LimpiarCorreo = true;
                    ViewBag.LimpiarCodigo = true;

                    ModelState.Remove("codigoCorreo");

                    return View(usuario);
                }

                if (codigoGuardado != codigoCorreo)
                {
                    ViewData["Mensaje"] = "El código de verificación es incorrecto.";
                    ViewData["ModoEditar"] = true;

                    ViewBag.LimpiarCorreo = true;
                    ViewBag.LimpiarCodigo = true;

                    ModelState.Remove("codigoCorreo");
                    return View(usuario);
                }
                HttpContext.Session.Remove("CodigoPerfil");
                HttpContext.Session.Remove("CorreoPerfil");
            }

            if (usuario == null)
            {
                return RedirectToAction("IniciarSesion", "Login");
            }

            if (usuarioEditado.Dni.Length != 8 || !usuarioEditado.Dni.All(char.IsDigit))
            {
                ViewData["Mensaje"] = "El DNI debe tener 8 dígitos.";
                ViewData["ModoEditar"] = true;
                return View(usuario);
            }

            if (usuarioEditado.Celular.Length != 9 || !usuarioEditado.Celular.StartsWith("9") || !usuarioEditado.Celular.All(char.IsDigit))
            {
                ViewData["Mensaje"] = "El celular debe tener 9 dígitos y empezar con 9.";
                ViewData["ModoEditar"] = true;
                return View(usuario);
            }

            if (!usuarioEditado.Correo.EndsWith("@gmail.com"))
            {
                ViewData["Mensaje"] = "El correo debe ser Gmail.";
                ViewData["ModoEditar"] = true;
                return View(usuario);
            }

            bool dniRepetido = _Context.Usuario.Any(x => x.Dni == usuarioEditado.Dni && x.Id != usuario.Id);
            if (dniRepetido)
            {
                ViewData["Mensaje"] = "Ese DNI ya está registrado.";
                ViewData["ModoEditar"] = true;
                return View(usuario);
            }

            bool correoRepetido = _Context.Usuario.Any(x => x.Correo == usuarioEditado.Correo && x.Id != usuario.Id);
            if (correoRepetido)
            {
                ViewData["Mensaje"] = "Ese correo ya está registrado.";
                ViewData["ModoEditar"] = true;
                ViewData["CorreoIntentado"] = usuarioEditado.Correo;
                return View(usuario);
            }
            string correoAnterior = usuario.Correo;
            usuario.Nombre = usuarioEditado.Nombre;
            usuario.Apellido = usuarioEditado.Apellido;
            usuario.Dni = usuarioEditado.Dni;
            usuario.Celular = usuarioEditado.Celular;
            usuario.Correo = usuarioEditado.Correo;
            usuario.Genero = usuarioEditado.Genero;
            if (correoAnterior != usuarioEditado.Correo)
            {
                var idsSolicitudes = _Context.SOLICITUD_CREDITO
                    .Where(x => x.Usuario_Id_Usuario == usuario.Id)
                    .Select(x => x.Id_Solicitud)
                    .ToList();

                var cronogramas = _Context.CRONOGRAMA
                    .Where(x => idsSolicitudes.Contains(x.SOLICITUD_CREDITO_Id_Solicitud))
                    .ToList();

                foreach (var c in cronogramas)
                {
                    c.CorreoDestino = usuarioEditado.Correo;
                }
            }

            if (!string.IsNullOrWhiteSpace(usuarioEditado.clave))
            {
                bool tieneMayuscula = usuarioEditado.clave.Any(char.IsUpper);
                bool tieneMinuscula = usuarioEditado.clave.Any(char.IsLower);
                bool tieneNumero = usuarioEditado.clave.Any(char.IsDigit);

                if (usuarioEditado.clave.Length < 6 || !tieneMayuscula || !tieneMinuscula || !tieneNumero)
                {
                    ViewData["Mensaje"] = "La contraseña debe tener mayúscula, minúscula, número y mínimo 6 caracteres.";
                    ViewData["ModoEditar"] = true;
                    return View(usuario);
                }

                usuario.clave = utilidades.EncriptarClave(usuarioEditado.clave);
            }

            _Context.SaveChanges();

            TempData["Mensaje"] = "Perfil actualizado correctamente. Vuelve a iniciar sesión para ver los cambios.";

            _Context.SaveChanges();
            TempData["MensajeOk"] = "Perfil actualizado correctamente.";
            return RedirectToAction("PerfilPersonal", "Login");
        }
        [HttpPost]
        public async Task<IActionResult> EnviarCodigoPerfil(string correo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(correo) || !correo.EndsWith("@gmail.com"))
                {
                    return Json(new { ok = false, mensaje = "Ingrese un correo Gmail válido." });
                }

                Random random = new Random();
                string codigo = random.Next(100000, 999999).ToString();

                HttpContext.Session.SetString("CodigoPerfil", codigo);
                HttpContext.Session.SetString("CorreoPerfil", correo);

                await _emailService.EnviarCodigoAsync(correo, codigo);

                return Json(new { ok = true, mensaje = "Código enviado correctamente al nuevo correo." });
            }
            catch
            {
                return Json(new { ok = false, mensaje = "No se pudo enviar el código de verificación." });
            }
        }
        public IActionResult Configuracion()
        {
            return View();
        }
        public IActionResult PagosPendientes()
        {
            string dni = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (usuario == null)
                return RedirectToAction("IniciarSesion", "Login");

            var cuotasAprobacion = _Context.CUOTA
                .Include(c => c.SOLICITUD_CREDITO)
                .Where(c =>
                    c.SOLICITUD_CREDITO.Usuario_Id_Usuario == usuario.Id &&
                    c.SOLICITUD_CREDITO.Estado == "Aprobado" &&
                    c.Estado == "Pendiente aprobación")
                .OrderBy(c => c.NumeroCuota)
                .ToList();

            var ultimaAprobacion = cuotasAprobacion.LastOrDefault();

            var siguientePendiente = _Context.CUOTA
                .Include(c => c.SOLICITUD_CREDITO)
                .Where(c =>
                    c.SOLICITUD_CREDITO.Usuario_Id_Usuario == usuario.Id &&
                    c.SOLICITUD_CREDITO.Estado == "Aprobado" &&
                    c.Estado == "Pendiente" &&
                    (ultimaAprobacion == null || c.NumeroCuota > ultimaAprobacion.NumeroCuota))
                .OrderBy(c => c.NumeroCuota)
                .FirstOrDefault();

            var cuotas = new List<Cuota>();

            cuotas.AddRange(cuotasAprobacion);

            if (siguientePendiente != null)
                cuotas.Add(siguientePendiente);

            foreach (var cuota in cuotas)
            {
                if (cuota.Estado == "Pendiente" &&
    DateTime.Now.Date > cuota.FechaLimitePago.Date)
                {
                    int diasAtraso = (DateTime.Now.Date - cuota.FechaLimitePago.Date).Days;

                    decimal montoBase =
                        cuota.Capital.GetValueOrDefault()
                        + cuota.Interes.GetValueOrDefault()
                        + cuota.Comisiones.GetValueOrDefault()
                        + cuota.Seguros.GetValueOrDefault();

                    decimal mora = diasAtraso * 5;

                    cuota.MontoCuota = montoBase + mora;
                }
            }

            _Context.SaveChanges();

            var idsSolicitudes = cuotas
                .Select(x => x.SOLICITUD_CREDITO_Id_Solicitud)
                .Distinct()
                .ToList();

            ViewBag.MetodosSolicitud = _Context.METODO_PAGO_SOLICITUD
                .Where(x => idsSolicitudes.Contains(x.SOLICITUD_CREDITO_Id_Solicitud))
                .ToList();

            return View(cuotas);
        }
        public IActionResult SimuladorCredito()
        {
            return View();
        }
        [HttpPost]
        public IActionResult RegistrarCancelacion(int idSolicitud, decimal montoDevuelto, string metodoPago, string codigoOperacion, string motivoCancelacion)
        {
            try
            {
                var pago = new PagoCancelacion
                {
                    MontoDevuelto = montoDevuelto,
                    MetodoPago = metodoPago,
                    CodigoOperacion = codigoOperacion,
                    MotivoCancelacion = motivoCancelacion,
                    FechaPago = DateTime.Now,
                    Estado = "Pendiente",
                    SOLICITUD_CREDITO_Id_Solicitud = idSolicitud
                };

                _Context.PAGO_CANCELACION.Add(pago);

                var solicitud = _Context.SOLICITUD_CREDITO.FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

                if (solicitud != null)
                {
                    solicitud.Estado = "Pendiente cancelación";
                }

                _Context.SaveChanges();

                return Json(new { ok = true, mensaje = "Pago registrado correctamente. La cancelación queda pendiente de aprobación." });
            }
            catch
            {
                return Json(new { ok = false, mensaje = "No se pudo registrar la cancelación." });
            }
        }
        [HttpPost]
        public IActionResult EditarSolicitud(int idSolicitud, decimal nuevoMonto, int nuevoPlazo)
        {
            try
            {
                var solicitud = _Context.SOLICITUD_CREDITO
                    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

                if (solicitud == null)
                {
                    return Json(new { ok = false, mensaje = "No se encontró la solicitud." });
                }

                string estadoActual = solicitud.Estado?.Trim().ToLower();

                if (estadoActual == "pendiente" ||
                    estadoActual == "pendiente cancelación" ||
                    estadoActual == "rechazado" ||
                    estadoActual == "cancelado")
                {
                    return Json(new { ok = false, mensaje = "No puedes editar esta solicitud." });
                }

                if (nuevoMonto < 1000 || nuevoMonto > 50000)
                {
                    return Json(new { ok = false, mensaje = "El monto debe estar entre S/ 1,000 y S/ 50,000." });
                }

                solicitud.MontoSolicitado = nuevoMonto;
                solicitud.PlazoMeses = nuevoPlazo;

                string mensajeHistorial = "";

                if (estadoActual == "aprobado")
                {
                    solicitud.Estado = "Pendiente";
                    mensajeHistorial = "Cliente editó una solicitud aprobada. Regresa a Pendiente para nueva revisión.";
                }
                else if (estadoActual == "en evaluación")
                {
                    solicitud.Estado = "En Evaluación";
                    solicitud.NotificacionEdicionVista = false;
                    mensajeHistorial = "Cliente editó monto y plazo mientras la solicitud estaba en evaluación.";
                }

                var subject = new SolicitudSubject();

                subject.AgregarObservador(
                    new HistorialEstadoObserver(_Context)
                );

                subject.Notificar(
                    solicitud,
                    mensajeHistorial + " Nuevo monto: S/ " + nuevoMonto +
                    ", nuevo plazo: " + nuevoPlazo + " meses."
                );

                _Context.SaveChanges();

                return Json(new
                {
                    ok = true,
                    mensaje = estadoActual == "aprobado"
                        ? "Cambios guardados. La solicitud volvió a Pendiente."
                        : "Cambios guardados. La solicitud sigue En Evaluación con el nuevo monto y plazo."
                });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, mensaje = "No se pudo editar la solicitud: " + ex.Message });
            }
        }
        [HttpPost]
        public IActionResult RegistrarPagoCuota(
    int idCuota,
    decimal montoPagado,
    string metodoPago,
    string entidadPago,
    string codigoOperacion)
        {
            try
            {
                var pago = new PagoCuota
                {
                    Id_Cuota = idCuota,
                    MontoPagado = montoPagado,
                    MetodoPago = metodoPago,
                    EntidadPago = entidadPago,
                    CodigoOperacion = codigoOperacion,
                    FechaPago = DateTime.Now,
                    Estado = "Pendiente validación"
                };

                _Context.PAGO_CUOTA.Add(pago);

                var cuota = _Context.CUOTA
                    .FirstOrDefault(x => x.Id_Cuota == idCuota);

                if (cuota != null)
                {
                    cuota.Estado = "Pendiente aprobación";
                }

                _Context.SaveChanges();

                return Json(new
                {
                    ok = true,
                    mensaje = "Pago registrado correctamente."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = ex.Message
                });
            }
        }
        public IActionResult PreguntasFrecuentes()
        {
            return View();
        }
        [HttpGet]
        public JsonResult ObtenerMensajes(int idSolicitud)
        {
            var solicitud = _Context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

            var mensajes = _Context.MENSAJE
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .OrderBy(x => x.FechaEnvio)
                .Select(x => new
                {
                    mensaje = x.MensajeTexto,
                    fecha = x.FechaEnvio,
                    tipoUsuario = x.Remitente,
                    imagen = x.Imagen,

                    nombre = x.Remitente == "Usuario"
                        ? solicitud.USUARIO.Nombre + " " + solicitud.USUARIO.Apellido
                        : "Rafael Rosales"
                })
                .ToList();

            return Json(mensajes);
        }
        [HttpPost]
        public async Task<JsonResult> EnviarMensaje(
     int idSolicitud,
     string? mensaje,
     IFormFile? imagen)
        {
            string rutaImagen = null;

            if (imagen != null)
            {
                string nombreArchivo =
                    Guid.NewGuid().ToString() +
                    Path.GetExtension(imagen.FileName);

                string carpeta =
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "imagenesChat");

                if (!Directory.Exists(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }

                string rutaCompleta =
                    Path.Combine(carpeta, nombreArchivo);

                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await imagen.CopyToAsync(stream);
                }

                rutaImagen = "/imagenesChat/" + nombreArchivo;
            }

            Mensaje nuevo = new Mensaje();

            nuevo.SOLICITUD_CREDITO_Id_Solicitud = idSolicitud;
            nuevo.MensajeTexto = string.IsNullOrWhiteSpace(mensaje)
                ? "[Imagen]"
                : mensaje;

            nuevo.Imagen = rutaImagen;

            nuevo.Remitente = "Usuario";
            nuevo.FechaEnvio = DateTime.Now;
            nuevo.Leido = false;

            _Context.MENSAJE.Add(nuevo);
            _Context.SaveChanges();

            return Json(new { ok = true });
        }
        [HttpGet]
        public JsonResult ObtenerInfoSolicitud(int idSolicitud)
        {
            var analista = _Context.Usuario
                .FirstOrDefault(x => x.Rol == "Analista");

            if (analista == null)
            {
                return Json(new
                {
                    analista = "",
                    correo = ""
                });
            }

            return Json(new
            {
                analista = analista.Nombre + " " + analista.Apellido,
                correo = analista.Correo
            });
        }

        [HttpPost]
        public JsonResult CancelarEvaluacion(int idSolicitud, string motivo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(motivo))
                {
                    return Json(new { ok = false, mensaje = "Debe ingresar el motivo de cancelación." });
                }

                var solicitud = _Context.SOLICITUD_CREDITO
                    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Estado == "En Evaluación");

                if (solicitud == null)
                {
                    return Json(new { ok = false, mensaje = "No se encontró la solicitud en evaluación." });
                }

                var propuestaRecomendada = _Context.PROPUESTA_CREDITO
                    .FirstOrDefault(x =>
                        x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud &&
                        x.EsRecomendada == true);

                decimal montoFinal = propuestaRecomendada != null
                    ? propuestaRecomendada.Monto
                    : solicitud.MontoSolicitado;

                var cancelacion = new CancelacionEvaluacion
                {
                    IdSolicitud = null,
                    MontoSolicitado = montoFinal,
                    MotivoCancelacion = motivo.Trim(),
                    FechaCancelacion = DateTime.Now,
                    Responsable = "Analista de Riesgo"
                };

                _Context.CancelacionEvaluacion.Add(cancelacion);


                var propuestas = _Context.PROPUESTA_CREDITO
                    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

                var perfil = _Context.PERFIL_FINANCIERO
                    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

                var mensajes = _Context.MENSAJE
                    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

                var evaluacion = _Context.Evaluacion_Riesgo
                    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);
                var historialCrediticio = _Context.HISTORIAL_CREDITO
    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);
                var cuotas = _Context.CUOTA
    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

                var cronogramas = _Context.CRONOGRAMA
                    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);
                var pagosCancelacion = _Context.PAGO_CANCELACION
    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

                _Context.PROPUESTA_CREDITO.RemoveRange(propuestas);
                _Context.PERFIL_FINANCIERO.RemoveRange(perfil);
                _Context.MENSAJE.RemoveRange(mensajes);
                _Context.Evaluacion_Riesgo.RemoveRange(evaluacion);
                _Context.HISTORIAL_CREDITO.RemoveRange(historialCrediticio);
                _Context.CUOTA.RemoveRange(cuotas);
                _Context.CRONOGRAMA.RemoveRange(cronogramas);
                _Context.PAGO_CANCELACION.RemoveRange(pagosCancelacion);
                var historialEstado = _Context.HISTORIAL_ESTADO
    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

                _Context.HISTORIAL_ESTADO.RemoveRange(historialEstado);
                _Context.SOLICITUD_CREDITO.Remove(solicitud);




                _Context.SaveChanges();

                return Json(new { ok = true, mensaje = "Tu solicitud fue cancelada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, mensaje = ex.Message });
            }
        }
        [HttpPost]
        public IActionResult GuardarResena(int calificacion, string comentario)
        {
            string dni = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (usuario == null)
            {
                return Json(new { ok = false, mensaje = "Usuario no encontrado." });
            }

            if (calificacion < 1 || calificacion > 5)
            {
                return Json(new { ok = false, mensaje = "Debe seleccionar una calificación." });
            }

            if (string.IsNullOrWhiteSpace(comentario))
            {
                return Json(new { ok = false, mensaje = "Debe escribir un comentario." });
            }

            bool yaExiste = _Context.ComentarioClientes
                .Any(x => x.Usuario_Id == usuario.Id);

            if (yaExiste)
            {
                return Json(new { ok = false, mensaje = "Ya registraste una reseña." });
            }

            var resena = new ComentarioCliente
            {
                Usuario_Id = usuario.Id,
                Calificacion = calificacion,
                Comentario = comentario.Trim(),
                FechaComentario = DateTime.Now
            };

            _Context.ComentarioClientes.Add(resena);
            _Context.SaveChanges();

            return Json(new { ok = true, mensaje = "Reseña enviada correctamente. Gracias por tu opinión." });
        }

        [HttpPost]
        public IActionResult MarcarClienteInactivo()
        {
            string dni = User.FindFirst("Dni")?.Value;

            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (usuario != null && usuario.Rol == "Cliente")
            {
                usuario.EstadoActivo = false;
                usuario.UltimaConexion = DateTime.Now;
                _Context.SaveChanges();
            }

            return Ok();
        }
        


    }

}