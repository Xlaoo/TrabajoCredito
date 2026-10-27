using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using trabajo.Hubs;
using trabajo.Models;
using trabajo.Service;
using Microsoft.ML.OnnxRuntime;
using FirebaseAdmin;
using Microsoft.AspNetCore.StaticFiles;
using Google.Apis.Auth.OAuth2;
QuestPDF.Settings.License = LicenseType.Community;


var contentRoot = Directory.GetCurrentDirectory();
if (!Directory.Exists(Path.Combine(contentRoot, "Views")))
{
    if (Directory.Exists(Path.Combine(contentRoot, "trabajo", "Views")))
    {
        contentRoot = Path.Combine(contentRoot, "trabajo");
        Directory.SetCurrentDirectory(contentRoot);
    }
    else
    {
        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        if (Directory.Exists(Path.Combine(candidate, "Views")))
        {
            contentRoot = candidate;
            Directory.SetCurrentDirectory(contentRoot);
        }
    }
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

// ==========================================
// FIREBASE ADMIN SDK
// ==========================================
if (FirebaseApp.DefaultInstance == null)
{
    GoogleCredential credential;

    var firebaseJson = builder.Configuration["FIREBASE_ADMIN_JSON"];

    if (!string.IsNullOrWhiteSpace(firebaseJson))
    {
        using var stream = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(firebaseJson)
        );
        credential = GoogleCredential.FromStream(stream);
    }
    else
    {
        credential = GoogleCredential.FromFile(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                "CrediPlusSecrets",
                "firebase-adminsdk.json"
            )
        );
    }

    FirebaseApp.Create(new AppOptions
    {
        Credential = credential
    });
}

// ==========================================
// NORTHFLANK / REVERSE PROXY: FORWARDED HEADERS — CONFIGURACIÓN ESTRICTA
//
// CONFIGURACIÓN DE SEGURIDAD:
//   1. ForwardLimit = 1: Un solo nivel de proxy inverso confiable (Ingress de Northflank).
//   2. KnownProxies / KnownNetworks se configuran ÚNICAMENTE mediante la variable
//      de entorno NORTHFLANK_PROXY_IP y loopback (127.0.0.1 / ::1 para local).
//      No se confía automáticamente en todas las redes RFC 1918 ni link-local.
//   3. El middleware nativo UseForwardedHeaders() es el único responsable de
//      resolver Request.Scheme e IsHttps a partir de X-Forwarded-Proto.
// ==========================================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Un único nivel de proxy (Ingress de Northflank -> Contenedor Kestrel)
    options.ForwardLimit = 1;

    // Limpiar proxies por defecto para configuración explícita y verificada
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // 1. Loopback (Localhost y pruebas locales)
    options.KnownProxies.Add(System.Net.IPAddress.Loopback);      // 127.0.0.1
    options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);  // ::1

    // 2. Proxies/subredes verificados mediante la variable de entorno NORTHFLANK_PROXY_IP
    var proxyEnv = builder.Configuration["NORTHFLANK_PROXY_IP"]
        ?? Environment.GetEnvironmentVariable("NORTHFLANK_PROXY_IP");

    if (!string.IsNullOrWhiteSpace(proxyEnv))
    {
        var entradas = proxyEnv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entrada in entradas)
        {
            if (entrada.Contains('/'))
            {
                var partes = entrada.Split('/', 2);
                if (System.Net.IPAddress.TryParse(partes[0], out var redIp)
                    && int.TryParse(partes[1], out int prefixLen))
                {
                    options.KnownNetworks.Add(
                        new Microsoft.AspNetCore.HttpOverrides.IPNetwork(redIp, prefixLen));

                    // Soporte para IPv4-mapped IPv6 en contenedores Linux
                    if (redIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        options.KnownNetworks.Add(
                            new Microsoft.AspNetCore.HttpOverrides.IPNetwork(redIp.MapToIPv6(), prefixLen + 96));
                    }
                }
                else
                {
                    Console.WriteLine($"[SEGURIDAD] NORTHFLANK_PROXY_IP entrada '{entrada}' no es un CIDR válido.");
                }
            }
            else
            {
                if (System.Net.IPAddress.TryParse(entrada, out var proxyIp))
                {
                    options.KnownProxies.Add(proxyIp);

                    // Soporte para IPv4-mapped IPv6 en contenedores Linux (::ffff:x.x.x.x)
                    if (proxyIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        options.KnownProxies.Add(proxyIp.MapToIPv6());
                    }
                }
                else
                {
                    Console.WriteLine($"[SEGURIDAD] NORTHFLANK_PROXY_IP entrada '{entrada}' no es una IP válida.");
                }
            }
        }
    }
    else
    {
        Console.WriteLine("[SEGURIDAD] NORTHFLANK_PROXY_IP no configurada. " +
            "UseForwardedHeaders solo procesará solicitudes de loopback (127.0.0.1 / ::1). " +
            "En Northflank, configura NORTHFLANK_PROXY_IP con la IP o CIDR del Ingress para habilitar HTTPS.");
    }
});


// ==========================================
// DATA PROTECTION — PERSISTENCIA DE CLAVES
// En Northflank, las claves de Data Protection se pierden
// al reiniciar el contenedor si no se persisten.
// Solución: leer la ruta de persistencia desde la variable
// de entorno DATA_PROTECTION_KEY_PATH.
//
// Para habilitarlo en Northflank:
//   1. Configurar un volumen persistente montado en, p.ej., /app/dpkeys
//   2. Agregar la variable de entorno:
//      DATA_PROTECTION_KEY_PATH=/app/dpkeys
//
// Sin la variable, la app funciona pero las cookies se invalidan
// en cada reinicio del contenedor (los usuarios deben volver a iniciar sesión).
// ==========================================
var dataProtectionKeyPath = builder.Configuration["DATA_PROTECTION_KEY_PATH"]
    ?? Environment.GetEnvironmentVariable("DATA_PROTECTION_KEY_PATH");

if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    var keyDir = new DirectoryInfo(dataProtectionKeyPath);
    if (!keyDir.Exists) keyDir.Create();
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(keyDir)
        .SetApplicationName("CrediPlus");
}
else
{
    // Sin volumen persistente: advertencia, las claves son efímeras.
    Console.WriteLine("[ADVERTENCIA] DATA_PROTECTION_KEY_PATH no configurada. " +
        "Las claves de protección de datos son efímeras: las cookies se invalidan " +
        "en cada reinicio del contenedor. Configura un volumen en Northflank.");
    builder.Services.AddDataProtection()
        .SetApplicationName("CrediPlus");
}

// ==========================================
// CONFIGURACIÓN DE HSTS (HTTP Strict Transport Security)
// Preload = false (evita registro irreversible de dominios hasta validación final)
// ==========================================
builder.Services.AddHsts(options =>
{
    options.Preload = false;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// ==========================================
// MVC CON FILTRO GLOBAL: SIN CACHÉ EN RESPUESTAS PRIVADAS
// (se eliminó el AddControllersWithViews duplicado que existía antes)
// ==========================================
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(
        new ResponseCacheAttribute
        {
            NoStore = true,
            Location = ResponseCacheLocation.None,
        });
});

builder.Services.AddSignalR();
builder.Services.AddDistributedMemoryCache();

// ==========================================
// PROTECCIÓN ANTI-FORGERY (CSRF)
// ==========================================
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// ==========================================
// SESIÓN
// ==========================================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // Localhost: SameAsRequest (funciona con HTTP).
    // Producción (Northflank): Always (solo HTTPS).
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// ==========================================
// BASE DE DATOS (MySQL en Aiven)
// ==========================================
var connectionString = builder.Configuration.GetConnectionString("cadenaSQL");

builder.Services.AddDbContext<UsuarioContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.Parse("9.4.0-mysql"),
        mysqlOptions =>
        {
            mysqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null
            );
        }
    )
);

builder.Services.AddScoped<IusuarioServices, UsuariService>();
builder.Services.AddScoped<IServicioEvaluacionCrediticia, ServicioEvaluacionCrediticia>();
builder.Services.AddSingleton<ServicioEmbeddingVoz>();
builder.Services.AddHttpClient<ITurnstileService, TurnstileService>();

// ==========================================
// AUTENTICACIÓN POR COOKIES + RBAC (ClaimTypes.Role)
// El rol se emite desde la base de datos al hacer login.
// Valores reales: "Cliente", "Analista", "Administrador".
// ==========================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/IniciarSesion";
        options.AccessDeniedPath = "/Login/AccesoDenegado";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Localhost: SameAsRequest (funciona con HTTP).
        // Producción (Northflank con HTTPS terminado en el proxy): Always.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

var app = builder.Build();

// ==========================================
// PIPELINE HTTP
// UseForwardedHeaders DEBE ir primero para que el resto
// del pipeline (HTTPS redirect, HSTS) reciba el proto real.
// ==========================================
app.UseForwardedHeaders();

// Log de diagnóstico pasivo (no altera el esquema de la petición ni debilita la seguridad)
// Permite identificar con certeza en los logs de Northflank la IP exacta del Ingress
// para configurarla en NORTHFLANK_PROXY_IP sin necesidad de adivinar ni inventar valores.
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps)
        {
            var proto = context.Request.Headers["X-Forwarded-Proto"].ToString();
            if (!string.IsNullOrEmpty(proto))
            {
                var remoteIp = context.Connection.RemoteIpAddress;
                Console.WriteLine($"[DIAGNÓSTICO PROXY] Petición con X-Forwarded-Proto='{proto}' no procesada como HTTPS. " +
                    $"RemoteIp de la conexión TCP: '{remoteIp}'. Si este es el Ingress de Northflank, " +
                    $"configura NORTHFLANK_PROXY_IP={remoteIp} en las variables de entorno de Northflank.");
            }
        }

        await next();
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// ==========================================
// CABECERAS DE SEGURIDAD HTTP CONTROLADAS (OWASP / Northflank)
// ==========================================
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "microphone=(self), camera=(), geolocation=(), payment=(), usb=(), display-capture=()";
    headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";

    // Content Security Policy (CSP) activa y protectora para mitigar XSS y data injection
    // Rutas con CSP estricta (sin 'unsafe-inline' en script-src y style-src):
    // - Páginas públicas: /, /Login, /Login/PantallaPrincipal, /Login/IniciarSesion, /Login/Registro, /Login/OlvideContrasena, /Home/Error
    // - Pantallas internas cliente migradas: /Login/DashboardCliente, /Login/MisSolicitudes, /Login/PagosPendientes, /Login/SimuladorCredito, /Login/SolicitarCredito
    // Para el resto de rutas (p.ej. paneles aún no migrados) se mantiene compatibilidad con 'unsafe-inline'.
    var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
    var path = (exceptionFeature?.Path ?? context.Request.Path.Value)?.TrimEnd('/') ?? "";
    bool esRutaEstricta =
        string.IsNullOrEmpty(path) ||
        string.Equals(path, "/Login", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Login/PantallaPrincipal", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Login/IniciarSesion", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Login/Registro", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Login/OlvideContrasena", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Login/DashboardCliente", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Login/MisSolicitudes", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Login/PagosPendientes", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Login/SimuladorCredito", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Login/SolicitarCredito", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/Home/Error", StringComparison.OrdinalIgnoreCase);

    string scriptSrc = esRutaEstricta
        ? "script-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://challenges.cloudflare.com; "
        : "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://challenges.cloudflare.com; ";

    string styleSrc = esRutaEstricta
        ? "style-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; "
        : "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; ";

    headers["Content-Security-Policy"] = 
        "default-src 'self'; " +
        scriptSrc +
        styleSrc +
        "img-src 'self' data: blob: https://cdn-icons-png.flaticon.com https://challenges.cloudflare.com; " +
        "font-src 'self' data: https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.gstatic.com; " +
        "connect-src 'self' https://challenges.cloudflare.com; " +
        "frame-src 'self' https://challenges.cloudflare.com; " +
        "frame-ancestors 'none'; " +
        "media-src 'self' blob:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';";

    // Si la solicitud es HTTPS o proviene de proxy HTTPS de Northflank (X-Forwarded-Proto),
    // asegurar que se emita HSTS si no está presente
    bool esHttps = context.Request.IsHttps || 
                   string.Equals(context.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);

    if (esHttps && !headers.ContainsKey("Strict-Transport-Security") && !app.Environment.IsDevelopment())
    {
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});

var proveedorTiposArchivo = new FileExtensionContentTypeProvider();
proveedorTiposArchivo.Mappings[".apk"] =
    "application/vnd.android.package-archive";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = proveedorTiposArchivo,
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.ContentType = "text/css; charset=utf-8";
        }
        else if (ctx.File.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.ContentType = "application/javascript; charset=utf-8";
        }
    }
});

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// ENDPOINTS
// ==========================================

// Endpoint público de Liveness Health Check para Northflank.
app.MapGet("/health", () => Results.Text("Healthy", "text/plain", statusCode: StatusCodes.Status200OK))
   .AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=PantallaPrincipal}/{id?}");

// SignalR Hub — protegido con [Authorize(Roles="Analista,Administrador")]
// directamente en ChatAnalistaHub.cs
app.MapHub<ChatAnalistaHub>("/chatAnalistaHub");

app.Run();
