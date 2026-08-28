using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using trabajo.Hubs;
using trabajo.Models;
using trabajo.Service;
using Microsoft.ML.OnnxRuntime;
QuestPDF.Settings.License = LicenseType.Community;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
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
builder.Services.AddSingleton<ServicioEmbeddingVoz>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/IniciarSesion";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    });
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(
        new ResponseCacheAttribute
        {
            NoStore = true,
            Location = ResponseCacheLocation.None,
        }
        );
});
string modeloVoz = Path.Combine(
    Directory.GetCurrentDirectory(),
    "ModelosVoz",
    "ecapa-speaker-v1.onnx"
);

using (var sessionVoz = new InferenceSession(modeloVoz))
{
    Console.WriteLine("========== MODELO ECAPA ==========");

    foreach (var entrada in sessionVoz.InputMetadata)
    {
        Console.WriteLine($"ENTRADA: {entrada.Key}");
        Console.WriteLine($"TIPO: {entrada.Value.ElementType}");
        Console.WriteLine(
            $"DIMENSIONES: {string.Join(", ", entrada.Value.Dimensions)}"
        );
    }

    foreach (var salida in sessionVoz.OutputMetadata)
    {
        Console.WriteLine($"SALIDA: {salida.Key}");
        Console.WriteLine($"TIPO: {salida.Value.ElementType}");
        Console.WriteLine(
            $"DIMENSIONES: {string.Join(", ", salida.Value.Dimensions)}"
        );
    }

    Console.WriteLine("=================================");
}
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=PantallaPrincipal}/{id?}");
app.MapHub<ChatAnalistaHub>("/chatAnalistaHub");
app.Run();
