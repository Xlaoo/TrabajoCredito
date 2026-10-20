using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;
using trabajo.Controllers;

namespace TestTrabajoCredito
{
    public class BiometriaVozTests
    {
        private readonly ITestOutputHelper _output;

        public BiometriaVozTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static double InvocarCalcularSimilitudCoseno(float[]? reg, float[]? act)
        {
            var metodo = typeof(LoginController).GetMethod(
                "CalcularSimilitudCoseno",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(metodo);
            return (double)metodo.Invoke(null, new object?[] { reg, act })!;
        }

        private static string InvocarNormalizarTextoVoz(string? texto)
        {
            var metodo = typeof(LoginController).GetMethod(
                "NormalizarTextoVoz",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(metodo);
            return (string)metodo.Invoke(null, new object?[] { texto })!;
        }

        private static bool InvocarVerificarCoincidenciaPalabraClave(string? trans, string? esp)
        {
            var metodo = typeof(LoginController).GetMethod(
                "VerificarCoincidenciaPalabraClave",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(metodo);
            return (bool)metodo.Invoke(null, new object?[] { trans, esp })!;
        }

        [Fact]
        public void ProbarTodasLasValidacionesDeBiometria()
        {
            var runner = new EscenarioRunner(_output, "Biometría de Voz y Comparación de Embeddings Acústicos");

            // 1. Embeddings idénticos de 192 dimensiones (misma persona / misma muestra)
            runner.Evaluar(
                nombreEscenario: "Embeddings acústicos idénticos de 192 dimensiones",
                datoEntrada: "Vector A == Vector A (192 float)",
                esperado: 1.0,
                accion: () =>
                {
                    float[] vec = new float[192];
                    for (int i = 0; i < 192; i++) vec[i] = (float)Math.Sin(i + 1);
                    return Math.Round(InvocarCalcularSimilitudCoseno(vec, vec), 4);
                });

            // 2. Embeddings opuestos (ángulo 180 grados)
            runner.Evaluar(
                nombreEscenario: "Embeddings acústicos opuestos (ángulo 180°)",
                datoEntrada: "Vector A y Vector B = -A",
                esperado: -1.0,
                accion: () =>
                {
                    float[] vecA = new float[192];
                    float[] vecB = new float[192];
                    for (int i = 0; i < 192; i++)
                    {
                        vecA[i] = (float)Math.Cos(i + 1);
                        vecB[i] = -vecA[i];
                    }
                    return Math.Round(InvocarCalcularSimilitudCoseno(vecA, vecB), 4);
                });

            // 3. Embeddings ortogonales (similitud 0.0)
            runner.Evaluar(
                nombreEscenario: "Embeddings acústicos ortogonales (ángulo 90°)",
                datoEntrada: "Vector e1 (1,0,...) vs Vector e2 (0,1,...)",
                esperado: 0.0,
                accion: () =>
                {
                    float[] vecA = new float[192];
                    float[] vecB = new float[192];
                    vecA[0] = 1.0f;
                    vecB[1] = 1.0f;
                    return Math.Round(InvocarCalcularSimilitudCoseno(vecA, vecB), 4);
                });

            // 4. Vectores incompatibles (longitudes dispares: 192 vs 100)
            runner.Evaluar(
                nombreEscenario: "Vectores con dimensiones incompatibles (192 vs 100)",
                datoEntrada: "len(A)=192, len(B)=100",
                esperado: 0.0,
                accion: () =>
                {
                    float[] vec192 = new float[192];
                    float[] vec100 = new float[100];
                    return InvocarCalcularSimilitudCoseno(vec192, vec100);
                });

            // 5-7. Vectores nulos o vacíos
            runner.Evaluar(
                nombreEscenario: "Vector registrado nulo",
                datoEntrada: "reg=null, act=new float[192]",
                esperado: 0.0,
                accion: () => InvocarCalcularSimilitudCoseno(null, new float[192]));

            runner.Evaluar(
                nombreEscenario: "Vector actual nulo",
                datoEntrada: "reg=new float[192], act=null",
                esperado: 0.0,
                accion: () => InvocarCalcularSimilitudCoseno(new float[192], null));

            runner.Evaluar(
                nombreEscenario: "Vectores con arreglo vacío (longitud 0)",
                datoEntrada: "reg=float[0], act=float[0]",
                esperado: 0.0,
                accion: () => InvocarCalcularSimilitudCoseno(new float[0], new float[0]));

            // 8. Vector con norma cero (protección contra división por cero)
            runner.Evaluar(
                nombreEscenario: "Vector con norma cero (todos los componentes en 0)",
                datoEntrada: "Vector ceros vs Vector normal",
                esperado: 0.0,
                accion: () =>
                {
                    float[] ceros = new float[192];
                    float[] normal = new float[192];
                    normal[0] = 1.0f;
                    return InvocarCalcularSimilitudCoseno(ceros, normal);
                });

            // 9-13. Normalización de texto vocal
            runner.Evaluar(
                nombreEscenario: "Normalización: Remoción de tildes y mayúsculas",
                datoEntrada: "\"Mi voz es mi contraseña.\"",
                esperado: "mi voz es mi contrasena",
                accion: () => InvocarNormalizarTextoVoz("Mi voz es mi contraseña."));

            runner.Evaluar(
                nombreEscenario: "Normalización: Remoción de signos de puntuación (comas, signos de exclamación)",
                datoEntrada: "\"Árbol, Canción, Éxito!\"",
                esperado: "arbol cancion exito",
                accion: () => InvocarNormalizarTextoVoz("Árbol, Canción, Éxito!"));

            runner.Evaluar(
                nombreEscenario: "Normalización: Colapso de espacios múltiples y extremos",
                datoEntrada: "\"   Espacios     múltiples   \"",
                esperado: "espacios multiples",
                accion: () => InvocarNormalizarTextoVoz("   Espacios     múltiples   "));

            runner.Evaluar(
                nombreEscenario: "Normalización: Cadena vacía",
                datoEntrada: "\"\"",
                esperado: "",
                accion: () => InvocarNormalizarTextoVoz(""));

            runner.Evaluar(
                nombreEscenario: "Normalización: Cadena nula",
                datoEntrada: "[null]",
                esperado: "",
                accion: () => InvocarNormalizarTextoVoz(null));

            // 14-19. Verificación de palabra clave / frase de seguridad
            runner.Evaluar(
                nombreEscenario: "Palabra clave: Coincidencia exacta con diferencias tipográficas",
                datoEntrada: "Transcrito: 'Mi Voz Es Mi Contraseña' vs Esperado: 'mi voz es mi contrasena'",
                esperado: true,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("Mi Voz Es Mi Contraseña", "mi voz es mi contrasena"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Frase institucional 'CrediPlus Seguro 2026'",
                datoEntrada: "Transcrito: 'CrediPlus Seguro 2026'",
                esperado: true,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("CrediPlus Seguro 2026", "crediplus seguro 2026"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Frase totalmente distinta",
                datoEntrada: "'PalabraClave' vs 'OtraPalabra'",
                esperado: false,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("PalabraClave", "OtraPalabra"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Coincidencia parcial incompleta (debe ser exacta)",
                datoEntrada: "'mi voz' vs 'mi voz es mi contrasena'",
                esperado: false,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("mi voz", "mi voz es mi contrasena"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Transcripción nula",
                datoEntrada: "trans=null, esp='clave'",
                esperado: false,
                accion: () => InvocarVerificarCoincidenciaPalabraClave(null, "clave"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Frase esperada nula",
                datoEntrada: "trans='clave', esp=null",
                esperado: false,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("clave", null));

            runner.VerificarTodosAprobados();
        }

        [Fact]
        public async System.Threading.Tasks.Task ProbarPersistenciaEfCoreActualizacionVoz()
        {
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<trabajo.Models.UsuarioContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_Voz_" + Guid.NewGuid().ToString())
                .Options;

            using (var ctx = new trabajo.Models.UsuarioContext(options))
            {
                var u = new trabajo.Models.Usuario
                {
                    Id = 1,
                    Dni = "12345678",
                    Nombre = "Test",
                    Apellido = "User",
                    Celular = "999888777",
                    Correo = "test@example.com",
                    clave = "hash",
                    Rol = "Cliente",
                    Genero = "M",
                    FraseVoz = "Frase Antigua",
                    EmbeddingVoz = new byte[192 * sizeof(float)],
                    AudioRegistro = "/audios/antiguo.webm",
                    VozHabilitadaLogin = true
                };
                ctx.Usuario.Add(u);
                await ctx.SaveChangesAsync();
            }

            // Simular lo que hace RenovarVozLogin
            using (var ctx = new trabajo.Models.UsuarioContext(options))
            {
                var usuario = ctx.Usuario.FirstOrDefault(x => x.Dni == "12345678");
                Assert.NotNull(usuario);

                usuario.FraseVoz = "Nueva Frase Secreta";
                usuario.AudioRegistro = "/audiosRegistro/nuevo.webm";
                byte[] nuevoEmb = new byte[192 * sizeof(float)];
                nuevoEmb[0] = 42;
                usuario.EmbeddingVoz = nuevoEmb;
                usuario.VozHabilitadaLogin = true;

                var entry = ctx.Entry(usuario);
                entry.Property(u => u.FraseVoz).IsModified = true;
                entry.Property(u => u.EmbeddingVoz).IsModified = true;
                entry.Property(u => u.AudioRegistro).IsModified = true;
                entry.Property(u => u.VozHabilitadaLogin).IsModified = true;

                int filas = await ctx.SaveChangesAsync();
                _output.WriteLine($"Filas afectadas con entry.State=Unchanged: {filas}");
            }

            // Verificar si persistió
            using (var ctx = new trabajo.Models.UsuarioContext(options))
            {
                var usuario = ctx.Usuario.FirstOrDefault(x => x.Dni == "12345678");
                Assert.NotNull(usuario);
                _output.WriteLine($"Frase en DB: '{usuario.FraseVoz}'");
                _output.WriteLine($"Audio en DB: '{usuario.AudioRegistro}'");
                _output.WriteLine($"Embedding[0]: {usuario.EmbeddingVoz?[0]}");
                Assert.Equal("Nueva Frase Secreta", usuario.FraseVoz);
                Assert.Equal("/audiosRegistro/nuevo.webm", usuario.AudioRegistro);
                Assert.Equal((byte)42, usuario.EmbeddingVoz?[0]);
            }
        }
    }
}
