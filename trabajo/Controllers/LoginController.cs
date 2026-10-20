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
using System.Text;
using System.Security.Cryptography;
using FirebaseAdmin.Messaging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Globalization;
using Vosk;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using System.Diagnostics;
namespace trabajo.Controllers
{
    [Authorize]
    public class LoginController : Controller
    {
        private readonly IusuarioServices _UsuarioService;
        private readonly UsuarioContext _Context;
        private readonly ServicioEmbeddingVoz _ServicioEmbeddingVoz;
        private readonly EmailService _emailService = new EmailService();
        private static string codigoLogin = "";

        // ==========================================
        // SEGURIDAD - ALERTAS DE ACCESO (RATE LIMIT 5 MIN)
        // ==========================================
        private static readonly ConcurrentDictionary<string, (DateTime UltimoEnvio, int ContadorFallos)> _alertasFallidasCache = new();

        // ==========================================
        // COMPATIBILIDAD CON GMAIL, HOTMAIL, OUTLOOK
        // ==========================================
        private static readonly Regex _regexCorreoValido = new(
            @"^[A-Za-z0-9._%+-]+@(gmail\.com|hotmail\.com|hotmail\.es|outlook\.com|outlook\.es|live\.com)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static bool EsCorreoValidoDominio(string? correo)
        {
            if (string.IsNullOrWhiteSpace(correo)) return false;
            return _regexCorreoValido.IsMatch(correo.Trim());
        }

        private string ObtenerDispositivoCliente()
        {
            try
            {
                string userAgent = Request.Headers["User-Agent"].ToString();
                if (string.IsNullOrWhiteSpace(userAgent)) return "Dispositivo no identificado";

                string so = "Sistema desconocido";
                if (userAgent.Contains("Windows")) so = "Windows";
                else if (userAgent.Contains("Android")) so = "Android";
                else if (userAgent.Contains("iPhone") || userAgent.Contains("iPad")) so = "iOS";
                else if (userAgent.Contains("Macintosh") || userAgent.Contains("Mac OS")) so = "macOS";
                else if (userAgent.Contains("Linux")) so = "Linux";

                string navegador = "Navegador web";
                if (userAgent.Contains("Edg/")) navegador = "Microsoft Edge";
                else if (userAgent.Contains("Chrome/") && !userAgent.Contains("Edg/")) navegador = "Google Chrome";
                else if (userAgent.Contains("Firefox/")) navegador = "Mozilla Firefox";
                else if (userAgent.Contains("Safari/") && !userAgent.Contains("Chrome/")) navegador = "Safari";

                return $"{navegador} en {so}";
            }
            catch
            {
                return "Navegador web";
            }
        }
        // ==========================================
        // RECUPERACIÓN DE CONTRASEÑA
        // ==========================================

        private static readonly Dictionary<string, string>
            codigosRecuperacion = new();

        private static readonly Dictionary<string, DateTime>
            expiracionesRecuperacion = new();

        // ==========================================
        // SEGURIDAD - INTENTOS DE VOZ
        // ==========================================

        private const int MAX_INTENTOS_VOZ = 3;
        private const int MINUTOS_BLOQUEO_VOZ = 5;
        // Hora en la que vence el código de inicio de sesión
        private static DateTime codigoLoginExpira = DateTime.MinValue;
        private static readonly object _lockRenovacionVoz = new object();

        private readonly IConfiguration _configuration;
        private readonly ITurnstileService _turnstileService;

        public LoginController(
            IusuarioServices usuarioService,
            UsuarioContext context,
            ServicioEmbeddingVoz servicioEmbeddingVoz,
            IConfiguration configuration,
            ITurnstileService turnstileService)
        {
            _UsuarioService = usuarioService;
            _Context = context;
            _ServicioEmbeddingVoz = servicioEmbeddingVoz;
            _configuration = configuration;
            _turnstileService = turnstileService;
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

            return View("PantallaPrincipal", comentarios);
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

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccesoDenegado()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Registro()
        {
            ViewBag.TurnstileSiteKey = _turnstileService.ObtenerSiteKey();
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
            // ==========================================
            // VERIFICACIÓN DE SEGURIDAD CAPTCHA (TURNSTILE)
            // ==========================================
            string? turnstileToken = Request.Form["cf-turnstile-response"].FirstOrDefault();
            string? ipRemota = HttpContext.Connection.RemoteIpAddress?.ToString();
            var resTurnstile = await _turnstileService.ValidarTokenAsync(turnstileToken, ipRemota);
            if (!resTurnstile.Valido)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = resTurnstile.Mensaje
                });
            }

            // ==========================================
            // DNI
            // ==========================================

            if (string.IsNullOrWhiteSpace(usuario.Dni) ||
                !Regex.IsMatch(usuario.Dni, @"^\d{8}$"))
            {
                return Json(new
                {
                    ok = false,
                    campo = "dniRegistro",
                    mensaje = "El DNI debe tener exactamente 8 números."
                });
            }

            // ==========================================
            // CELULAR
            // ==========================================

            if (string.IsNullOrWhiteSpace(usuario.Celular) ||
                !Regex.IsMatch(usuario.Celular, @"^9\d{8}$"))
            {
                return Json(new
                {
                    ok = false,
                    campo = "celularRegistro",
                    mensaje =
                        "El celular debe tener 9 números y empezar con 9."
                });
            }

            // ==========================================
            // CORREO
            // ==========================================

            if (string.IsNullOrWhiteSpace(usuario.Correo) ||
                !EsCorreoValidoDominio(usuario.Correo))
            {
                return Json(new
                {
                    ok = false,
                    campo = "correoRegistro",
                    mensaje = "Ingresa un correo válido (Gmail, Hotmail o Outlook)."
                });
            }

            // ==========================================
            // GÉNERO
            // ==========================================

            if (string.IsNullOrWhiteSpace(usuario.Genero))
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "Debe seleccionar un género."
                });
            }

            // ==========================================
            // CONTRASEÑA (POLÍTICA: MÍNIMO 12 CARACTERES, MAYÚSCULA, MINÚSCULA, NÚMERO Y SÍMBOLO)
            // ==========================================

            var resClave = PasswordPolicyHelper.Validar(usuario.clave);
            if (!resClave.EsValida)
            {
                return Json(new
                {
                    ok = false,
                    campo = "clave",
                    mensaje = resClave.MensajeError
                });
            }

            // ==========================================
            // CONFIRMACIÓN
            // ==========================================

            if (usuario.clave != confirmarClave)
            {
                return Json(new
                {
                    ok = false,
                    campo = "confirmarClaveRegistro",
                    mensaje = "Las contraseñas no coinciden."
                });
            }

            // ==========================================
            // DATOS DUPLICADOS
            // ==========================================

            bool dniExiste =
                await _Context.Usuario.AnyAsync(
                    x => x.Dni == usuario.Dni);

            if (dniExiste)
            {
                return Json(new
                {
                    ok = false,
                    campo = "dniRegistro",
                    mensaje = "Este DNI ya está registrado."
                });
            }

            bool celularExiste =
                await _Context.Usuario.AnyAsync(
                    x => x.Celular == usuario.Celular);

            if (celularExiste)
            {
                return Json(new
                {
                    ok = false,
                    campo = "celularRegistro",
                    mensaje = "Este número de celular ya está registrado."
                });
            }

            bool correoExiste =
                await _Context.Usuario.AnyAsync(
                    x => x.Correo == usuario.Correo);

            if (correoExiste)
            {
                return Json(new
                {
                    ok = false,
                    campo = "correoRegistro",
                    mensaje = "Este correo ya está registrado."
                });
            }

            // ==========================================
            // VERIFICACIÓN DE CORREO
            // ==========================================

            string verificadoSesion =
                HttpContext.Session.GetString("CodigoRegistroVerificado");

            string correoVerificadoSesion =
                HttpContext.Session.GetString("CorreoRegistroVerificado");

            bool correoValidado =
                verificadoSesion == "true" &&
                string.Equals(
                    usuario.Correo?.Trim(),
                    correoVerificadoSesion,
                    StringComparison.OrdinalIgnoreCase);

            if (!correoValidado)
            {
                // Si aún no está verificado en sesión mediante el modal, verificar si se envió el código directamente
                if (!string.IsNullOrWhiteSpace(codigoVerificacion))
                {
                    string hashGuardado =
                        HttpContext.Session.GetString("HashCodigoRegistro");

                    string correoCodigo =
                        HttpContext.Session.GetString("CodigoRegistroCorreo");

                    string expiracionTexto =
                        HttpContext.Session.GetString("CodigoRegistroExpira");

                    if (!string.IsNullOrWhiteSpace(hashGuardado) &&
                        long.TryParse(expiracionTexto, out long expUnix) &&
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds() < expUnix &&
                        string.Equals(usuario.Correo?.Trim(), correoCodigo, StringComparison.OrdinalIgnoreCase))
                    {
                        string hashIngresado = utilidades.EncriptarClave(codigoVerificacion.Trim());
                        byte[] hashIngresadoBytes = Encoding.UTF8.GetBytes(hashIngresado);
                        byte[] hashGuardadoBytes = Encoding.UTF8.GetBytes(hashGuardado);

                        if (CryptographicOperations.FixedTimeEquals(hashIngresadoBytes, hashGuardadoBytes))
                        {
                            correoValidado = true;
                        }
                    }
                }
            }

            if (!correoValidado)
            {
                return Json(new
                {
                    ok = false,
                    campo = "correoRegistro",
                    mensaje = "Debes verificar tu correo electrónico antes de registrarte."
                });
            }

            // ==========================================
            // CREAR USUARIO (SIN BIOMETRÍA EN REGISTRO GENERAL)
            // ==========================================

            usuario.clave =
                utilidades.EncriptarClave(
                    usuario.clave
                );

            usuario.Rol = "Cliente";
            usuario.FechaRegistro = DateTime.Now;
            usuario.EstadoActivo = false;
            usuario.UltimaConexion = DateTime.Now;

            // Valores de voz nulos para nuevos registros
            usuario.AudioRegistro = null;
            usuario.EmbeddingVoz = null;
            usuario.FraseVoz = null;
            usuario.VozHabilitadaLogin = false;

            Usuario usuarioCreado =
                await _UsuarioService
                    .SaveUsuario(usuario);

            if (usuarioCreado.Id > 0)
            {
                // ==========================================
                // ELIMINAR CÓDIGO Y ESTADO DE REGISTRO
                // ==========================================

                HttpContext.Session.Remove("HashCodigoRegistro");
                HttpContext.Session.Remove("CodigoRegistro");
                HttpContext.Session.Remove("CodigoRegistroCorreo");
                HttpContext.Session.Remove("CodigoRegistroExpira");
                HttpContext.Session.Remove("CodigoRegistroVerificado");
                HttpContext.Session.Remove("CorreoRegistroVerificado");
                HttpContext.Session.Remove("IntentosCodigoRegistro");


                TempData["Mensaje"] =
                    "Usuario registrado exitosamente";


                return Json(new
                {
                    ok = true,

                    mensaje =
                        "Usuario registrado exitosamente",

                    redirectUrl =
                        Url.Action(
                            "IniciarSesion",
                            "Login"
                        )
                });
            }

            return Json(new
            {
                ok = false,
                mensaje =
                    "No se pudo crear el usuario."
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult IniciarSesion()
        {
            if (TempData["Mensaje"] != null)
            {
                ViewBag.MensajeRegistro = TempData["Mensaje"];
            }

            ViewBag.TurnstileSiteKey = _turnstileService.ObtenerSiteKey();

            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> IniciarSesion(string dni, string clave)
        {
            try
            {
                // ==========================================
                // VERIFICACIÓN DE SEGURIDAD CAPTCHA (TURNSTILE)
                // ==========================================
                string? turnstileToken = Request.Form["cf-turnstile-response"].FirstOrDefault();
                string? ipRemota = HttpContext.Connection.RemoteIpAddress?.ToString();
                var resTurnstile = await _turnstileService.ValidarTokenAsync(turnstileToken, ipRemota);
                if (!resTurnstile.Valido)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = resTurnstile.Mensaje
                    });
                }
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

                var usuarioRegistrado =
                    await _Context.Usuario.FirstOrDefaultAsync(x => x.Dni == dni);

                if (usuarioRegistrado == null)
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

                if (usuarioRegistrado.clave != claveEncriptada)
                {
                    // Alerta de seguridad preventiva: Intento de acceso fallido (Cooldown 5 min por DNI)
                    try
                    {
                        DateTime ahora = DateTime.Now;
                        bool enviarAlerta = false;

                        _alertasFallidasCache.AddOrUpdate(dni,
                            (ahora, 1),
                            (key, anterior) =>
                            {
                                if ((ahora - anterior.UltimoEnvio).TotalMinutes >= 5)
                                {
                                    enviarAlerta = true;
                                    return (ahora, anterior.ContadorFallos + 1);
                                }
                                return (anterior.UltimoEnvio, anterior.ContadorFallos + 1);
                            });

                        if (_alertasFallidasCache.TryGetValue(dni, out var registroAlerta) && registroAlerta.ContadorFallos == 1)
                        {
                            enviarAlerta = true;
                        }

                        if (enviarAlerta && !string.IsNullOrWhiteSpace(usuarioRegistrado.Correo))
                        {
                            string correoDestino = usuarioRegistrado.Correo;
                            string nombreDestino = $"{usuarioRegistrado.Nombre} {usuarioRegistrado.Apellido}".Trim();
                            string fechaHoraStr = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _emailService.EnviarAlertaIntentoFallidoAsync(correoDestino, nombreDestino, fechaHoraStr);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[CrediPlus] Error enviando alerta de acceso fallido: {ex.Message}");
                                }
                            });
                        }
                    }
                    catch (Exception exAlerta)
                    {
                        Console.WriteLine($"[CrediPlus] Error en caché de alertas fallidas: {exAlerta.Message}");
                    }

                    return Json(new
                    {
                        ok = false,
                        mensaje = "La contraseña es incorrecta."
                    });
                }

                // Credenciales correctas: limpiar registro de intentos fallidos
                _alertasFallidasCache.TryRemove(dni, out _);

                // Alerta de seguridad preventiva: Detectamos acceso correcto con DNI y contraseña
                if (!string.IsNullOrWhiteSpace(usuarioRegistrado.Correo))
                {
                    string correoDestino = usuarioRegistrado.Correo;
                    string nombreDestino = $"{usuarioRegistrado.Nombre} {usuarioRegistrado.Apellido}".Trim();
                    string dispositivoInfo = ObtenerDispositivoCliente();
                    string fechaHoraStr = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.EnviarAlertaAccesoCorrectoAsync(correoDestino, nombreDestino, dispositivoInfo, fechaHoraStr);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[CrediPlus] Error enviando alerta de acceso correcto: {ex.Message}");
                        }
                    });
                }

                Usuario usuarioEncontrado = usuarioRegistrado;

                // ==========================================
                // GUARDAR USUARIO PENDIENTE
                // ==========================================

                HttpContext.Session.SetString(
    "DniLoginPendiente",
    usuarioEncontrado.Dni
);
                // ==========================================
                // CONSULTAR BLOQUEO DE VOZ DE ESTA CUENTA
                // ==========================================

                string claveBloqueoVoz =
                    "BloqueoVoz_" + usuarioEncontrado.Dni;

                string bloqueoTexto =
                    HttpContext.Session.GetString(
                        claveBloqueoVoz
                    );

                bool vozBloqueada = false;
                int segundosBloqueoVoz = 0;

                if (!string.IsNullOrWhiteSpace(bloqueoTexto) &&
                    long.TryParse(
                        bloqueoTexto,
                        out long bloqueoHastaUnix))
                {
                    long ahoraUnix =
                        DateTimeOffset.UtcNow
                            .ToUnixTimeSeconds();

                    if (ahoraUnix < bloqueoHastaUnix)
                    {
                        vozBloqueada = true;

                        segundosBloqueoVoz =
                            (int)(
                                bloqueoHastaUnix -
                                ahoraUnix
                            );
                    }
                    else
                    {
                        // El bloqueo ya terminó.
                        HttpContext.Session.Remove(
                            claveBloqueoVoz
                        );

                        HttpContext.Session.Remove(
                            "IntentosVoz_" +
                            usuarioEncontrado.Dni
                        );
                    }
                }

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

                    vozHabilitada =
        usuarioEncontrado.VozHabilitadaLogin,

                    mostrarVerificacion = true,

                    correo =
        usuarioEncontrado.Correo,

                    fraseVoz =
        usuarioEncontrado.FraseVoz,

                    // Bloqueo exclusivo de esta cuenta
                    vozBloqueada = vozBloqueada,

                    segundosBloqueoVoz =
        segundosBloqueoVoz
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
        public async Task<IActionResult> RegistrarVozLogin(
    IFormFile? audio,
    string? fraseVoz)
        {
            try
            {
                string? dniLoginPendiente =
    HttpContext.Session.GetString(
        "DniLoginPendiente"
    );
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
                // ==========================================
                // VALIDAR FRASE RECONOCIDA
                // ==========================================

                if (string.IsNullOrWhiteSpace(fraseVoz))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se recibió la frase de seguridad."
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

                usuario.AudioRegistro =
                    "/audiosRegistro/" + nombreAudio;

                // ==========================================
                // GENERAR EMBEDDING BIOMÉTRICO
                // ==========================================

                float[] embedding =
                    await _ServicioEmbeddingVoz
                        .GenerarEmbeddingDesdeAudio(audio);

                if (embedding == null ||
                    embedding.Length != 192)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se pudo generar correctamente la identificación de voz."
                    });
                }

                // ==========================================
                // CONVERTIR FLOAT[] A BYTE[]
                // ==========================================

                usuario.EmbeddingVoz =
                    new byte[embedding.Length * sizeof(float)];

                Buffer.BlockCopy(
                    embedding,
                    0,
                    usuario.EmbeddingVoz,
                    0,
                    usuario.EmbeddingVoz.Length
                );
                // ==========================================
                // GUARDAR FRASE QUE EL USUARIO CONFIRMÓ
                // ==========================================

                usuario.FraseVoz =
                    fraseVoz.Trim();
                // ==========================================
                // ACABA DE REGISTRAR SU VOZ
                // TODAVÍA DEBE VALIDARSE POR CORREO
                // ==========================================

                usuario.VozHabilitadaLogin = false;
                _Context.Usuario.Update(usuario);

                await _Context.SaveChangesAsync();

                return Json(new
                {
                    ok = true,
                    mensaje =
                        "Registro de voz realizado correctamente."
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

        // ==========================================
        // MODELO VOSK Y TRANSCRIPCIÓN LOCAL

                // ==========================================
        // MODELO VOSK Y TRANSCRIPCIÓN LOCAL
        // ==========================================
        private static Model? _modeloVosk;
        private static readonly object _lockVosk = new object();

        private static Model? ObtenerModeloVosk()
        {
            if (_modeloVosk != null) return _modeloVosk;

            lock (_lockVosk)
            {
                if (_modeloVosk != null) return _modeloVosk;

                try
                {
                    string[] rutasCandidatas = new[]
                    {
                        Path.Combine(Directory.GetCurrentDirectory(), "ModelosVoz", "vosk-model-small-es-0.42"),
                        Path.Combine(AppContext.BaseDirectory, "ModelosVoz", "vosk-model-small-es-0.42"),
                        Path.Combine("/app", "ModelosVoz", "vosk-model-small-es-0.42"),
                        Path.Combine("/src", "trabajo", "ModelosVoz", "vosk-model-small-es-0.42"),
                        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "trabajo", "ModelosVoz", "vosk-model-small-es-0.42")),
                        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "ModelosVoz", "vosk-model-small-es-0.42"))
                    };

                    string? rutaValida = null;
                    foreach (var ruta in rutasCandidatas)
                    {
                        if (Directory.Exists(ruta))
                        {
                            rutaValida = ruta;
                            break;
                        }
                    }

                    if (rutaValida != null)
                    {
                        _modeloVosk = new Model(rutaValida);
                        Console.WriteLine("Diagnóstico técnico Vosk: Modelo cargado correctamente desde carpeta existente.");
                    }
                    else
                    {
                        Console.WriteLine("Aviso técnico Vosk: No se encontró la carpeta del modelo 'vosk-model-small-es-0.42' en las rutas del servidor/contenedor.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Aviso técnico Vosk: Error cargando el modelo: " + ex.Message);
                }

                return _modeloVosk;
            }
        }

        private async Task<byte[]> ConvertirAudioAWavPcm16k(IFormFile audio)
        {
            string ruta = await _ServicioEmbeddingVoz.ConvertirAudioAWavPcm16k(audio);
            try
            {
                return await System.IO.File.ReadAllBytesAsync(ruta);
            }
            finally
            {
                try { if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta); } catch { }
            }
        }

        private static string TranscribirAudioConVosk(string rutaWav)
        {
            if (string.IsNullOrWhiteSpace(rutaWav) || !System.IO.File.Exists(rutaWav)) return "";
            try
            {
                var model = ObtenerModeloVosk();
                if (model == null) return "";

                using var fs = new FileStream(rutaWav, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < 12) return "";

                // Parsear encabezado RIFF/WAV para localizar el bloque "data" sin asumir tamaño fijo
                byte[] header = new byte[12];
                if (fs.Read(header, 0, 12) < 12) return "";

                long pcmStart = 0;
                long pcmLength = fs.Length;

                if (header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
                    header[8] == 'W' && header[9] == 'A' && header[10] == 'V' && header[11] == 'E')
                {
                    byte[] chunkHeader = new byte[8];
                    while (fs.Position + 8 <= fs.Length)
                    {
                        if (fs.Read(chunkHeader, 0, 8) < 8) break;
                        string chunkId = Encoding.ASCII.GetString(chunkHeader, 0, 4);
                        int subChunkSize = BitConverter.ToInt32(chunkHeader, 4);
                        if (subChunkSize < 0) break;

                        if (chunkId == "data")
                        {
                            pcmStart = fs.Position;
                            pcmLength = Math.Min(subChunkSize, fs.Length - pcmStart);
                            break;
                        }

                        fs.Position += subChunkSize + ((subChunkSize & 1) == 1 ? 1 : 0);
                    }
                }

                fs.Position = pcmStart;
                using var rec = new VoskRecognizer(model, 16000.0f);
                var sb = new StringBuilder();

                const int chunkSize = 4096;
                byte[] buffer = new byte[chunkSize];
                long totalLeido = 0;

                while (totalLeido < pcmLength)
                {
                    int porLeer = (int)Math.Min(chunkSize, pcmLength - totalLeido);
                    int leidos = fs.Read(buffer, 0, porLeer);
                    if (leidos <= 0) break;
                    totalLeido += leidos;

                    if (rec.AcceptWaveform(buffer, leidos))
                    {
                        string resJson = rec.Result();
                        string seg = ExtraerTextoDeJsonVosk(resJson);
                        if (!string.IsNullOrWhiteSpace(seg))
                        {
                            if (sb.Length > 0) sb.Append(" ");
                            sb.Append(seg.Trim());
                        }
                    }
                }

                string finalJson = rec.FinalResult();
                string segFinal = ExtraerTextoDeJsonVosk(finalJson);
                if (!string.IsNullOrWhiteSpace(segFinal))
                {
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append(segFinal.Trim());
                }

                return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Aviso: Error durante transcripción Vosk desde archivo: " + ex.Message);
            }
            return "";
        }

        private static ReadOnlyMemory<byte> ExtraerPcmDeWav(byte[] wavBytes)
        {
            if (wavBytes == null || wavBytes.Length < 12)
                return ReadOnlyMemory<byte>.Empty;

            // Analizar la estructura RIFF/WAV para localizar el bloque "data" sin asumir tamaño fijo de encabezado
            if (wavBytes[0] == 'R' && wavBytes[1] == 'I' && wavBytes[2] == 'F' && wavBytes[3] == 'F' &&
                wavBytes[8] == 'W' && wavBytes[9] == 'A' && wavBytes[10] == 'V' && wavBytes[11] == 'E')
            {
                int offset = 12;
                while (offset + 8 <= wavBytes.Length)
                {
                    string chunkId = Encoding.ASCII.GetString(wavBytes, offset, 4);
                    int chunkSize = BitConverter.ToInt32(wavBytes, offset + 4);

                    if (chunkSize < 0) break;

                    if (chunkId == "data")
                    {
                        int dataStart = offset + 8;
                        int dataLen = Math.Min(chunkSize, wavBytes.Length - dataStart);
                        if (dataLen > 0)
                        {
                            return new ReadOnlyMemory<byte>(wavBytes, dataStart, dataLen);
                        }
                        break;
                    }

                    offset += 8 + chunkSize;
                    if ((chunkSize & 1) == 1) offset++; // Alinear a word si es impar
                }
            }

            return new ReadOnlyMemory<byte>(wavBytes);
        }

        private static string ExtraerTextoDeJsonVosk(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("text", out var textProp))
                {
                    return textProp.GetString() ?? "";
                }
            }
            catch { }
            return "";
        }

        private static string TranscribirAudioConVosk(byte[] wavBytes)
        {
            if (wavBytes == null || wavBytes.Length == 0) return "";
            try
            {
                var model = ObtenerModeloVosk();
                if (model == null) return "";

                ReadOnlyMemory<byte> pcmData = ExtraerPcmDeWav(wavBytes);
                if (pcmData.Length == 0) return "";

                using var rec = new VoskRecognizer(model, 16000.0f);
                var sb = new StringBuilder();

                const int chunkSize = 4096;
                ReadOnlySpan<byte> pcmSpan = pcmData.Span;
                byte[] buffer = new byte[chunkSize];

                for (int i = 0; i < pcmSpan.Length; i += chunkSize)
                {
                    int len = Math.Min(chunkSize, pcmSpan.Length - i);
                    pcmSpan.Slice(i, len).CopyTo(buffer);

                    if (rec.AcceptWaveform(buffer, len))
                    {
                        string resJson = rec.Result();
                        string seg = ExtraerTextoDeJsonVosk(resJson);
                        if (!string.IsNullOrWhiteSpace(seg))
                        {
                            if (sb.Length > 0) sb.Append(" ");
                            sb.Append(seg.Trim());
                        }
                    }
                }

                string finalJson = rec.FinalResult();
                string segFinal = ExtraerTextoDeJsonVosk(finalJson);
                if (!string.IsNullOrWhiteSpace(segFinal))
                {
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append(segFinal.Trim());
                }

                return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Aviso: Error durante transcripción Vosk: " + ex.Message);
            }
            return "";
        }

        private static string NormalizarTextoVoz(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            string normalizado = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalizado)
            {
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat != UnicodeCategory.NonSpacingMark)
                {
                    if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                    {
                        sb.Append(c);
                    }
                }
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        private static bool VerificarCoincidenciaPalabraClave(string? transcripcion, string? fraseEsperada)
        {
            string trans = NormalizarTextoVoz(transcripcion);
            string esp = NormalizarTextoVoz(fraseEsperada);

            if (string.IsNullOrWhiteSpace(esp) || string.IsNullOrWhiteSpace(trans))
            {
                return false;
            }

            // Exigir coincidencia completa con la palabra o frase registrada, sin coincidencias parciales
            return string.Equals(trans, esp, StringComparison.Ordinal);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> VerificarVozLogin(
            IFormFile? audio)
        {
            try
            {
                string dniLoginPendiente =
                    HttpContext.Session.GetString("DniLoginPendiente") ?? "";

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
                // COMPROBAR BLOQUEO DE VOZ
                // ==========================================
                string claveBloqueo = "BloqueoVoz_" + dniLoginPendiente;
                string claveIntentos = "IntentosVoz_" + dniLoginPendiente;

                string bloqueoTexto = HttpContext.Session.GetString(claveBloqueo);

                if (!string.IsNullOrWhiteSpace(bloqueoTexto) &&
                    long.TryParse(bloqueoTexto, out long bloqueoHastaUnix))
                {
                    long ahoraUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    if (ahoraUnix < bloqueoHastaUnix)
                    {
                        int segundosRestantes = (int)(bloqueoHastaUnix - ahoraUnix);
                        return Json(new
                        {
                            ok = false,
                            bloqueadoVoz = true,
                            segundosRestantes = segundosRestantes,
                            mensaje = "La verificación por voz está bloqueada temporalmente por demasiados intentos fallidos."
                        });
                    }

                    HttpContext.Session.Remove(claveBloqueo);
                    HttpContext.Session.Remove(claveIntentos);
                }

                // ==========================================
                // COMPROBAR AUDIO (No penaliza intentos)
                // ==========================================
                if (audio == null || audio.Length == 0)
                {
                    return Json(new
                    {
                        ok = false,
                        esErrorAudio = true,
                        mensaje = "No se recibió ninguna grabación de voz."
                    });
                }

                if (audio.Length < 1500)
                {
                    return Json(new
                    {
                        ok = false,
                        esErrorAudio = true,
                        mensaje = "La grabación no contiene audio suficiente. Por favor, verifica el micrófono y habla claramente."
                    });
                }

                if (audio.Length > 5 * 1024 * 1024)
                {
                    return Json(new
                    {
                        ok = false,
                        esErrorAudio = true,
                        mensaje = "El audio no puede superar los 5 MB."
                    });
                }

                // ==========================================
                // BUSCAR USUARIO
                // ==========================================
                Usuario? usuario = await _Context.Usuario.AsNoTracking().FirstOrDefaultAsync(x => x.Dni == dniLoginPendiente);
                if (usuario == null)
                {
                    Console.WriteLine("Usuario localizado: NO");
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró el usuario."
                    });
                }

                // ==========================================
                // COMPROBAR VOZ HABILITADA
                // ==========================================
                if (!usuario.VozHabilitadaLogin)
                {
                    return Json(new
                    {
                        ok = false,
                        requiereCorreoPrimero = true,
                        mensaje = "Por seguridad debes iniciar sesión una vez mediante correo electrónico antes de utilizar el acceso por voz."
                    });
                }

                // ==========================================
                // COMPROBAR VOZ Y PALABRA REGISTRADAS
                // ==========================================
                if (usuario.EmbeddingVoz == null || usuario.EmbeddingVoz.Length == 0)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No tienes una identificación de voz registrada."
                    });
                }

                if (usuario.EmbeddingVoz.Length != 192 * sizeof(float))
                {
                    return Json(new
                    {
                        ok = false,
                        necesitaRegrabarVoz = true,
                        mensaje = "Tu registro de voz anterior no tiene el formato biométrico correcto. Debes registrar nuevamente tu voz."
                    });
                }

                if (string.IsNullOrWhiteSpace(usuario.FraseVoz))
                {
                    return Json(new
                    {
                        ok = false,
                        necesitaRegrabarVoz = true,
                        mensaje = "No tienes una palabra clave de seguridad configurada. Debes registrar nuevamente tu voz."
                    });
                }

                // ==========================================
                // RECUPERAR EMBEDDING REGISTRADO
                // ==========================================
                float[] embeddingRegistrado = new float[192];
                Buffer.BlockCopy(
                    usuario.EmbeddingVoz,
                    0,
                    embeddingRegistrado,
                    0,
                    usuario.EmbeddingVoz.Length
                );

                string? rutaWavCompartida = null;
                var cronometroTotal = Stopwatch.StartNew();
                long heapInicioTotal = GC.GetTotalMemory(forceFullCollection: false);
                long ramInicioTotal = Process.GetCurrentProcess().WorkingSet64;

                try
                {
                    // ==========================================
                    // CONVERSIÓN ÚNICA FFMPEG (Compartida entre ECAPA y Vosk)
                    // ==========================================
                    try
                    {
                        rutaWavCompartida = await _ServicioEmbeddingVoz.ConvertirAudioAWavPcm16k(audio);
                    }
                    catch (Exception exConv)
                    {
                        Console.WriteLine("Aviso: Error en conversión FFmpeg: " + exConv.Message);
                        return Json(new
                        {
                            ok = false,
                            esErrorAudio = true,
                            mensaje = "No se pudo procesar el formato del audio grabado. Por favor, verifica el micrófono e inténtalo nuevamente."
                        });
                    }

                    bool ffmpegOk = !string.IsNullOrWhiteSpace(rutaWavCompartida) && System.IO.File.Exists(rutaWavCompartida);
                    if (!ffmpegOk)
                    {
                        return Json(new
                        {
                            ok = false,
                            esErrorAudio = true,
                            mensaje = "No se pudo procesar el formato del audio grabado."
                        });
                    }

                    // ==========================================
                    // VALIDACIÓN A: BIOMETRÍA ECAPA-TDNN (ONNX)
                    // ==========================================
                    List<float[]> embeddingsActuales;
                    try
                    {
                        embeddingsActuales = await _ServicioEmbeddingVoz.GenerarEmbeddingsCandidatos(rutaWavCompartida);
                    }
                    catch (InvalidOperationException exNivel)
                    {
                        // No se detectó nivel de voz suficiente (micrófono mudo/silencio)
                        // No penalizar con intentos fallidos
                        return Json(new
                        {
                            ok = false,
                            esErrorAudio = true,
                            mensaje = exNivel.Message
                        });
                    }

                    if (embeddingsActuales == null || embeddingsActuales.Count == 0)
                    {
                        return Json(new
                        {
                            ok = false,
                            esErrorAudio = true,
                            mensaje = "No se pudo analizar correctamente la voz. Inténtalo nuevamente."
                        });
                    }

                    double similitud = 0;
                    foreach (float[] embActual in embeddingsActuales)
                    {
                        if (embActual != null && embActual.Length == 192)
                        {
                            double sim = CalcularSimilitudCoseno(embeddingRegistrado, embActual);
                            if (sim > similitud)
                            {
                                similitud = sim;
                            }
                        }
                    }

                    const double UMBRAL_VOZ = 0.65;
                    bool biometriaValida = similitud >= UMBRAL_VOZ;

                    // ==========================================
                    // VALIDACIÓN B: PALABRA CLAVE (EXCLUSIVAMENTE VOSK EN SERVIDOR)
                    // (Reutiliza el archivo WAV compartido sin ejecutar FFmpeg de nuevo)
                    // ==========================================
                    Model? modeloVosk = ObtenerModeloVosk();
                    bool modeloCargado = modeloVosk != null;

                    string transcripcionServidor = TranscribirAudioConVosk(rutaWavCompartida);
                    bool transcripcionVacia = string.IsNullOrWhiteSpace(transcripcionServidor);

                    bool palabraValida = VerificarCoincidenciaPalabraClave(transcripcionServidor, usuario.FraseVoz);

                    // ==========================================
                    // LOGS SEGUROS (Sin datos sensibles ni palabras secretas)
                    // ==========================================
                    Console.WriteLine("==========================================");
                    Console.WriteLine("VERIFICACIÓN INDEPENDIENTE DE VOZ (FFMPEG ÚNICO)");
                    Console.WriteLine($"BIOMETRÍA ECAPA-TDNN: {(biometriaValida ? "CORRECTA" : "RECHAZADA")} (Similitud: {similitud:F4})");
                    Console.WriteLine($"PALABRA CLAVE: {(palabraValida ? "CORRECTA" : "INCORRECTA")}");
                    Console.WriteLine("--- DIAGNÓSTICO TÉCNICO SEGURO ---");
                    Console.WriteLine("Usuario localizado: SÍ");
                    Console.WriteLine($"FraseVoz recuperada: {(usuario.FraseVoz != null ? "SÍ" : "NO")}");
                    Console.WriteLine($"FraseVoz vacía o NULL: {(string.IsNullOrWhiteSpace(usuario.FraseVoz) ? "SÍ" : "NO")}");
                    Console.WriteLine($"Modelo Vosk cargado: {(modeloCargado ? "SÍ" : "NO")}");
                    Console.WriteLine($"FFmpeg ejecutado correctamente: {(ffmpegOk ? "SÍ" : "NO")}");
                    Console.WriteLine($"Transcripción Vosk vacía: {(transcripcionVacia ? "SÍ" : "NO")}");
                    Console.WriteLine($"Coincidencia exacta después de normalizar: {(palabraValida ? "SÍ" : "NO")}");
                    Console.WriteLine($"RESULTADO GENERAL: {(biometriaValida && palabraValida ? "ACCESO CONCEDIDO" : "ACCESO DENEGADO")}");
                    Console.WriteLine("==========================================");

                // ==========================================
                // EVALUACIÓN COMBINADA: AMBAS DEBEN SER CORRECTAS
                // ==========================================
                if (!biometriaValida || !palabraValida)
                {
                    int intentosFallidos = HttpContext.Session.GetInt32(claveIntentos) ?? 0;
                    intentosFallidos++;

                    if (intentosFallidos >= MAX_INTENTOS_VOZ)
                    {
                        long bloqueoHasta = DateTimeOffset.UtcNow
                            .AddMinutes(MINUTOS_BLOQUEO_VOZ)
                            .ToUnixTimeSeconds();

                        HttpContext.Session.SetString(claveBloqueo, bloqueoHasta.ToString());
                        HttpContext.Session.Remove(claveIntentos);

                        Console.WriteLine("🔒 VERIFICACIÓN POR VOZ BLOQUEADA (5 minutos)");

                        return Json(new
                        {
                            ok = false,
                            bloqueadoVoz = true,
                            biometriaValida = biometriaValida,
                            palabraValida = palabraValida,
                            segundosRestantes = 300,
                            intentosRestantes = 0,
                            mensaje = "Has agotado los 3 intentos de verificación por voz. Esta opción ha sido bloqueada durante 5 minutos."
                        });
                    }

                    HttpContext.Session.SetInt32(claveIntentos, intentosFallidos);
                    int intentosRestantes = MAX_INTENTOS_VOZ - intentosFallidos;

                    string motivo;
                    string mensajeResultado;

                    if (biometriaValida && !palabraValida)
                    {
                        motivo = "PalabraIncorrecta";
                        mensajeResultado = intentosRestantes == 1
                            ? "La identidad biométrica es correcta, pero la palabra clave no coincide. Te queda 1 intento."
                            : $"La identidad biométrica es correcta, pero la palabra clave no coincide. Te quedan {intentosRestantes} intentos.";
                    }
                    else if (!biometriaValida && palabraValida)
                    {
                        motivo = "BiometriaIncorrecta";
                        mensajeResultado = intentosRestantes == 1
                            ? "La palabra clave fue reconocida, pero la identidad biométrica no coincide. Te queda 1 intento."
                            : $"La palabra clave fue reconocida, pero la identidad biométrica no coincide. Te quedan {intentosRestantes} intentos.";
                    }
                    else
                    {
                        motivo = "AmbasIncorrectas";
                        mensajeResultado = intentosRestantes == 1
                            ? "Ni la identidad biométrica ni la palabra clave coinciden. Te queda 1 intento."
                            : $"Ni la identidad biométrica ni la palabra clave coinciden. Te quedan {intentosRestantes} intentos.";
                    }

                    return Json(new
                    {
                        ok = false,
                        bloqueadoVoz = false,
                        biometriaValida = biometriaValida,
                        palabraValida = palabraValida,
                        motivo = motivo,
                        intentosRestantes = intentosRestantes,
                        similitud = Math.Round(similitud, 4),
                        mensaje = mensajeResultado
                    });
                }

                // ==========================================
                // AMBAS VERIFICACIONES EXITOSAS
                // ==========================================
                HttpContext.Session.Remove("IntentosVoz_" + usuario.Dni);

                List<Claim> claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usuario.Nombre),
                    new Claim("Apellido", usuario.Apellido),
                    new Claim("Dni", usuario.Dni),
                    new Claim("Celular", usuario.Celular),
                    new Claim("Correo", usuario.Correo),
                    new Claim(ClaimTypes.Role, usuario.Rol)
                };

                ClaimsIdentity claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme
                );

                AuthenticationProperties properties = new AuthenticationProperties
                {
                    AllowRefresh = true
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    properties
                );

                usuario.EstadoActivo = true;
                usuario.UltimaConexion = DateTime.Now;
                await _Context.SaveChangesAsync();

                string url;
                if (usuario.Rol == "Analista")
                {
                    url = Url.Action("ProgramaAnalista", "Analista");
                }
                else if (usuario.Rol == "Administrador")
                {
                    url = Url.Action("ProgramaAdministrador", "Administrador");
                }
                else
                {
                    url = Url.Action("DashboardCliente", "Login");
                }

                HttpContext.Session.Remove("DniLoginPendiente");

                return Json(new
                {
                    ok = true,
                    biometriaValida = true,
                    palabraValida = true,
                    similitud = Math.Round(similitud, 4),
                    mensaje = "Voz y palabra clave verificadas correctamente.",
                    redirectUrl = url
                });
                }
                finally
                {
                    // ==========================================
                    // LIMPIEZA DEL ARCHIVO WAV COMPARTIDO
                    // ==========================================
                    if (!string.IsNullOrWhiteSpace(rutaWavCompartida) && System.IO.File.Exists(rutaWavCompartida))
                    {
                        try { System.IO.File.Delete(rutaWavCompartida); } catch { }
                    }

                    cronometroTotal.Stop();
                    long heapFinTotal = GC.GetTotalMemory(forceFullCollection: false);
                    long ramFinTotal = Process.GetCurrentProcess().WorkingSet64;

                    Console.WriteLine($"[DIAGNÓSTICO LOGIN TOTAL] Duración: {cronometroTotal.ElapsedMilliseconds} ms | Heap .NET: {heapInicioTotal / 1048576.0:F1} MB -> {heapFinTotal / 1048576.0:F1} MB | RAM Proceso (WorkingSet): {ramInicioTotal / 1048576.0:F1} MB -> {ramFinTotal / 1048576.0:F1} MB");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR VERIFICANDO VOZ: " + ex.Message);
                return Json(new
                {
                    ok = false,
                    mensaje = "No se pudo verificar la voz: " + ex.Message
                });
            }
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult OlvideContrasena()
        {
            bool validado =
                HttpContext.Session.GetString(
                    "RecuperacionValidada"
                ) == "true";

            ViewBag.RecuperacionValidada = validado;
            ViewBag.TurnstileSiteKey = _turnstileService.ObtenerSiteKey();

            if (validado)
            {
                ViewBag.DniRecuperacion =
                    HttpContext.Session.GetString(
                        "RecuperacionDni"
                    );

                ViewBag.CorreoRecuperacion =
                    HttpContext.Session.GetString(
                        "RecuperacionCorreo"
                    );
            }

            return View();
        }


        // ==========================================
        // SOLICITAR RECUPERACIÓN
        // ==========================================
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SolicitarRecuperacion(
            string dni,
            string correo)
        {
            try
            {
                // ==========================================
                // VERIFICACIÓN DE SEGURIDAD CAPTCHA (TURNSTILE)
                // ==========================================
                string? turnstileToken = Request.Form["cf-turnstile-response"].FirstOrDefault();
                string? ipRemota = HttpContext.Connection.RemoteIpAddress?.ToString();
                var resTurnstile = await _turnstileService.ValidarTokenAsync(turnstileToken, ipRemota);
                if (!resTurnstile.Valido)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = resTurnstile.Mensaje
                    });
                }
                // ==========================================
                // VALIDAR DNI
                // ==========================================

                if (string.IsNullOrWhiteSpace(dni) ||
                    !Regex.IsMatch(dni, @"^\d{8}$"))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "El DNI debe tener exactamente 8 números."
                    });
                }

                // ==========================================
                // VALIDAR CORREO
                // ==========================================

                if (string.IsNullOrWhiteSpace(correo) ||
                    !EsCorreoValidoDominio(correo))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "Ingresa un correo válido (Gmail, Hotmail o Outlook)."
                    });
                }

                correo =
                    correo.Trim().ToLowerInvariant();

                // ==========================================
                // BUSCAR USUARIO
                // ==========================================

                Usuario usuario =
                    _Context.Usuario.FirstOrDefault(
                        x =>
                            x.Dni == dni &&
                            x.Correo.ToLower() == correo
                    );

                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "El DNI y el correo no corresponden a la misma cuenta."
                    });
                }

                // ==========================================
                // CLAVE ÚNICA DNI + CORREO
                // ==========================================

                string claveRecuperacion =
                    dni + "|" + correo;

                // ==========================================
                // VERIFICAR SI YA HAY CÓDIGO VIGENTE
                // ==========================================

                if (
                    expiracionesRecuperacion
                        .TryGetValue(
                            claveRecuperacion,
                            out DateTime expiracionActual
                        )
                    &&
                    DateTime.UtcNow < expiracionActual
                )
                {
                    int segundosRestantes =
                        (int)Math.Ceiling(
                            (
                                expiracionActual -
                                DateTime.UtcNow
                            ).TotalSeconds
                        );

                    return Json(new
                    {
                        ok = false,
                        esperando = true,
                        segundosRestantes =
                            segundosRestantes,

                        mensaje =
                            $"Ya enviamos un código. Espera {segundosRestantes} segundos para solicitar otro."
                    });
                }

                // ==========================================
                // GENERAR CÓDIGO RANDOM
                // ==========================================

                const string caracteres =
                    "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

                char[] codigoArray =
                    new char[6];

                for (int i = 0; i < 6; i++)
                {
                    int posicion =
                        RandomNumberGenerator.GetInt32(
                            caracteres.Length
                        );

                    codigoArray[i] =
                        caracteres[posicion];
                }

                string codigo =
                    new string(codigoArray);

                // ==========================================
                // EXPIRACIÓN: 1 MINUTO
                // ==========================================

                DateTime expiracion =
                    DateTime.UtcNow.AddMinutes(1);

                // ==========================================
                // GUARDAR CÓDIGO TEMPORALMENTE
                // ==========================================

                codigosRecuperacion[
                    claveRecuperacion
                ] = codigo;

                expiracionesRecuperacion[
                    claveRecuperacion
                ] = expiracion;

                // ==========================================
                // LIMPIAR VALIDACIÓN ANTERIOR
                // ==========================================

                HttpContext.Session.Remove(
                    "RecuperacionValidada"
                );

                HttpContext.Session.Remove(
                    "RecuperacionDni"
                );

                HttpContext.Session.Remove(
                    "RecuperacionCorreo"
                );

                // ==========================================
                // CORREO PROFESIONAL
                // ==========================================

                string cuerpoHtml = $@"
<div style='
    margin:0;
    padding:40px 15px;
    background:#f1f5f9;
    font-family:Arial,Helvetica,sans-serif;
'>

    <div style='
        max-width:600px;
        margin:auto;
        background:white;
        border-radius:22px;
        overflow:hidden;
        box-shadow:0 15px 40px rgba(15,23,42,.12);
    '>

        <div style='
            padding:32px;
            text-align:center;
            color:white;
            background:linear-gradient(
                135deg,
                #4361ee,
                #7209b7
            );
        '>

            <div style='font-size:42px;'>
                🔐
            </div>

            <div style='
                font-size:30px;
                font-weight:700;
                margin-top:8px;
            '>
                CrediPlus
            </div>

            <div style='
                font-size:15px;
                margin-top:6px;
                opacity:.9;
            '>
                Recuperación segura de contraseña
            </div>

        </div>

        <div style='
            padding:38px 35px;
        '>

            <h2 style='
                color:#312e81;
                margin-top:0;
            '>
                Código de verificación
            </h2>

            <p style='
                color:#475569;
                font-size:16px;
                line-height:1.7;
            '>
                Hola <strong>{usuario.Nombre}</strong>,
            </p>

            <p style='
                color:#475569;
                font-size:16px;
                line-height:1.7;
            '>
                Usa el siguiente código para continuar
                con la recuperación de tu contraseña.
            </p>

            <div style='
                margin:30px 0;
                padding:26px 15px;
                text-align:center;
                border-radius:16px;
                background:#eef2ff;
                border:1px solid #c7d2fe;
            '>

                <div style='
                    color:#64748b;
                    font-size:13px;
                    font-weight:bold;
                    margin-bottom:12px;
                '>
                    CÓDIGO DE SEGURIDAD
                </div>

                <div style='
                    color:#4c1d95;
                    font-size:38px;
                    font-weight:800;
                    letter-spacing:10px;
                '>
                    {codigo}
                </div>

            </div>

            <div style='
                background:#fff7ed;
                border-left:4px solid #f59e0b;
                padding:15px 17px;
                border-radius:8px;
                color:#92400e;
                font-size:14px;
                line-height:1.6;
            '>
                ⏱ Este código es válido durante
                <strong>1 minuto</strong>.
                Cuando expire tendrás que solicitar uno nuevo.
            </div>

            <div style='
                margin-top:22px;
                background:#f8fafc;
                padding:16px;
                border-radius:10px;
                color:#64748b;
                font-size:13px;
                line-height:1.6;
            '>
                🔒 No compartas este código con ninguna persona.
            </div>

        </div>

        <div style='
            background:#f8fafc;
            padding:20px;
            text-align:center;
            color:#94a3b8;
            font-size:12px;
        '>
            © {DateTime.Now.Year} CrediPlus
            <br>
            Seguridad de tu cuenta
        </div>

    </div>
</div>";

                // ==========================================
                // ENVIAR CORREO
                // ==========================================

                await _emailService.EnviarCorreoAsync(
                    usuario.Correo,
                    "Código de recuperación - CrediPlus",
                    cuerpoHtml
                );

                return Json(new
                {
                    ok = true,

                    mensaje =
                        "Código enviado correctamente. Tienes 1 minuto para utilizarlo.",

                    segundos = 60
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR RECUPERACIÓN: " +
                    ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo enviar el código de recuperación."
                });
            }
        }

        // ==========================================
        // VALIDAR BOTÓN DEL CORREO
        // ==========================================
        [HttpPost]
        [AllowAnonymous]
        public IActionResult VerificarCodigoRecuperacion(
            string dni,
            string correo,
            string codigo)
        {
            try
            {
                // ==========================================
                // VALIDACIONES
                // ==========================================

                if (string.IsNullOrWhiteSpace(dni) ||
                    !Regex.IsMatch(dni, @"^\d{8}$"))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "El DNI no es válido."
                    });
                }

                if (string.IsNullOrWhiteSpace(correo))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "El correo es obligatorio."
                    });
                }

                correo =
                    correo.Trim().ToLowerInvariant();

                codigo =
                    (codigo ?? "")
                        .Trim()
                        .ToUpperInvariant();

                if (!Regex.IsMatch(
                        codigo,
                        @"^[A-Z0-9]{6}$"))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "El código debe tener exactamente 6 caracteres."
                    });
                }

                // ==========================================
                // BUSCAR USUARIO
                // ==========================================

                Usuario usuario =
                    _Context.Usuario.FirstOrDefault(
                        x =>
                            x.Dni == dni &&
                            x.Correo.ToLower() == correo
                    );

                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se encontró una cuenta con esos datos."
                    });
                }

                string claveRecuperacion =
                    dni + "|" + correo;

                // ==========================================
                // VERIFICAR SI EXISTE CÓDIGO
                // ==========================================

                if (
                    !codigosRecuperacion.TryGetValue(
                        claveRecuperacion,
                        out string codigoGuardado
                    )
                )
                {
                    return Json(new
                    {
                        ok = false,
                        expirado = true,
                        mensaje =
                            "No existe un código activo. Solicita uno nuevo."
                    });
                }

                // ==========================================
                // VERIFICAR EXPIRACIÓN
                // ==========================================

                if (
                    !expiracionesRecuperacion.TryGetValue(
                        claveRecuperacion,
                        out DateTime expiracion
                    )
                    ||
                    DateTime.UtcNow >= expiracion
                )
                {
                    codigosRecuperacion.Remove(
                        claveRecuperacion
                    );

                    expiracionesRecuperacion.Remove(
                        claveRecuperacion
                    );

                    return Json(new
                    {
                        ok = false,
                        expirado = true,

                        mensaje =
                            "El código ha expirado. Solicita un nuevo código."
                    });
                }

                // ==========================================
                // VERIFICAR CÓDIGO
                // ==========================================

                if (!string.Equals(
                        codigo,
                        codigoGuardado,
                        StringComparison.Ordinal))
                {
                    return Json(new
                    {
                        ok = false,
                        expirado = false,

                        mensaje =
                            "El código de seguridad es incorrecto."
                    });
                }

                // ==========================================
                // CÓDIGO CORRECTO
                // ==========================================

                HttpContext.Session.SetString(
                    "RecuperacionValidada",
                    "true"
                );

                HttpContext.Session.SetString(
                    "RecuperacionDni",
                    usuario.Dni
                );

                HttpContext.Session.SetString(
                    "RecuperacionCorreo",
                    usuario.Correo
                );

                // ==========================================
                // ELIMINAR CÓDIGO YA UTILIZADO
                // ==========================================

                codigosRecuperacion.Remove(
                    claveRecuperacion
                );

                expiracionesRecuperacion.Remove(
                    claveRecuperacion
                );

                TempData["MostrarModalRecuperacion"] =
                    "true";

                return Json(new
                {
                    ok = true,

                    mensaje =
                        "Identidad verificada correctamente.",

                    redirectUrl =
                        Url.Action(
                            "OlvideContrasena",
                            "Login"
                        )
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR VALIDANDO RECUPERACIÓN: " +
                    ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo verificar el código."
                });
            }
        }


        // ==========================================
        // CAMBIAR CONTRASEÑA DESPUÉS DE VALIDAR
        // ==========================================

        [HttpPost]
        [AllowAnonymous]
        public IActionResult CambiarContrasenaRecuperada(
            string nuevaClave,
            string confirmarClave)
        {
            // ==========================================
            // COMPROBAR QUE VALIDÓ EL CORREO
            // ==========================================

            bool validado =
                HttpContext.Session.GetString(
                    "RecuperacionValidada"
                ) == "true";

            if (!validado)
            {
                return Json(new
                {
                    ok = false,
                    mensaje =
                        "Primero debes validar la recuperación desde tu correo."
                });
            }

            string dni =
                HttpContext.Session.GetString(
                    "RecuperacionDni"
                );

            if (string.IsNullOrWhiteSpace(dni))
            {
                return Json(new
                {
                    ok = false,
                    mensaje =
                        "La sesión de recuperación ha expirado."
                });
            }

            // ==========================================
            // VALIDAR CONTRASEÑA (POLÍTICA: MÍNIMO 12 CARACTERES, MAYÚSCULA, MINÚSCULA, NÚMERO Y SÍMBOLO)
            // ==========================================

            var resClave = PasswordPolicyHelper.Validar(nuevaClave);
            if (!resClave.EsValida)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = resClave.MensajeError
                });
            }

            // ==========================================
            // CONFIRMAR CONTRASEÑA
            // ==========================================

            if (nuevaClave != confirmarClave)
            {
                return Json(new
                {
                    ok = false,
                    mensaje =
                        "Las contraseñas no coinciden."
                });
            }

            // ==========================================
            // BUSCAR USUARIO
            // ==========================================

            Usuario usuario =
                _Context.Usuario.FirstOrDefault(
                    x => x.Dni == dni
                );

            if (usuario == null)
            {
                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se encontró la cuenta."
                });
            }

            // ==========================================
            // NO PERMITIR MISMA CONTRASEÑA
            // ==========================================

            string nuevaClaveEncriptada =
                utilidades.EncriptarClave(
                    nuevaClave
                );

            if (usuario.clave ==
                nuevaClaveEncriptada)
            {
                return Json(new
                {
                    ok = false,
                    mensaje =
                        "La nueva contraseña no puede ser igual a la contraseña actual."
                });
            }

            // ==========================================
            // GUARDAR
            // ==========================================

            usuario.clave =
                nuevaClaveEncriptada;

            _Context.SaveChanges();

            // ==========================================
            // BORRAR RECUPERACIÓN
            // ==========================================

            HttpContext.Session.Remove(
                "RecuperacionValidada"
            );

            HttpContext.Session.Remove(
                "RecuperacionDni"
            );

            HttpContext.Session.Remove(
                "RecuperacionCorreo"
            );


            return Json(new
            {
                ok = true,
                mensaje =
                    "Contraseña actualizada correctamente.",

                redirectUrl =
                    Url.Action(
                        "IniciarSesion",
                        "Login"
                    )
            });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> EnviarCodigo(string correo)
        {
            try
            {
                // ==========================================
                // VALIDAR CORREO
                // ==========================================

                if (string.IsNullOrWhiteSpace(correo))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Primero debes ingresar tu correo."
                    });
                }

                correo = correo.Trim().ToLowerInvariant();

                if (!EsCorreoValidoDominio(correo))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Ingresa un correo válido (Gmail, Hotmail o Outlook)."
                    });
                }

                // ==========================================
                // VERIFICAR SI EL CORREO YA ESTÁ REGISTRADO
                // ==========================================

                bool correoExiste = await _Context.Usuario.AnyAsync(x => x.Correo == correo);
                if (correoExiste)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Este correo electrónico ya está registrado en CrediPlus."
                    });
                }

                // ==========================================
                // VERIFICAR SI TODAVÍA HAY UN CÓDIGO VÁLIDO (COOLDOWN 60s)
                // ==========================================

                string expiracionTexto =
                    HttpContext.Session.GetString("CodigoRegistroExpira");

                if (long.TryParse(expiracionTexto, out long expiracionUnix))
                {
                    long ahoraUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    if (ahoraUnix < expiracionUnix)
                    {
                        int segundosRestantes = (int)(expiracionUnix - ahoraUnix);

                        return Json(new
                        {
                            ok = false,
                            esperando = true,
                            segundosRestantes = segundosRestantes,
                            mensaje = $"Ya enviamos un código. Espera {segundosRestantes} segundos para solicitar otro."
                        });
                    }
                }

                // ==========================================
                // GENERAR CÓDIGO OTP DE 6 DÍGITOS NUMÉRICOS
                // ==========================================

                int otpNum = RandomNumberGenerator.GetInt32(100000, 1000000);
                string codigoOtp = otpNum.ToString("D6");

                // ==========================================
                // SEGURIDAD: NO ALMACENAR EN TEXTO PLANO
                // SE GUARDA EL HASH CRIPTOGRÁFICO SHA-256
                // ==========================================

                string hashCodigo = utilidades.EncriptarClave(codigoOtp);

                // ==========================================
                // EXPIRA ESTRICTAMENTE EN 60 SEGUNDOS
                // ==========================================

                long expiracion = DateTimeOffset.UtcNow
                    .AddSeconds(60)
                    .ToUnixTimeSeconds();

                // ==========================================
                // GUARDAR EN SESSION
                // ==========================================

                HttpContext.Session.SetString("HashCodigoRegistro", hashCodigo);
                HttpContext.Session.SetString("CodigoRegistroCorreo", correo);
                HttpContext.Session.SetString("CodigoRegistroExpira", expiracion.ToString());
                HttpContext.Session.SetInt32("IntentosCodigoRegistro", 0);
                HttpContext.Session.Remove("CodigoRegistroVerificado");
                HttpContext.Session.Remove("CorreoRegistroVerificado");
                HttpContext.Session.Remove("CodigoRegistro");

                // ==========================================
                // ENVIAR CORREO CON PLANTILLA PROFESIONAL
                // ==========================================

                await _emailService.EnviarCodigoRegistroOtpAsync(correo, codigoOtp);

                return Json(new
                {
                    ok = true,
                    mensaje = "Código enviado correctamente. Tienes 60 segundos para utilizarlo.",
                    segundos = 60
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR ENVIANDO CÓDIGO: " + ex.Message);

                return Json(new
                {
                    ok = false,
                    mensaje = "No se pudo enviar el código de verificación al correo."
                });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult ConfirmarCodigoRegistro(string correo, string codigo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(codigo))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Debes ingresar el correo y el código de 6 dígitos."
                    });
                }

                correo = correo.Trim().ToLowerInvariant();
                codigo = codigo.Trim();

                if (codigo.Length != 6 || !Regex.IsMatch(codigo, @"^\d{6}$"))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El código debe tener exactamente 6 dígitos numéricos."
                    });
                }

                string hashGuardado = HttpContext.Session.GetString("HashCodigoRegistro");
                string correoGuardado = HttpContext.Session.GetString("CodigoRegistroCorreo");
                string expiracionTexto = HttpContext.Session.GetString("CodigoRegistroExpira");
                int intentos = HttpContext.Session.GetInt32("IntentosCodigoRegistro") ?? 0;

                if (string.IsNullOrWhiteSpace(hashGuardado) || string.IsNullOrWhiteSpace(correoGuardado))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No hay un código pendiente. Solicita uno nuevo."
                    });
                }

                if (!string.Equals(correo, correoGuardado, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El correo ingresado no coincide con el que solicitó el código."
                    });
                }

                if (intentos >= 3)
                {
                    HttpContext.Session.Remove("HashCodigoRegistro");
                    HttpContext.Session.Remove("CodigoRegistroCorreo");
                    HttpContext.Session.Remove("CodigoRegistroExpira");
                    return Json(new
                    {
                        ok = false,
                        expirado = true,
                        mensaje = "Has superado el límite de 3 intentos fallidos. Solicita un nuevo código."
                    });
                }

                if (!long.TryParse(expiracionTexto, out long expiracionUnix) ||
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiracionUnix)
                {
                    HttpContext.Session.Remove("HashCodigoRegistro");
                    HttpContext.Session.Remove("CodigoRegistroCorreo");
                    HttpContext.Session.Remove("CodigoRegistroExpira");
                    return Json(new
                    {
                        ok = false,
                        expirado = true,
                        mensaje = "El código de verificación ha expirado (límite 60s). Solicita uno nuevo."
                    });
                }

                // ==========================================
                // COMPARACIÓN SEGURA EN TIEMPO CONSTANTE
                // ==========================================

                string hashIngresado = utilidades.EncriptarClave(codigo);
                byte[] a = Encoding.UTF8.GetBytes(hashIngresado);
                byte[] b = Encoding.UTF8.GetBytes(hashGuardado);

                if (!CryptographicOperations.FixedTimeEquals(a, b))
                {
                    intentos++;
                    HttpContext.Session.SetInt32("IntentosCodigoRegistro", intentos);
                    int restantes = 3 - intentos;
                    return Json(new
                    {
                        ok = false,
                        mensaje = restantes > 0
                            ? $"Código incorrecto. Te quedan {restantes} intento(s)."
                            : "Código incorrecto. Has superado el límite de intentos. Solicita un nuevo código."
                    });
                }

                // ==========================================
                // CÓDIGO VÁLIDO: MARCAR VERIFICACIÓN EN SESIÓN
                // ==========================================

                HttpContext.Session.SetString("CodigoRegistroVerificado", "true");
                HttpContext.Session.SetString("CorreoRegistroVerificado", correo);

                // Limpiar OTP temporal para evitar reuso
                HttpContext.Session.Remove("HashCodigoRegistro");
                HttpContext.Session.Remove("CodigoRegistroExpira");

                return Json(new
                {
                    ok = true,
                    mensaje = "¡Correo electrónico verificado exitosamente!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "Error al verificar el código: " + ex.Message
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> EnviarCodigoLogin()
        {
            try
            {
                string? dniLoginPendiente =
    HttpContext.Session.GetString(
        "DniLoginPendiente"
    );
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
                string? dniLoginPendiente =
           HttpContext.Session.GetString(
               "DniLoginPendiente"
           );
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

                usuarioEncontrado.EstadoActivo =
    true;

                usuarioEncontrado.UltimaConexion =
                    DateTime.Now;


                // ==========================================
                // CORREO VERIFICADO CORRECTAMENTE
                // ==========================================

                // Solamente habilitamos voz si realmente
                // existe un registro biométrico válido.

                bool tieneVozValida =
                    !string.IsNullOrWhiteSpace(
                        usuarioEncontrado.AudioRegistro
                    )
                    &&
                    usuarioEncontrado.EmbeddingVoz != null
                    &&
                    usuarioEncontrado.EmbeddingVoz.Length ==
                        192 * sizeof(float);


                if (tieneVozValida)
                {
                    usuarioEncontrado.VozHabilitadaLogin =
                        true;
                }


                _Context.Usuario.Update(
                    usuarioEncontrado
                );

                await _Context.SaveChangesAsync();

                // Limpiar código utilizado
                codigoLogin = "";

                HttpContext.Session.Remove(
                    "DniLoginPendiente"
                );
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

        // =========================================================================
        // RENOVACIÓN SEGURA DE BIOMETRÍA Y PALABRA SECRETA DESDE EL LOGIN
        // =========================================================================

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SolicitarOtpRenovacionVoz()
        {
            try
            {
                string? dniLoginPendiente = HttpContext.Session.GetString("DniLoginPendiente");
                if (string.IsNullOrWhiteSpace(dniLoginPendiente))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La sesión de seguridad ha expirado. Por favor, inicia sesión nuevamente."
                    });
                }

                Usuario usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dniLoginPendiente);
                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró el usuario en el sistema."
                    });
                }

                // Verificar que el usuario tenga una biometría previamente registrada
                bool tieneVozPrevia = !string.IsNullOrWhiteSpace(usuario.AudioRegistro) &&
                                      usuario.EmbeddingVoz != null &&
                                      usuario.EmbeddingVoz.Length == 192 * sizeof(float);

                if (!tieneVozPrevia)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No tienes una biometría registrada previamente para actualizar."
                    });
                }

                // Límite de solicitudes (Rate limiting): mínimo 15 segundos entre solicitudes
                string? ultimaSolicitudStr = HttpContext.Session.GetString("RenovacionVoz_UltimaSolicitud");
                if (!string.IsNullOrWhiteSpace(ultimaSolicitudStr) &&
                    DateTime.TryParse(ultimaSolicitudStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime ultimaSol))
                {
                    double segundosTranscurridos = (DateTime.UtcNow - ultimaSol).TotalSeconds;
                    if (segundosTranscurridos < 15)
                    {
                        int esperar = (int)Math.Ceiling(15 - segundosTranscurridos);
                        return Json(new
                        {
                            ok = false,
                            mensaje = $"Por favor espera {esperar} segundos antes de solicitar otro código."
                        });
                    }
                }

                // Generar código OTP criptográficamente seguro de 6 dígitos
                string otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

                // Asociar exclusivamente a la operación RenovacionBiometriaVoz en sesión
                HttpContext.Session.SetString("RenovacionVoz_Otp", otp);
                HttpContext.Session.SetString("RenovacionVoz_OtpExpira", DateTime.UtcNow.AddSeconds(60).ToString("o"));
                HttpContext.Session.SetInt32("RenovacionVoz_OtpIntentos", 0);
                HttpContext.Session.SetString("RenovacionVoz_UltimaSolicitud", DateTime.UtcNow.ToString("o"));

                // Correo enmascarado para confirmación visual sin revelar datos sensibles
                string correoEnmascarado = EnmascararCorreoSeguro(usuario.Correo);

                // Enviar el correo mediante el servicio existente
                string cuerpoHtml = $@"
<div style='font-family: Arial, sans-serif; max-width: 520px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
    <div style='text-align: center; margin-bottom: 20px;'>
        <h2 style='color: #4338ca; margin: 0;'>CrediPlus Seguridad</h2>
        <p style='color: #64748b; font-size: 14px; margin-top: 4px;'>Actualización de Biometría de Voz</p>
    </div>
    <div style='background-color: #f8fafc; border-radius: 8px; padding: 20px; text-align: center; border: 1px dashed #cbd5e1;'>
        <p style='color: #334155; font-size: 15px; margin: 0 0 12px 0;'>Has solicitado actualizar tu biometría de voz y palabra secreta. Usa el siguiente código para verificar tu identidad:</p>
        <div style='font-size: 32px; font-weight: 800; letter-spacing: 6px; color: #1e1b4b; background: #ede9fe; padding: 12px 24px; border-radius: 8px; display: inline-block;'>
            {otp}
        </div>
        <p style='color: #dc2626; font-size: 13px; font-weight: bold; margin-top: 14px; margin-bottom: 0;'>
            ⏳ Este código es válido exclusivamente durante 60 segundos.
        </p>
    </div>
    <p style='color: #94a3b8; font-size: 12px; margin-top: 20px; text-align: center;'>
        Si no realizaste esta solicitud, ignora este mensaje. Tu cuenta permanece protegida.
    </p>
</div>";

                await _emailService.EnviarCorreoAsync(
                    usuario.Correo,
                    "🔐 CrediPlus — Código de verificación para actualizar biometría de voz",
                    cuerpoHtml
                );

                return Json(new
                {
                    ok = true,
                    correo = correoEnmascarado,
                    segundosVigencia = 60,
                    mensaje = "Código de seguridad enviado a tu correo electrónico."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "No se pudo enviar el código de verificación: " + ex.Message
                });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult VerificarOtpRenovacionVoz(string? codigo)
        {
            try
            {
                string? dniLoginPendiente = HttpContext.Session.GetString("DniLoginPendiente");
                if (string.IsNullOrWhiteSpace(dniLoginPendiente))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La sesión de seguridad ha expirado. Inicia sesión nuevamente."
                    });
                }

                if (string.IsNullOrWhiteSpace(codigo))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Debes ingresar el código de seguridad de 6 dígitos."
                    });
                }

                string? otpGuardado = HttpContext.Session.GetString("RenovacionVoz_Otp");
                string? expiraStr = HttpContext.Session.GetString("RenovacionVoz_OtpExpira");

                if (string.IsNullOrWhiteSpace(otpGuardado) || string.IsNullOrWhiteSpace(expiraStr))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No hay un código activo. Solicita un nuevo código."
                    });
                }

                // Validar vigencia real en el servidor (60 segundos)
                if (DateTime.TryParse(expiraStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expira))
                {
                    if (DateTime.UtcNow > expira)
                    {
                        HttpContext.Session.Remove("RenovacionVoz_Otp");
                        HttpContext.Session.Remove("RenovacionVoz_OtpExpira");
                        return Json(new
                        {
                            ok = false,
                            expirado = true,
                            mensaje = "El código de seguridad ha expirado. Solicita un nuevo código."
                        });
                    }
                }

                // Control estricto de intentos fallidos (máximo 3 intentos)
                int intentos = HttpContext.Session.GetInt32("RenovacionVoz_OtpIntentos") ?? 0;
                if (intentos >= 3)
                {
                    HttpContext.Session.Remove("RenovacionVoz_Otp");
                    HttpContext.Session.Remove("RenovacionVoz_OtpExpira");
                    return Json(new
                    {
                        ok = false,
                        bloqueado = true,
                        mensaje = "Has superado el número máximo de intentos. Solicita un nuevo código."
                    });
                }

                // Comparación estricta del código
                if (codigo.Trim() != otpGuardado.Trim())
                {
                    intentos++;
                    HttpContext.Session.SetInt32("RenovacionVoz_OtpIntentos", intentos);
                    int restantes = Math.Max(0, 3 - intentos);
                    if (restantes == 0)
                    {
                        HttpContext.Session.Remove("RenovacionVoz_Otp");
                        HttpContext.Session.Remove("RenovacionVoz_OtpExpira");
                        return Json(new
                        {
                            ok = false,
                            bloqueado = true,
                            mensaje = "Código incorrecto. Has superado el límite de intentos. Solicita un nuevo código."
                        });
                    }

                    return Json(new
                    {
                        ok = false,
                        mensaje = $"Código incorrecto. Te quedan {restantes} intento(s)."
                    });
                }

                // Código correcto: invalidar el OTP utilizado
                HttpContext.Session.Remove("RenovacionVoz_Otp");
                HttpContext.Session.Remove("RenovacionVoz_OtpExpira");
                HttpContext.Session.Remove("RenovacionVoz_OtpIntentos");

                // Crear una autorización temporal exclusiva para renovación (un solo uso, 10 min de validez)
                string tokenAutorizado = Guid.NewGuid().ToString("N");
                HttpContext.Session.SetString("RenovacionVoz_TokenAutorizado", tokenAutorizado);
                HttpContext.Session.SetString("RenovacionVoz_TokenExpira", DateTime.UtcNow.AddMinutes(10).ToString("o"));

                return Json(new
                {
                    ok = true,
                    token = tokenAutorizado,
                    mensaje = "Identidad verificada exitosamente. Procede a registrar tu nueva voz."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "Error al verificar el código: " + ex.Message
                });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RenovarVozLogin(
            IFormFile? audio,
            string? fraseVoz,
            string? tokenRenovacion)
        {
            string? nuevaRutaAudio = null;
            string? rutaWavCompartida = null;
            try
            {
                string? dniLoginPendiente = HttpContext.Session.GetString("DniLoginPendiente");
                if (string.IsNullOrWhiteSpace(dniLoginPendiente))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La sesión de seguridad ha expirado. Inicia sesión nuevamente."
                    });
                }

                // ==========================================
                // CONSUMO ATÓMICO Y ESTRICTO DEL TOKEN (UN SOLO USO Y ANTI-CONCURRENCIA)
                // ==========================================
                bool tokenAutorizadoValido = false;
                lock (_lockRenovacionVoz)
                {
                    string? tokenEsperado = HttpContext.Session.GetString("RenovacionVoz_TokenAutorizado");
                    string? tokenExpiraStr = HttpContext.Session.GetString("RenovacionVoz_TokenExpira");

                    if (!string.IsNullOrWhiteSpace(tokenEsperado) &&
                        !string.IsNullOrWhiteSpace(tokenRenovacion) &&
                        tokenRenovacion == tokenEsperado)
                    {
                        if (DateTime.TryParse(tokenExpiraStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expiraToken) && DateTime.UtcNow <= expiraToken)
                        {
                            tokenAutorizadoValido = true;
                            // Invalidar inmediatamente de la sesión para impedir cualquier reintento o solicitud concurrente
                            HttpContext.Session.Remove("RenovacionVoz_TokenAutorizado");
                            HttpContext.Session.Remove("RenovacionVoz_TokenExpira");
                        }
                    }
                }

                if (!tokenAutorizadoValido)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No tienes una autorización válida para actualizar la biometría o el token ya ha sido utilizado. Verifica tu identidad nuevamente."
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
                    audio.ContentType.StartsWith(tipo, StringComparison.OrdinalIgnoreCase)
                );

                if (!audioValido)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El archivo enviado no es un formato de audio válido."
                    });
                }

                Usuario? usuario = await _Context.Usuario.FirstOrDefaultAsync(x => x.Dni == dniLoginPendiente);
                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró el usuario en el sistema."
                    });
                }

                // Convertir una sola vez a WAV 16kHz PCM para Vosk y ECAPA-TDNN compartidos
                rutaWavCompartida = await _ServicioEmbeddingVoz.ConvertirAudioAWavPcm16k(audio);

                // 1. RECONOCIMIENTO DE FRASE CON VOSK DIRECTAMENTE DESDE EL AUDIO
                string transcripcionVosk = TranscribirAudioConVosk(rutaWavCompartida);
                string fraseFinal = !string.IsNullOrWhiteSpace(transcripcionVosk)
                    ? transcripcionVosk.Trim()
                    : (fraseVoz ?? "").Trim();

                if (string.IsNullOrWhiteSpace(fraseFinal))
                {
                    fraseFinal = "Mi voz protege mi cuenta";
                }

                // 2. GENERAR EL NUEVO EMBEDDING BIOMÉTRICO CON ECAPA-TDNN DESDE EL MISMO WAV
                var candidatos = await _ServicioEmbeddingVoz.GenerarEmbeddingsCandidatos(rutaWavCompartida);
                if (candidatos == null || candidatos.Count == 0)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se pudo generar correctamente la identificación biométrica de la nueva voz."
                    });
                }

                float[] embedding = candidatos[0];
                if (embedding == null || embedding.Length != 192)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se pudo generar correctamente la identificación biométrica de la nueva voz."
                    });
                }

                // Validar que los valores sean finitos y tengan norma válida
                double normaCheck = 0;
                for (int i = 0; i < embedding.Length; i++)
                {
                    if (float.IsNaN(embedding[i]) || float.IsInfinity(embedding[i]))
                    {
                        return Json(new
                        {
                            ok = false,
                            mensaje = "La grabación produjo un vector biométrico inestable. Por favor, graba nuevamente."
                        });
                    }
                    normaCheck += embedding[i] * embedding[i];
                }
                if (normaCheck < 1e-6)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Nivel de audio insuficiente para generar la biometría. Habla claramente cerca del micrófono."
                    });
                }

                // 3. PREPARAR EL NUEVO ARCHIVO DE AUDIO EN WWWROOT
                string carpetaAudio = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "audiosRegistro");
                if (!Directory.Exists(carpetaAudio))
                {
                    Directory.CreateDirectory(carpetaAudio);
                }

                string nuevoNombreAudio = Guid.NewGuid().ToString() + ".webm";
                nuevaRutaAudio = Path.Combine(carpetaAudio, nuevoNombreAudio);

                byte[] bytesAudio;
                using (var ms = new MemoryStream())
                {
                    await audio.CopyToAsync(ms);
                    bytesAudio = ms.ToArray();
                }

                await System.IO.File.WriteAllBytesAsync(nuevaRutaAudio, bytesAudio);

                // Recordar la ruta del audio anterior para eliminarlo de forma segura tras guardar en MySQL
                string? rutaAudioAnterior = usuario.AudioRegistro;

                // 4. ACTUALIZAR EXCLUSIVAMENTE LOS CUATRO CAMPOS BIOMÉTRICOS EN MYSQL CON LA ESTRATEGIA DE EJECUCIÓN REINTENTABLE
                var strategy = _Context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _Context.Database.BeginTransactionAsync();

                    // Re-verificar si un intento previo ya persistió los datos (en caso de reintento tras pérdida de respuesta)
                    var usuarioEnDb = await _Context.Usuario.FirstOrDefaultAsync(x => x.Dni == dniLoginPendiente);
                    if (usuarioEnDb == null)
                    {
                        throw new InvalidOperationException("No se encontró el usuario en el sistema.");
                    }

                    string rutaRelativaNuevoAudio = "/audiosRegistro/" + nuevoNombreAudio;
                    if (usuarioEnDb.AudioRegistro == rutaRelativaNuevoAudio && usuarioEnDb.FraseVoz == fraseFinal)
                    {
                        // Ya se persistió en una ejecución previa de la estrategia
                        return;
                    }

                    usuarioEnDb.FraseVoz = fraseFinal;
                    usuarioEnDb.AudioRegistro = rutaRelativaNuevoAudio;

                    usuarioEnDb.EmbeddingVoz = new byte[embedding.Length * sizeof(float)];
                    Buffer.BlockCopy(embedding, 0, usuarioEnDb.EmbeddingVoz, 0, usuarioEnDb.EmbeddingVoz.Length);

                    // Como ya se validó el OTP de renovación antes de grabar, se habilita directamente la voz
                    usuarioEnDb.VozHabilitadaLogin = true;

                    // Configurar ChangeTracker de EF Core para actualizar las columnas modificadas
                    var entry = _Context.Entry(usuarioEnDb);
                    entry.Property(u => u.FraseVoz).IsModified = true;
                    entry.Property(u => u.EmbeddingVoz).IsModified = true;
                    entry.Property(u => u.AudioRegistro).IsModified = true;
                    entry.Property(u => u.VozHabilitadaLogin).IsModified = true;

                    int filasAfectadas = await _Context.SaveChangesAsync();
                    if (filasAfectadas == 0)
                    {
                        await transaction.RollbackAsync();
                        throw new InvalidOperationException("No se pudo actualizar la biometría en la base de datos.");
                    }

                    await transaction.CommitAsync();
                });

                // 5. COMPROBAR QUE LA NUEVA FRASE Y EMBEDDING SE RECUPEREN DESDE MYSQL MEDIANTE CONSULTA SIN SEGUIMIENTO
                var usuarioGuardado = await _Context.Usuario.AsNoTracking().FirstOrDefaultAsync(x => x.Dni == dniLoginPendiente);
                if (usuarioGuardado == null || usuarioGuardado.FraseVoz != fraseFinal || usuarioGuardado.EmbeddingVoz == null || usuarioGuardado.EmbeddingVoz.Length != 192 * sizeof(float))
                {
                    Console.WriteLine("[CrediPlus] Advertencia: La consulta sin seguimiento en MySQL no devolvió la biometría actualizada.");
                }

                // 6. ELIMINAR EL ARCHIVO BIOMÉTRICO ANTERIOR SOLO TRAS CONFIRMAR EL GUARDADO EN MYSQL
                if (!string.IsNullOrWhiteSpace(rutaAudioAnterior))
                {
                    try
                    {
                        string rutaFisicaAnterior = Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot",
                            rutaAudioAnterior.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                        );

                        if (System.IO.File.Exists(rutaFisicaAnterior) && !rutaFisicaAnterior.EndsWith(nuevoNombreAudio, StringComparison.OrdinalIgnoreCase))
                        {
                            System.IO.File.Delete(rutaFisicaAnterior);
                        }
                    }
                    catch { }
                }

                return Json(new
                {
                    ok = true,
                    mensaje = "Biometría y palabra secreta actualizadas correctamente.",
                    nuevaFrase = usuario.FraseVoz
                });
            }
            catch (Exception ex)
            {
                // Si ocurrió un fallo en MySQL o en el procesamiento, eliminar el nuevo archivo temporalmente creado
                if (!string.IsNullOrWhiteSpace(nuevaRutaAudio) && System.IO.File.Exists(nuevaRutaAudio))
                {
                    try { System.IO.File.Delete(nuevaRutaAudio); } catch { }
                }

                return Json(new
                {
                    ok = false,
                    mensaje = "Ocurrió un error al actualizar la biometría: " + ex.Message
                });
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(rutaWavCompartida) && System.IO.File.Exists(rutaWavCompartida))
                {
                    try { System.IO.File.Delete(rutaWavCompartida); } catch { }
                }
            }
        }

        private static string EnmascararCorreoSeguro(string? correo)
        {
            if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@'))
                return "***@***.com";

            var partes = correo.Split('@');
            var usuario = partes[0];
            var dominio = partes[1];

            if (usuario.Length <= 2)
            {
                return $"{usuario[0]}***@{dominio}";
            }

            return $"{usuario.Substring(0, 2)}***{usuario[^1]}@{dominio}";
        }

        [Authorize(Roles = "Cliente")]
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

        [Authorize(Roles = "Cliente")]
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
        [Authorize(Roles = "Cliente")]
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
                FechaSolicitud = DateTime.UtcNow,
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
                FechaRegistro = DateTime.UtcNow,
                SOLICITUD_CREDITO_Id_Solicitud = solicitud.Id_Solicitud
            };

            _Context.PERFIL_FINANCIERO.Add(perfil);

            var historial = new HistorialEstado
            {
                EstadoActual = "Pendiente",
                MotivoCambio = "pendiente en evaluación.",
                FechaCambio = DateTime.UtcNow,
                SOLICITUD_CREDITO_Id_Solicitud = solicitud.Id_Solicitud
            };

            _Context.HISTORIAL_ESTADO.Add(historial);

            _Context.SaveChanges();

            TempData["MostrarConfirmacion"] = "true";
            return RedirectToAction("SolicitarCredito");
        }

        [HttpGet]
        [Authorize(Roles = "Cliente")]
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
        [Authorize(Roles = "Cliente")]
        public IActionResult BorrarSolicitudRechazada(int idSolicitud)
        {
            string dni = User.FindFirst("Dni")?.Value;
            var usuario = _Context.Usuario.FirstOrDefault(x => x.Dni == dni);
            if (usuario == null)
            {
                return Json(new { mensaje = "No se encontró el usuario autenticado." });
            }

            var solicitud = _Context.SOLICITUD_CREDITO
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Usuario_Id_Usuario == usuario.Id && x.Estado == "Rechazado");

            if (solicitud == null)
            {
                return Json(new { mensaje = "No se encontró la solicitud rechazada o no tienes permiso para acceder a ella." });
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

            var otraSolicitud = _Context.SOLICITUD_CREDITO
                .FirstOrDefault(s => s.Usuario_Id_Usuario == solicitud.Usuario_Id_Usuario && s.Id_Solicitud != idSolicitud);

            var historialCredito = _Context.HISTORIAL_CREDITO
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .ToList();

            if (otraSolicitud != null)
            {
                // Preservar el historial crediticio del cliente asociándolo a su otra solicitud
                foreach (var h in historialCredito)
                {
                    h.SOLICITUD_CREDITO_Id_Solicitud = otraSolicitud.Id_Solicitud;
                }
            }
            else
            {
                // Si no tiene otras solicitudes, remover registros y sincronizar resumen para evitar contadores huérfanos
                _Context.HISTORIAL_CREDITO.RemoveRange(historialCredito);

                var resumenUsuario = _Context.RESUMEN_CREDITICIO
                    .FirstOrDefault(x => x.Usuario_Id == solicitud.Usuario_Id_Usuario);
                if (resumenUsuario != null)
                {
                    resumenUsuario.DeudasActivas = 0;
                    resumenUsuario.DeudasPagadas = 0;
                    resumenUsuario.DeudasVencidas = 0;
                    resumenUsuario.UltimaActualizacion = DateTime.Now;
                }
            }

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
        [Authorize(Roles = "Cliente")]
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
                        PeruTimeHelper.TodayPeru > c.FechaLimitePago.Date)
                    {
                        diasAtraso = (PeruTimeHelper.TodayPeru - c.FechaLimitePago.Date).Days;
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
        [Authorize(Roles = "Cliente")]
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
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> PerfilPersonal(Usuario usuarioEditado, string codigoCorreo)
        {
            string dniActual = User.FindFirst("Dni")?.Value;

            var usuario =
    _Context.Usuario.FirstOrDefault(
        x => x.Dni == dniActual
    );

            if (usuario == null)
            {
                return RedirectToAction(
                    "IniciarSesion",
                    "Login"
                );
            }

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

            if (!EsCorreoValidoDominio(usuarioEditado.Correo))
            {
                ViewData["Mensaje"] = "El correo debe ser Gmail, Hotmail o Outlook.";
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
                var resClave = PasswordPolicyHelper.Validar(usuarioEditado.clave);
                if (!resClave.EsValida)
                {
                    ViewData["Mensaje"] = resClave.MensajeError;
                    ViewData["ModoEditar"] = true;
                    return View(usuario);
                }

                usuario.clave = utilidades.EncriptarClave(usuarioEditado.clave);
            }

            // ==========================================
            // GUARDAR CAMBIOS EN BASE DE DATOS
            // ==========================================

            await _Context.SaveChangesAsync();


            // ==========================================
            // ACTUALIZAR DATOS DE LA SESIÓN / COOKIE
            // ==========================================

            List<Claim> claims = new List<Claim>()
{
    new Claim(
        ClaimTypes.Name,
        usuario.Nombre ?? ""
    ),

    new Claim(
        "Apellido",
        usuario.Apellido ?? ""
    ),

    new Claim(
        "Dni",
        usuario.Dni ?? ""
    ),

    new Claim(
        "Celular",
        usuario.Celular ?? ""
    ),

    new Claim(
        "Correo",
        usuario.Correo ?? ""
    ),

    new Claim(
        ClaimTypes.Role,
        usuario.Rol ?? ""
    )
};


            // ==========================================
            // CREAR NUEVA IDENTIDAD
            // ==========================================

            ClaimsIdentity claimsIdentity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme
                );


            // ==========================================
            // REEMPLAZAR COOKIE ANTIGUA
            // ==========================================

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
            // MENSAJE DE ÉXITO
            // ==========================================

            TempData["MensajeOk"] =
                "Perfil actualizado correctamente.";

            return RedirectToAction(
                "PerfilPersonal",
                "Login"
            );
        }
        [HttpPost]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> EnviarCodigoPerfil(string correo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(correo) || !EsCorreoValidoDominio(correo))
                {
                    return Json(new { ok = false, mensaje = "Ingrese un correo válido (Gmail, Hotmail o Outlook)." });
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
        [HttpGet]
        [Authorize(Roles = "Cliente")]
        public IActionResult Configuracion()
        {
            return View();
        }
        [HttpGet]
        [Authorize(Roles = "Cliente")]
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
                    PeruTimeHelper.TodayPeru > cuota.FechaLimitePago.Date)
                {
                    int diasAtraso = (PeruTimeHelper.TodayPeru - cuota.FechaLimitePago.Date).Days;

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
        [HttpGet]
        [Authorize(Roles = "Cliente")]
        public IActionResult SimuladorCredito()
        {
            return View();
        }
        [HttpPost]
        [Authorize(Roles = "Cliente")]
        public IActionResult RegistrarCancelacion(int idSolicitud, decimal montoDevuelto, string metodoPago, string codigoOperacion, string motivoCancelacion)
        {
            try
            {
                // ==========================================
                // IDOR GUARD: verificar propiedad del recurso
                // ==========================================
                string dni = User.FindFirst("Dni")?.Value;
                var usuarioAutenticado = _Context.Usuario.FirstOrDefault(u => u.Dni == dni);
                if (usuarioAutenticado == null)
                    return Json(new { ok = false, mensaje = "Sesión inválida." });

                var solicitud = _Context.SOLICITUD_CREDITO
                    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Usuario_Id_Usuario == usuarioAutenticado.Id);

                if (solicitud == null)
                    return Json(new { ok = false, mensaje = "No se encontró la solicitud." });

                var pago = new PagoCancelacion
                {
                    MontoDevuelto = montoDevuelto,
                    MetodoPago = metodoPago,
                    CodigoOperacion = codigoOperacion,
                    MotivoCancelacion = motivoCancelacion,
                    FechaPago = DateTime.UtcNow,
                    Estado = "Pendiente",
                    SOLICITUD_CREDITO_Id_Solicitud = idSolicitud
                };

                _Context.PAGO_CANCELACION.Add(pago);
                solicitud.Estado = "Pendiente cancelación";
                _Context.SaveChanges();

                return Json(new { ok = true, mensaje = "Pago registrado correctamente. La cancelación queda pendiente de aprobación." });
            }
            catch
            {
                return Json(new { ok = false, mensaje = "No se pudo registrar la cancelación." });
            }
        }
        [HttpPost]
        [Authorize(Roles = "Cliente")]
        public IActionResult EditarSolicitud(int idSolicitud, decimal nuevoMonto, int nuevoPlazo)
        {
            try
            {
                // ==========================================
                // IDOR GUARD: verificar propiedad del recurso
                // ==========================================
                string dni = User.FindFirst("Dni")?.Value;
                var usuarioAutenticado = _Context.Usuario.FirstOrDefault(u => u.Dni == dni);
                if (usuarioAutenticado == null)
                    return Json(new { ok = false, mensaje = "Sesión inválida." });

                var solicitud = _Context.SOLICITUD_CREDITO
                    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Usuario_Id_Usuario == usuarioAutenticado.Id);

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
        [Authorize(Roles = "Cliente")]
        public IActionResult RegistrarPagoCuota(
    int idCuota,
    decimal montoPagado,
    string metodoPago,
    string entidadPago,
    string codigoOperacion)
        {
            try
            {
                // ==========================================
                // IDOR GUARD: verificar propiedad del recurso
                // La cuota debe pertenecer a una solicitud del usuario autenticado.
                // ==========================================
                string dni = User.FindFirst("Dni")?.Value;
                var usuarioAutenticado = _Context.Usuario.FirstOrDefault(u => u.Dni == dni);
                if (usuarioAutenticado == null)
                    return Json(new { ok = false, mensaje = "Sesión inválida." });

                var cuota = _Context.CUOTA
                    .Include(c => c.SOLICITUD_CREDITO)
                    .FirstOrDefault(x => x.Id_Cuota == idCuota && x.SOLICITUD_CREDITO.Usuario_Id_Usuario == usuarioAutenticado.Id);

                if (cuota == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró la cuota especificada."
                    });
                }

                if (cuota.Estado == "Pagado" || cuota.Estado == "Pendiente aprobación")
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La cuota ya se encuentra pagada o en proceso de validación."
                    });
                }

                var pago = new PagoCuota
                {
                    Id_Cuota = idCuota,
                    MontoPagado = montoPagado,
                    MetodoPago = metodoPago,
                    EntidadPago = entidadPago,
                    CodigoOperacion = codigoOperacion,
                    FechaPago = DateTime.UtcNow,
                    Estado = "Pendiente validación"
                };

                _Context.PAGO_CUOTA.Add(pago);
                cuota.Estado = "Pendiente aprobación";
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
        [HttpGet]
        [Authorize(Roles = "Cliente")]
        public IActionResult PreguntasFrecuentes()
        {
            return View();
        }
        [HttpGet]
        [Authorize(Roles = "Cliente")]
        public JsonResult ObtenerMensajes(int idSolicitud)
        {
            // ==========================================
            // IDOR GUARD: verificar propiedad del recurso
            // ==========================================
            string dni = User.FindFirst("Dni")?.Value;
            var usuarioAutenticado = _Context.Usuario.FirstOrDefault(u => u.Dni == dni);
            if (usuarioAutenticado == null)
                return Json(new { ok = false, mensaje = "Sesión inválida." });

            var solicitud = _Context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Usuario_Id_Usuario == usuarioAutenticado.Id);

            if (solicitud == null)
                return Json(new List<object>());

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
                        : "Analista de Crédito"
                })
                .ToList();

            return Json(mensajes);
        }
        [HttpPost]
        [Authorize(Roles = "Cliente")]
        public async Task<JsonResult> EnviarMensaje(
     int idSolicitud,
     string? mensaje,
     IFormFile? imagen)
        {
            // ==========================================
            // IDOR GUARD: verificar propiedad del recurso
            // ==========================================
            string dni = User.FindFirst("Dni")?.Value;
            var usuarioAutenticado = _Context.Usuario.FirstOrDefault(u => u.Dni == dni);
            if (usuarioAutenticado == null)
                return Json(new { ok = false, mensaje = "Sesión inválida." });

            bool solicitudPropia = _Context.SOLICITUD_CREDITO
                .Any(x => x.Id_Solicitud == idSolicitud && x.Usuario_Id_Usuario == usuarioAutenticado.Id);

            if (!solicitudPropia)
                return Json(new { ok = false, mensaje = "No autorizado." });

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

                using (var stream = new System.IO.FileStream(rutaCompleta, FileMode.Create))
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

            // El remitente siempre lo determina el servidor según el rol autenticado,
            // nunca se acepta desde el cliente.
            nuevo.Remitente = "Usuario";
            nuevo.FechaEnvio = DateTime.Now;
            nuevo.Leido = false;

            _Context.MENSAJE.Add(nuevo);
            _Context.SaveChanges();

            return Json(new { ok = true });
        }
        [HttpGet]
        [Authorize(Roles = "Cliente")]
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
        [Authorize(Roles = "Cliente")]
        public JsonResult CancelarEvaluacion(int idSolicitud, string motivo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(motivo))
                {
                    return Json(new { ok = false, mensaje = "Debe ingresar el motivo de cancelación." });
                }

                // ==========================================
                // IDOR GUARD: verificar propiedad del recurso
                // ==========================================
                string dni = User.FindFirst("Dni")?.Value;
                var usuarioAutenticado = _Context.Usuario.FirstOrDefault(u => u.Dni == dni);
                if (usuarioAutenticado == null)
                    return Json(new { ok = false, mensaje = "Sesión inválida." });

                var solicitud = _Context.SOLICITUD_CREDITO
                    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud
                        && x.Estado == "En Evaluación"
                        && x.Usuario_Id_Usuario == usuarioAutenticado.Id);

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
                    FechaCancelacion = DateTime.UtcNow,
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
        [Authorize(Roles = "Cliente")]
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
        [Authorize(Roles = "Cliente")]
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
        private static double CalcularSimilitudCoseno(
    float[] embeddingRegistrado,
    float[] embeddingActual)
        {
            if (embeddingRegistrado == null ||
                embeddingActual == null ||
                embeddingRegistrado.Length == 0 ||
                embeddingActual.Length == 0 ||
                embeddingRegistrado.Length != embeddingActual.Length)
            {
                return 0;
            }

            double productoPunto = 0;
            double normaRegistrada = 0;
            double normaActual = 0;

            for (int i = 0; i < embeddingRegistrado.Length; i++)
            {
                productoPunto +=
                    embeddingRegistrado[i] *
                    embeddingActual[i];

                normaRegistrada +=
                    embeddingRegistrado[i] *
                    embeddingRegistrado[i];

                normaActual +=
                    embeddingActual[i] *
                    embeddingActual[i];
            }

            if (normaRegistrada == 0 ||
                normaActual == 0)
            {
                return 0;
            }

            return productoPunto /
                (
                    Math.Sqrt(normaRegistrada) *
                    Math.Sqrt(normaActual)
                );
        }
        // ==========================================
        // CÓDIGO SEGURO DE RECUPERACIÓN
        // ==========================================

        private string GenerarCodigoRecuperacion(
            Usuario usuario,
            long bloqueTiempo)
        {
            string secreto =
                _configuration["RecoveryCodeSecret"];

            if (string.IsNullOrWhiteSpace(secreto))
            {
                throw new InvalidOperationException(
                    "RecoveryCodeSecret no está configurado."
                );
            }

            string correoNormalizado =
                usuario.Correo
                    .Trim()
                    .ToLowerInvariant();

            // Al incluir la contraseña actual,
            // el código deja de ser válido después
            // de cambiar la contraseña.
            string datos =
                usuario.Dni +
                "|" +
                correoNormalizado +
                "|" +
                usuario.clave +
                "|" +
                bloqueTiempo;

            using HMACSHA256 hmac =
                new HMACSHA256(
                    Encoding.UTF8.GetBytes(secreto)
                );

            byte[] hash =
                hmac.ComputeHash(
                    Encoding.UTF8.GetBytes(datos)
                );

            const string caracteres =
                "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

            char[] codigo =
                new char[6];

            for (int i = 0; i < 6; i++)
            {
                codigo[i] =
                    caracteres[
                        hash[i] %
                        caracteres.Length
                    ];
            }

            return new string(codigo);
        }


        // ==========================================
        // BLOQUE TEMPORAL DEL CÓDIGO
        // ==========================================

        private static long ObtenerBloqueRecuperacion()
        {
            // Bloques de 5 minutos.
            // Se acepta el actual y el anterior.
            // Así funciona aproximadamente
            // durante 5 a 10 minutos.

            long ahora =
                DateTimeOffset.UtcNow
                    .ToUnixTimeSeconds();

            return ahora / 300;
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> VincularDispositivoAuthenticator(
    string fcmToken,
    string? nombreDispositivo)
        {
            try
            {
                string? dni =
                    HttpContext.Session.GetString(
                        "DniLoginPendiente"
                    );

                if (string.IsNullOrWhiteSpace(dni))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "La sesión ha expirado. Inicia sesión nuevamente."
                    });
                }

                bool identidadVerificada =
                    HttpContext.Session.GetString(
                        "AuthenticatorIdentidadVerificada"
                    ) == "true";

                if (!identidadVerificada)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "Primero debes verificar tu identidad por correo."
                    });
                }

                if (string.IsNullOrWhiteSpace(fcmToken))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se recibió el token del dispositivo."
                    });
                }

                Usuario? usuario =
                    await _Context.Usuario
                        .FirstOrDefaultAsync(
                            x => x.Dni == dni
                        );

                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se encontró el usuario."
                    });
                }

                AutenticadorDispositivo? dispositivo =
     await _Context
         .AUTENTICADOR_DISPOSITIVO
         .FirstOrDefaultAsync(
             x => x.UsuarioId == usuario.Id &&
                  x.Activo
         );

                string tokenNuevo = fcmToken.Trim();

                if (dispositivo != null)
                {
                    // Es el mismo celular ya registrado.
                    if (dispositivo.FcmToken == tokenNuevo)
                    {
                        return Json(new
                        {
                            ok = true,
                            mensaje =
                                "Este dispositivo ya está vinculado con la cuenta."
                        });
                    }

                    // Es OTRO celular.
                    // No permitimos reemplazar el dispositivo existente.
                    return Json(new
                    {
                        ok = false,
                        codigo = "DISPOSITIVO_YA_VINCULADO",
                        mensaje =
                            "Esta cuenta ya está vinculada a otro dispositivo."
                    });
                }

                dispositivo = new AutenticadorDispositivo
                {
                    UsuarioId = usuario.Id,
                    FcmToken = tokenNuevo,

                    NombreDispositivo =
                        string.IsNullOrWhiteSpace(nombreDispositivo)
                            ? "Android"
                            : nombreDispositivo.Trim(),

                    FechaVinculacion = DateTime.Now,
                    Activo = true
                };

                _Context
                    .AUTENTICADOR_DISPOSITIVO
                    .Add(dispositivo);

                await _Context.SaveChangesAsync();

                return Json(new
                {
                    ok = true,
                    mensaje =
                        "Dispositivo vinculado correctamente."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR VINCULANDO AUTHENTICATOR: " + ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo vincular el dispositivo."
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CrearSolicitudAuthenticator()
        {
            try
            {
                // ==========================================
                // OBTENER USUARIO QUE YA VALIDÓ DNI + CLAVE
                // ==========================================

                string? dniLoginPendiente =
                    HttpContext.Session.GetString(
                        "DniLoginPendiente"
                    );

                if (string.IsNullOrWhiteSpace(dniLoginPendiente))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "La sesión de inicio ha expirado. Inicia sesión nuevamente."
                    });
                }

                // ==========================================
                // BUSCAR USUARIO
                // ==========================================

                Usuario? usuario =
                    await _Context.Usuario
                        .FirstOrDefaultAsync(
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
                // BUSCAR DISPOSITIVO VINCULADO
                // ==========================================

                AutenticadorDispositivo? dispositivo =
                    await _Context.AUTENTICADOR_DISPOSITIVO
                        .FirstOrDefaultAsync(
                            x =>
                                x.UsuarioId == usuario.Id &&
                                x.Activo
                        );

                // ==========================================
                // TODAVÍA NO TIENE AUTHENTICATOR CONFIGURADO
                // ==========================================

                if (dispositivo == null)
                {
                    return Json(new
                    {
                        ok = true,
                        configurado = false,

                        correoOculto =
                            OcultarCorreo(usuario.Correo),

                        mensaje =
                            "Debes configurar CrediPlus Authenticator."
                    });
                }

                // ==========================================
                // GENERAR NÚMERO DE COINCIDENCIA
                // 10 - 99
                // ==========================================

                int numero =
                    RandomNumberGenerator.GetInt32(
                        10,
                        100
                    );

                // ==========================================
                // CREAR SOLICITUD
                // ==========================================

                AutenticadorSolicitud solicitud =
                    new AutenticadorSolicitud
                    {
                        UsuarioId = usuario.Id,

                        NumeroVerificacion =
                            numero.ToString(),

                        Estado = "Pendiente",

                        FechaCreacion = DateTime.Now,

                        FechaExpiracion =
    DateTime.Now.AddSeconds(30)
                    };

                _Context.AUTENTICADOR_SOLICITUD.Add(
                    solicitud
                );

                await _Context.SaveChangesAsync();


                // ==========================================
                // OBTENER URL DEL SERVIDOR
                // ==========================================

                string protocolo =
                    Request.Headers["X-Forwarded-Proto"]
                        .FirstOrDefault()
                    ?? Request.Scheme;

                string host =
                    Request.Headers["X-Forwarded-Host"]
                        .FirstOrDefault()
                    ?? Request.Host.Value;

                string baseUrl =
                    $"{protocolo}://{host}";

                /*
                 * DESARROLLO LOCAL
                 * El celular NO puede usar localhost de la PC.
                 */
                if (
                    host.StartsWith(
                        "localhost",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    host.StartsWith("127.0.0.1")
                )
                {
                    baseUrl = "http://192.168.18.13:5280";
                }

                Console.WriteLine(
                    "BASE URL ENVIADA AL AUTHENTICATOR: "
                    + baseUrl
                );


                // ==========================================
                // ENVIAR NOTIFICACIÓN FIREBASE
                // ==========================================

                var mensajeFirebase =
                    new FirebaseAdmin.Messaging.Message
                    {
                        Token = dispositivo.FcmToken,

                        Data =
                            new Dictionary<string, string>
                            {
                {
                    "tipo",
                    "solicitud_login"
                },

                {
                    "solicitudId",
                    solicitud.Id.ToString()
                },

                {
                    "numero",
                    numero.ToString()
                },

                {
                    "baseUrl",
                    baseUrl
                }
                            },

                        Android =
                            new AndroidConfig
                            {
                                Priority =
                                    Priority.High
                            }
                    };


                string resultadoFirebase =
                    await FirebaseMessaging
                        .DefaultInstance
                        .SendAsync(
                            mensajeFirebase
                        );


                Console.WriteLine(
                    "FCM ENVIADO: " +
                    resultadoFirebase
                );

                // ==========================================
                // RESPUESTA PARA LA WEB
                // ==========================================

                return Json(new
                {
                    ok = true,
                    configurado = true,

                    solicitudId = solicitud.Id,

                    numero = numero,

                    mensaje =
                        "Solicitud creada correctamente."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR AUTHENTICATOR: " + ex
                );

                return Json(new
                {
                    ok = false,

                    mensaje =
                        "No se pudo crear la solicitud de autenticación."
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> EnviarCodigoVinculacionAuthenticator()
        {
            try
            {
                string? dni =
                    HttpContext.Session.GetString("DniLoginPendiente");

                if (string.IsNullOrWhiteSpace(dni))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "La sesión ha expirado. Inicia sesión nuevamente."
                    });
                }

                Usuario? usuario =
                    await _Context.Usuario
                        .FirstOrDefaultAsync(x => x.Dni == dni);

                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró el usuario."
                    });
                }

                // Código seguro de 6 caracteres
                const string caracteres =
                    "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

                char[] codigoArray = new char[6];

                for (int i = 0; i < codigoArray.Length; i++)
                {
                    int posicion =
                        RandomNumberGenerator.GetInt32(
                            caracteres.Length
                        );

                    codigoArray[i] =
                        caracteres[posicion];
                }

                string codigo =
                    new string(codigoArray);

                // Expira en 1 minuto
                long expiracion =
                    DateTimeOffset.UtcNow
                        .AddMinutes(1)
                        .ToUnixTimeSeconds();

                // Guardar temporalmente en la sesión asociado a la identidad del usuario
                HttpContext.Session.SetString(
                    "CodigoAuthenticator",
                    codigo
                );

                HttpContext.Session.SetString(
                    "CodigoAuthenticatorExpira",
                    expiracion.ToString()
                );

                HttpContext.Session.SetString(
                    $"CodigoAuthenticator_{dni}",
                    codigo
                );

                HttpContext.Session.SetString(
                    $"CodigoAuthenticatorExpira_{dni}",
                    expiracion.ToString()
                );

                HttpContext.Session.SetString(
                    "AuthenticatorIdentidadVerificada",
                    "false"
                );

                // Enviar usando tu servicio actual
                await _emailService.EnviarCodigoAsync(
                    usuario.Correo,
                    codigo
                );

                return Json(new
                {
                    ok = true,
                    correoOculto = OcultarCorreo(usuario.Correo),
                    segundos = 60,
                    mensaje = "Código enviado correctamente."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[CrediPlus] Error al enviar código authenticator: {ex.GetType().Name} - {ex.Message}"
                );

                return Json(new
                {
                    ok = false,
                    mensaje = "No se pudo enviar el código de seguridad."
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public IActionResult VerificarCodigoVinculacionAuthenticator(
            [FromForm] string? codigo
        )
        {
            try
            {
                string? dni =
                    HttpContext.Session.GetString("DniLoginPendiente");

                if (string.IsNullOrWhiteSpace(dni))
                {
                    return Json(new
                    {
                        ok = false,
                        expirado = true,
                        mensaje = "La sesión ha expirado. Inicia sesión nuevamente."
                    });
                }

                string? codigoRecibido = codigo;

                if (string.IsNullOrWhiteSpace(codigoRecibido) &&
                    Request.HasFormContentType &&
                    Request.Form.ContainsKey("codigo"))
                {
                    codigoRecibido = Request.Form["codigo"].ToString();
                }

                if (string.IsNullOrWhiteSpace(codigoRecibido) &&
                    Request.Query.ContainsKey("codigo"))
                {
                    codigoRecibido = Request.Query["codigo"].ToString();
                }

                if (string.IsNullOrWhiteSpace(codigoRecibido))
                {
                    return Json(new
                    {
                        ok = false,
                        expirado = false,
                        mensaje = "Ingresa el código de seguridad."
                    });
                }

                string? codigoGuardado =
                    HttpContext.Session.GetString($"CodigoAuthenticator_{dni}") ??
                    HttpContext.Session.GetString("CodigoAuthenticator");

                string? expiracionTexto =
                    HttpContext.Session.GetString($"CodigoAuthenticatorExpira_{dni}") ??
                    HttpContext.Session.GetString("CodigoAuthenticatorExpira");

                if (
                    string.IsNullOrWhiteSpace(codigoGuardado) ||
                    string.IsNullOrWhiteSpace(expiracionTexto)
                )
                {
                    return Json(new
                    {
                        ok = false,
                        expirado = true,
                        mensaje = "No existe un código activo. Solicita un nuevo código."
                    });
                }

                if (!long.TryParse(
                        expiracionTexto,
                        out long expiracion))
                {
                    LimpiarCodigoAuthenticator(dni);

                    return Json(new
                    {
                        ok = false,
                        expirado = true,
                        mensaje = "El código ya no es válido. Solicita un nuevo código."
                    });
                }

                long ahora =
                    DateTimeOffset.UtcNow
                        .ToUnixTimeSeconds();

                if (ahora >= expiracion)
                {
                    LimpiarCodigoAuthenticator(dni);

                    return Json(new
                    {
                        ok = false,
                        expirado = true,
                        mensaje = "El código ha expirado. Solicita un nuevo código."
                    });
                }

                codigoRecibido =
                    codigoRecibido.Trim().ToUpperInvariant();

                string codigoEsperado =
                    codigoGuardado.Trim().ToUpperInvariant();

                if (!string.Equals(
                        codigoRecibido,
                        codigoEsperado,
                        StringComparison.Ordinal))
                {
                    return Json(new
                    {
                        ok = false,
                        expirado = false,
                        mensaje = "El código ingresado es incorrecto."
                    });
                }

                // Identidad confirmada correctamente
                HttpContext.Session.SetString(
                    "AuthenticatorIdentidadVerificada",
                    "true"
                );

                // El código ya fue usado: destruirlo para evitar reutilización
                LimpiarCodigoAuthenticator(dni);

                return Json(new
                {
                    ok = true,
                    mensaje = "Identidad confirmada correctamente."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[CrediPlus] Error al verificar código authenticator: {ex.GetType().Name} - {ex.Message}"
                );

                return Json(new
                {
                    ok = false,
                    mensaje = "Ocurrió un error al verificar el código. Inténtalo nuevamente."
                });
            }
        }

        private void LimpiarCodigoAuthenticator(string? dni)
        {
            try
            {
                HttpContext.Session.Remove("CodigoAuthenticator");
                HttpContext.Session.Remove("CodigoAuthenticatorExpira");

                if (!string.IsNullOrWhiteSpace(dni))
                {
                    HttpContext.Session.Remove($"CodigoAuthenticator_{dni}");
                    HttpContext.Session.Remove($"CodigoAuthenticatorExpira_{dni}");
                }
            }
            catch
            {
            }
        }
        private string OcultarCorreo(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
            {
                return "";
            }

            string[] partes = correo.Split('@');

            if (partes.Length != 2)
            {
                return correo;
            }

            string usuario = partes[0];
            string dominio = partes[1];

            string visible;

            if (usuario.Length <= 3)
            {
                visible = usuario.Substring(0, 1);
            }
            else
            {
                visible = usuario.Substring(0, 3);
            }

            return visible + "*****@" + dominio;
        }
        [AllowAnonymous]
        [HttpGet]
        public IActionResult DescargarAuthenticator()
        {
            var rutaApk = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "descargas",
                "CrediPlusAuthenticator.apk"
            );

            if (!System.IO.File.Exists(rutaApk))
            {
                return NotFound();
            }

            return PhysicalFile(
                rutaApk,
                "application/vnd.android.package-archive",
                "CrediPlusAuthenticator.apk"
            );
        }
        // =====================================================
        // PREPARAR CONFIGURACIÓN TOTP
        // =====================================================

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PrepararConfiguracionTotp()
        {
            try
            {
                string? dni =
                    HttpContext.Session.GetString(
                        "DniLoginPendiente"
                    );

                if (string.IsNullOrWhiteSpace(dni))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "La sesión ha expirado. Inicia sesión nuevamente."
                    });
                }


                bool identidadVerificada =
                    HttpContext.Session.GetString(
                        "AuthenticatorIdentidadVerificada"
                    ) == "true";


                if (!identidadVerificada)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "Primero debes verificar tu identidad por correo."
                    });
                }


                Usuario? usuario =
                    await _Context.Usuario
                        .FirstOrDefaultAsync(
                            x => x.Dni == dni
                        );


                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se encontró el usuario."
                    });
                }


                // =====================================================
                // PREPARAR CLAVE TOTP
                // =====================================================

                string? claveTotp = usuario.TotpSecret;

                if (string.IsNullOrWhiteSpace(claveTotp))
                {
                    byte[] bytesSecretos =
                        RandomNumberGenerator.GetBytes(20);

                    claveTotp =
                        ConvertirBase32(bytesSecretos);

                    // IMPORTANTE:
                    // Guardamos la clave en MySQL para que el APK,
                    // que hace una petición independiente,
                    // pueda validarla sin depender de la Session
                    // del navegador.
                    usuario.TotpSecret = claveTotp;
                    usuario.TotpHabilitado = false;

                    _Context.Usuario.Update(usuario);
                    await _Context.SaveChangesAsync();
                }

                // La mantenemos también en Session porque tu flujo web
                // actual todavía la utiliza.
                HttpContext.Session.SetString(
                    "TotpSecretPendiente",
                    claveTotp
                );


                return Json(new
                {
                    ok = true,
                    correo = usuario.Correo,
                    clave = FormatearClaveTotp(claveTotp)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR PREPARANDO TOTP: " + ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo preparar la configuración del autenticador."
                });
            }
        }
        // =====================================================
        // CONFIRMAR CONFIGURACIÓN TOTP
        // =====================================================

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmarConfiguracionTotp(
            string codigo)
        {
            try
            {
                string? dni =
                    HttpContext.Session.GetString(
                        "DniLoginPendiente"
                    );

                if (string.IsNullOrWhiteSpace(dni))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "La sesión ha expirado. Inicia sesión nuevamente."
                    });
                }


                bool identidadVerificada =
                    HttpContext.Session.GetString(
                        "AuthenticatorIdentidadVerificada"
                    ) == "true";


                if (!identidadVerificada)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "Primero debes verificar tu identidad por correo."
                    });
                }


                if (
                    string.IsNullOrWhiteSpace(codigo) ||
                    !Regex.IsMatch(codigo, @"^\d{6}$")
                )
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "El código debe tener exactamente 6 números."
                    });
                }


                string? claveTotp =
                    HttpContext.Session.GetString(
                        "TotpSecretPendiente"
                    );


                if (string.IsNullOrWhiteSpace(claveTotp))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "La configuración TOTP ha expirado. Genera una nueva."
                    });
                }


                bool codigoValido =
                    ValidarCodigoTotp(
                        claveTotp,
                        codigo
                    );


                if (!codigoValido)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "El código no es válido. Revisa el código generado por CrediPlus Authenticator."
                    });
                }


                Usuario? usuario =
                    await _Context.Usuario
                        .FirstOrDefaultAsync(
                            x => x.Dni == dni
                        );


                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se encontró el usuario."
                    });
                }


                // ==========================================
                // GUARDAR DEFINITIVAMENTE EN MYSQL
                // ==========================================

                usuario.TotpSecret =
                    claveTotp;

                usuario.TotpHabilitado =
                    true;


                _Context.Usuario.Update(
                    usuario
                );

                await _Context.SaveChangesAsync();


                // Destruir clave temporal
                HttpContext.Session.Remove(
                    "TotpSecretPendiente"
                );


                return Json(new
                {
                    ok = true,
                    mensaje =
                        "CrediPlus Authenticator fue vinculado correctamente."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR CONFIRMANDO TOTP: " + ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo confirmar la configuración TOTP."
                });
            }
        }
        // =====================================================
        // GENERAR / VALIDAR TOTP
        // =====================================================

        private static bool ValidarCodigoTotp(
            string secretoBase32,
            string codigoIngresado)
        {
            byte[] secreto =
                DecodificarBase32(
                    secretoBase32
                );


            long tiempoActual =
                DateTimeOffset.UtcNow
                    .ToUnixTimeSeconds()
                / 30;


            // Aceptar:
            // período anterior,
            // período actual,
            // período siguiente.
            // Esto tolera pequeñas diferencias de reloj.

            for (long diferencia = -1;
                 diferencia <= 1;
                 diferencia++)
            {
                string codigoEsperado =
                    GenerarCodigoTotp(
                        secreto,
                        tiempoActual + diferencia
                    );


                if (
                    string.Equals(
                        codigoEsperado,
                        codigoIngresado,
                        StringComparison.Ordinal
                    )
                )
                {
                    return true;
                }
            }


            return false;
        }


        private static string GenerarCodigoTotp(
            byte[] secreto,
            long contador)
        {
            byte[] contadorBytes =
                BitConverter.GetBytes(
                    contador
                );


            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(
                    contadorBytes
                );
            }


            using HMACSHA1 hmac =
                new HMACSHA1(
                    secreto
                );


            byte[] hash =
                hmac.ComputeHash(
                    contadorBytes
                );


            int offset =
                hash[
                    hash.Length - 1
                ] & 0x0F;


            int binario =
                (
                    (hash[offset] & 0x7F) << 24
                )
                |
                (
                    (hash[offset + 1] & 0xFF) << 16
                )
                |
                (
                    (hash[offset + 2] & 0xFF) << 8
                )
                |
                (
                    hash[offset + 3] & 0xFF
                );


            int codigo =
                binario % 1_000_000;


            return codigo.ToString(
                "D6"
            );
        }
        private static string ConvertirBase32(
    byte[] datos)
        {
            const string alfabeto =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";


            StringBuilder resultado =
                new StringBuilder();


            int buffer = 0;
            int bitsEnBuffer = 0;


            foreach (byte dato in datos)
            {
                buffer =
                    (buffer << 8) | dato;

                bitsEnBuffer += 8;


                while (bitsEnBuffer >= 5)
                {
                    bitsEnBuffer -= 5;

                    int indice =
                        (buffer >> bitsEnBuffer)
                        & 31;

                    resultado.Append(
                        alfabeto[indice]
                    );
                }
            }


            if (bitsEnBuffer > 0)
            {
                int indice =
                    (buffer << (5 - bitsEnBuffer))
                    & 31;

                resultado.Append(
                    alfabeto[indice]
                );
            }


            return resultado.ToString();
        }


        private static byte[] DecodificarBase32(
            string texto)
        {
            const string alfabeto =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";


            texto =
                texto
                    .Replace(" ", "")
                    .Trim()
                    .ToUpperInvariant();


            List<byte> bytes =
                new List<byte>();


            int buffer = 0;
            int bitsEnBuffer = 0;


            foreach (char caracter in texto)
            {
                int valor =
                    alfabeto.IndexOf(
                        caracter
                    );


                if (valor < 0)
                {
                    continue;
                }


                buffer =
                    (buffer << 5) | valor;

                bitsEnBuffer += 5;


                if (bitsEnBuffer >= 8)
                {
                    bitsEnBuffer -= 8;

                    bytes.Add(
                        (byte)(
                            (buffer >> bitsEnBuffer)
                            & 255
                        )
                    );
                }
            }


            return bytes.ToArray();
        }


        private static string FormatearClaveTotp(
            string clave)
        {
            StringBuilder resultado =
                new StringBuilder();


            for (int i = 0;
                 i < clave.Length;
                 i++)
            {
                if (
                    i > 0 &&
                    i % 4 == 0
                )
                {
                    resultado.Append(" ");
                }


                resultado.Append(
                    clave[i]
                );
            }


            return resultado.ToString();
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RegistrarDispositivoAuthenticator(
    string correo,
    string codigoTotp,
    string fcmToken,
    string? nombreDispositivo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(correo) ||
                    string.IsNullOrWhiteSpace(codigoTotp) ||
                    string.IsNullOrWhiteSpace(fcmToken))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Faltan datos para registrar el dispositivo."
                    });
                }

                correo = correo.Trim().ToLowerInvariant();
                codigoTotp = codigoTotp.Trim();
                fcmToken = fcmToken.Trim();

                Usuario? usuario =
                    await _Context.Usuario
                        .FirstOrDefaultAsync(
                            x => x.Correo.ToLower() == correo
                        );

                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró la cuenta."
                    });
                }

                // =====================================================
                // OBTENER LA CLAVE TOTP
                // =====================================================
                // Si la configuración ya fue confirmada, usamos
                // la clave definitiva de MySQL.
                //
                // Si el usuario está configurando Authenticator por
                // primera vez, todavía está en Session.
                // =====================================================
                string? claveTotp = usuario.TotpSecret;

                if (string.IsNullOrWhiteSpace(claveTotp))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No existe una configuración TOTP para esta cuenta. Genera nuevamente el código QR."
                    });
                }

                // =====================================================
                // VALIDAR EL TOTP GENERADO POR EL CELULAR
                // =====================================================

                bool codigoValido =
                    ValidarCodigoTotp(
                        claveTotp,
                        codigoTotp
                    );

                if (!codigoValido)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "El código TOTP no es válido."
                    });
                }

                // =====================================================
                // CONFIRMAR TOTP AUTOMÁTICAMENTE
                // =====================================================
                // El APK ya demostró que posee la misma clave del QR
                // porque generó un TOTP válido.
                // =====================================================

                if (!usuario.TotpHabilitado ||
                    string.IsNullOrWhiteSpace(usuario.TotpSecret))
                {
                    usuario.TotpSecret = claveTotp;
                    usuario.TotpHabilitado = true;

                    _Context.Usuario.Update(usuario);
                }

                // =====================================================
                // REGISTRAR / ACTUALIZAR DISPOSITIVO
                // =====================================================

                // =====================================================
                // REGISTRAR DISPOSITIVO - SOLO UNO POR CUENTA
                // =====================================================

                AutenticadorDispositivo? dispositivo =
                    await _Context
                        .AUTENTICADOR_DISPOSITIVO
                        .FirstOrDefaultAsync(
                            x => x.UsuarioId == usuario.Id &&
                                 x.Activo
                        );

                if (dispositivo != null)
                {
                    // Si es exactamente el mismo celular, no hay problema.
                    // Esto permite que la app vuelva a comunicarse con el
                    // servidor sin crear duplicados.
                    if (dispositivo.FcmToken == fcmToken)
                    {
                        return Json(new
                        {
                            ok = true,
                            mensaje =
                                "Este dispositivo ya está vinculado con la cuenta."
                        });
                    }

                    // Hay otro celular activo para esta cuenta.
                    return Json(new
                    {
                        ok = false,
                        codigo = "DISPOSITIVO_YA_VINCULADO",
                        mensaje =
                            "Esta cuenta ya está vinculada a otro dispositivo. " +
                            "Debes eliminar la vinculación anterior antes de registrar otro celular."
                    });
                }

                // =====================================================
                // NO EXISTE DISPOSITIVO: CREARLO
                // =====================================================

                dispositivo = new AutenticadorDispositivo
                {
                    UsuarioId = usuario.Id,
                    FcmToken = fcmToken,
                    NombreDispositivo =
                        string.IsNullOrWhiteSpace(nombreDispositivo)
                            ? "Android"
                            : nombreDispositivo.Trim(),
                    FechaVinculacion = DateTime.Now,
                    Activo = true
                };

                _Context
                    .AUTENTICADOR_DISPOSITIVO
                    .Add(dispositivo);

                await _Context.SaveChangesAsync();

                HttpContext.Session.Remove(
                    "TotpSecretPendiente"
                );

                return Json(new
                {
                    ok = true,
                    mensaje =
                        "CrediPlus Authenticator y el dispositivo fueron vinculados correctamente."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR REGISTRANDO DISPOSITIVO AUTHENTICATOR: " +
                    ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo vincular el dispositivo."
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> EliminarCuentaAuthenticator(
    string correo,
    string codigoTotp,
    string fcmToken)
        {
            try
            {
                // ==========================================
                // VALIDAR DATOS
                // ==========================================

                if (string.IsNullOrWhiteSpace(correo) ||
                    string.IsNullOrWhiteSpace(codigoTotp) ||
                    string.IsNullOrWhiteSpace(fcmToken))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "Faltan datos para desvincular la cuenta."
                    });
                }

                correo = correo.Trim().ToLowerInvariant();
                codigoTotp = codigoTotp.Trim();
                fcmToken = fcmToken.Trim();

                // ==========================================
                // BUSCAR USUARIO
                // ==========================================

                Usuario? usuario =
                    await _Context.Usuario
                        .FirstOrDefaultAsync(
                            x => x.Correo.ToLower() == correo
                        );

                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje = "No se encontró la cuenta."
                    });
                }

                // ==========================================
                // VALIDAR TOTP
                // ==========================================

                if (!usuario.TotpHabilitado ||
                    string.IsNullOrWhiteSpace(usuario.TotpSecret))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "La cuenta no tiene Authenticator habilitado."
                    });
                }

                bool codigoValido =
                    ValidarCodigoTotp(
                        usuario.TotpSecret,
                        codigoTotp
                    );

                if (!codigoValido)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se pudo verificar la identidad del Authenticator."
                    });
                }

                // ==========================================
                // VERIFICAR DISPOSITIVO
                // ==========================================

                AutenticadorDispositivo? dispositivo =
                    await _Context
                        .AUTENTICADOR_DISPOSITIVO
                        .FirstOrDefaultAsync(
                            x => x.UsuarioId == usuario.Id &&
                                 x.Activo
                        );

                if (dispositivo == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No existe un dispositivo vinculado para esta cuenta."
                    });
                }

                if (dispositivo.FcmToken != fcmToken)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "Este celular no es el dispositivo vinculado a la cuenta."
                    });
                }

                // ==========================================
                // ELIMINAR SOLICITUDES
                // ==========================================

                var solicitudes =
                    await _Context
                        .AUTENTICADOR_SOLICITUD
                        .Where(
                            x => x.UsuarioId == usuario.Id
                        )
                        .ToListAsync();

                if (solicitudes.Count > 0)
                {
                    _Context
                        .AUTENTICADOR_SOLICITUD
                        .RemoveRange(solicitudes);
                }

                // ==========================================
                // ELIMINAR DISPOSITIVO
                // ==========================================

                _Context
                    .AUTENTICADOR_DISPOSITIVO
                    .Remove(dispositivo);

                // ==========================================
                // LIMPIAR AUTHENTICATOR DEL USUARIO
                // ==========================================

                usuario.TotpSecret = null;
                usuario.TotpHabilitado = false;

                _Context.Usuario.Update(usuario);

                // ==========================================
                // GUARDAR TODO
                // ==========================================

                await _Context.SaveChangesAsync();

                return Json(new
                {
                    ok = true,
                    mensaje =
                        "La cuenta fue desvinculada correctamente."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR ELIMINANDO AUTHENTICATOR: " + ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo desvincular la cuenta."
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResponderSolicitudAuthenticator(
    int solicitudId,
    string numero)
        {
            try
            {
                AutenticadorSolicitud? solicitud =
                    await _Context.AUTENTICADOR_SOLICITUD
                        .FirstOrDefaultAsync(
                            x => x.Id == solicitudId
                        );

                if (solicitud == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "La solicitud no existe."
                    });
                }

                if (solicitud.Estado != "Pendiente")
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "Esta solicitud ya fue procesada."
                    });
                }

                if (DateTime.Now >
                    solicitud.FechaExpiracion)
                {
                    solicitud.Estado =
                        "Expirado";

                    await _Context.SaveChangesAsync();

                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "La solicitud expiró."
                    });
                }
                if (
                    solicitud.NumeroVerificacion !=
                    numero.Trim()
                )
                {
                    solicitud.Estado =
                        "Incorrecto";

                    await _Context.SaveChangesAsync();

                    return Json(new
                    {
                        ok = false,
                        estado = "Incorrecto",
                        mensaje =
                            "El número ingresado es incorrecto."
                    });
                }

                solicitud.Estado =
                    "Aprobado";

                await _Context.SaveChangesAsync();

                return Json(new
                {
                    ok = true,
                    mensaje =
                        "Inicio de sesión aprobado."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR RESPUESTA AUTHENTICATOR: " +
                    ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo aprobar la solicitud."
                });
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult>
     CancelarSolicitudAuthenticator(
         int solicitudId)
        {
            try
            {
                var solicitud =
                    await _Context
                        .AUTENTICADOR_SOLICITUD
                        .FirstOrDefaultAsync(
                            x => x.Id == solicitudId
                        );

                if (solicitud == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se encontró la solicitud."
                    });
                }

                // ==========================================
                // CAMBIAR ESTADO
                // ==========================================

                if (
                    solicitud.Estado ==
                    "Pendiente"
                )
                {
                    if (
                        DateTime.Now >
                        solicitud.FechaExpiracion
                    )
                    {
                        solicitud.Estado =
                            "Expirado";
                    }
                    else
                    {
                        solicitud.Estado =
                            "Rechazado";
                    }

                    await _Context
                        .SaveChangesAsync();

                    // ==========================================
                    // BUSCAR CELULAR VINCULADO
                    // ==========================================

                    var dispositivo =
                        await _Context
                            .AUTENTICADOR_DISPOSITIVO
                            .FirstOrDefaultAsync(
                                x =>
                                    x.UsuarioId ==
                                        solicitud.UsuarioId &&
                                    x.Activo
                            );

                    // ==========================================
                    // AVISAR AL CELULAR
                    // ==========================================

                    if (
                        dispositivo != null &&
                        !string.IsNullOrWhiteSpace(
                            dispositivo.FcmToken
                        )
                    )
                    {
                        try
                        {
                            var mensajeFirebase =
                                new FirebaseAdmin.Messaging.Message
                                {
                                    Token =
                                        dispositivo.FcmToken,

                                    Data =
                                        new Dictionary<
                                            string,
                                            string
                                        >
                                        {
                                    {
                                        "tipo",
                                        "cancelar_solicitud"
                                    },
                                    {
                                        "solicitudId",
                                        solicitud.Id
                                            .ToString()
                                    }
                                        },

                                    Android =
                                        new AndroidConfig
                                        {
                                            Priority =
                                                Priority.High
                                        }
                                };

                            await FirebaseMessaging
                                .DefaultInstance
                                .SendAsync(
                                    mensajeFirebase
                                );

                            Console.WriteLine(
                                "FCM CANCELACIÓN ENVIADO: " +
                                solicitud.Id
                            );
                        }
                        catch (Exception ex)
                        {
                            /*
                             * La solicitud ya quedó
                             * cancelada en la base de datos.
                             * Si Firebase falla, no debemos
                             * deshacer esa cancelación.
                             */
                            Console.WriteLine(
                                "ERROR FCM CANCELACIÓN: " +
                                ex
                            );
                        }
                    }
                }

                return Json(new
                {
                    ok = true,
                    estado =
                        solicitud.Estado
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR CANCELANDO AUTHENTICATOR: " +
                    ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo cancelar la solicitud."
                });
            }
        }
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult>
    ConsultarEstadoSolicitudAuthenticator(
        int solicitudId)
        {
            try
            {
                string? dni =
                    HttpContext.Session.GetString(
                        "DniLoginPendiente"
                    );

                if (string.IsNullOrWhiteSpace(dni))
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "La sesión de inicio ha expirado."
                    });
                }


                Usuario? usuario =
                    await _Context.Usuario
                        .FirstOrDefaultAsync(
                            x => x.Dni == dni
                        );


                if (usuario == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se encontró el usuario."
                    });
                }


                AutenticadorSolicitud? solicitud =
                    await _Context
                        .AUTENTICADOR_SOLICITUD
                        .FirstOrDefaultAsync(
                            x =>
                                x.Id == solicitudId &&
                                x.UsuarioId == usuario.Id
                        );


                if (solicitud == null)
                {
                    return Json(new
                    {
                        ok = false,
                        mensaje =
                            "No se encontró la solicitud."
                    });
                }


                if (
                    solicitud.Estado == "Pendiente" &&
                    DateTime.Now >
                    solicitud.FechaExpiracion
                )
                {
                    solicitud.Estado =
                        "Expirado";

                    await _Context
                        .SaveChangesAsync();
                }


                if (solicitud.Estado != "Aprobado")
                {
                    return Json(new
                    {
                        ok = true,
                        aprobado = false,
                        estado =
                            solicitud.Estado
                    });
                }


                // ==========================================
                // CREAR SESIÓN DEL USUARIO
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
                        CookieAuthenticationDefaults
                            .AuthenticationScheme
                    );


                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults
                        .AuthenticationScheme,

                    new ClaimsPrincipal(
                        claimsIdentity
                    ),

                    new AuthenticationProperties
                    {
                        AllowRefresh = true
                    }
                );


                usuario.EstadoActivo =
                    true;

                usuario.UltimaConexion =
                    DateTime.Now;

                await _Context
                    .SaveChangesAsync();


                string url;

                if (usuario.Rol == "Analista")
                {
                    url =
                        Url.Action(
                            "ProgramaAnalista",
                            "Analista"
                        )!;
                }
                else if (
                    usuario.Rol ==
                    "Administrador"
                )
                {
                    url =
                        Url.Action(
                            "ProgramaAdministrador",
                            "Administrador"
                        )!;
                }
                else
                {
                    url =
                        Url.Action(
                            "DashboardCliente",
                            "Login"
                        )!;
                }


                HttpContext.Session.Remove(
                    "DniLoginPendiente"
                );


                return Json(new
                {
                    ok = true,
                    aprobado = true,
                    estado = "Aprobado",
                    redirectUrl = url
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR CONSULTANDO AUTHENTICATOR: " +
                    ex
                );

                return Json(new
                {
                    ok = false,
                    mensaje =
                        "No se pudo consultar la solicitud."
                });
            }
        }

    }

}