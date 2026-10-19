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
// NORTHFLANK: FORWARDED HEADERS — CONFIGURACIÓN SEGURA
//
// PROBLEMA con la configuración anterior:
//   KnownNetworks.Clear() + KnownProxies.Clear() sin restricciones hace que
//   ASP.NET Core acepte X-Forwarded-For de CUALQUIER cliente que se conecte
//   directamente al contenedor. Un atacante puede falsificar su IP real.
//
// SOLUCIÓN:
//   Registrar explícitamente la IP del proxy de Northflank como KnownProxy.
//   La IP se lee de la variable de entorno NORTHFLANK_PROXY_IP para no
//   codificarla en código fuente (Northflank usa IPs dinámicas que pueden
//   cambiar entre despliegues).
//
// PARA CONFIGURAR EN NORTHFLANK:
//   1. En el dashboard de Northflank, ve al servicio → Variables de entorno.
//   2. Agrega:  NORTHFLANK_PROXY_IP=<IP o CIDR del proxy>
//      Ejemplo: NORTHFLANK_PROXY_IP=10.0.0.0/8
//      Si Northflank expone la IP exacta del ingress, úsala directamente:
//      NORTHFLANK_PROXY_IP=203.0.113.42
//   3. Reinicia el servicio para que tome efecto.
//
// INFORMACIÓN PENDIENTE:
//   La IP o rango CIDR del proxy de Northflank no es pública ni estática.
//   Debes obtenerla desde el panel de Northflank en:
//     Settings → Networking → Ingress / Load Balancer IP
//   o contactar al soporte de Northflank.
//   Sin esa información, usa la variable de entorno con el CIDR interno
//   que Northflank te indique (normalmente una red privada 10.x.x.x/8).
//
// COMPORTAMIENTO POR MODO:
//   - Desarrollo (localhost): confía en loopback (127.0.0.1) para pruebas.
//   - Producción + NORTHFLANK_PROXY_IP configurada: solo confía en esa IP/red.
//   - Producción sin NORTHFLANK_PROXY_IP: NO reenvía cabeceras (seguro por defecto).
//     Se imprime una advertencia en consola y la IP real del cliente será la
//     del proxy de Northflank (inofensivo para la funcionalidad, pero sin
//     IP real del cliente en logs).
// ==========================================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Limpiar los valores por defecto (loopback) para reconfigurar
    // solo con las redes/proxies que conocemos explícitamente.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    if (builder.Environment.IsDevelopment())
    {
        // En localhost el proxy es el propio proceso o Kestrel directo.
        // Solo se confía en loopback.
        options.KnownProxies.Add(System.Net.IPAddress.Loopback);      // 127.0.0.1
        options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);  // ::1
    }
    else
    {
        // PRODUCCIÓN: solo se confía en el proxy registrado en la variable de entorno.
        var proxyEnv = builder.Configuration["NORTHFLANK_PROXY_IP"]
            ?? Environment.GetEnvironmentVariable("NORTHFLANK_PROXY_IP");

        if (!string.IsNullOrWhiteSpace(proxyEnv))
        {
            // Soporta tanto IP única ("203.0.113.42")
            // como notación CIDR ("10.0.0.0/8").
            if (proxyEnv.Contains('/'))
            {
                // CIDR → registrar como KnownNetwork
                var partes = proxyEnv.Split('/', 2);
                if (System.Net.IPAddress.TryParse(partes[0], out var redIp)
                    && int.TryParse(partes[1], out int prefixLen))
                {
                    options.KnownNetworks.Add(
                        new Microsoft.AspNetCore.HttpOverrides.IPNetwork(redIp, prefixLen));
                }
                else
                {
                    Console.WriteLine($"[SEGURIDAD] NORTHFLANK_PROXY_IP='{proxyEnv}' no es un CIDR válido. " +
                        "Los Forwarded Headers NO se procesarán.");
                }
            }
            else
            {
                // IP única → registrar como KnownProxy
                if (System.Net.IPAddress.TryParse(proxyEnv, out var proxyIp))
                {
                    options.KnownProxies.Add(proxyIp);
                }
                else
                {
                    Console.WriteLine($"[SEGURIDAD] NORTHFLANK_PROXY_IP='{proxyEnv}' no es una IP válida. " +
                        "Los Forwarded Headers NO se procesarán.");
                }
            }
        }
        else
        {
            // Variable no configurada: NO procesar cabeceras reenviadas.
            // Seguro por defecto: la IP visible será la del proxy de Northflank,
            // pero no se aceptarán valores X-Forwarded-* de origen desconocido.
            options.ForwardedHeaders = ForwardedHeaders.None;
            Console.WriteLine("[SEGURIDAD] NORTHFLANK_PROXY_IP no configurada. " +
                "UseForwardedHeaders está DESHABILITADO en producción. " +
                "Configura la variable con la IP/CIDR del proxy de Northflank.");
        }
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
    headers["Content-Security-Policy"] = 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://challenges.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "img-src 'self' data: blob: https://cdn-icons-png.flaticon.com https://challenges.cloudflare.com; " +
        "font-src 'self' data: https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "connect-src 'self' wss: ws: https://challenges.cloudflare.com; " +
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
    ContentTypeProvider = proveedorTiposArchivo
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
