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

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Seseo fonético ('vos' vs 'voz')",
                datoEntrada: "trans='mi vos es mi clave', esp='mi voz es mi clave'",
                esperado: true,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("mi vos es mi clave", "mi voz es mi clave"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Signos de puntuación y mayúsculas en registro",
                datoEntrada: "trans='mi voz es mi clave', esp='¡Mi Voz, Es Mi Clave!'",
                esperado: true,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("mi voz es mi clave", "¡Mi Voz, Es Mi Clave!"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Frase diferente de longitud similar (rechazo estricto)",
                datoEntrada: "trans='otra cosa cualquiera', esp='mi voz es mi clave'",
                esperado: false,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("otra cosa cualquiera", "mi voz es mi clave"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Palabra corta 'rafa' con repetición por eco/micrófono ('rafa rafa')",
                datoEntrada: "trans='rafa rafa', esp='rafa'",
                esperado: true,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("rafa rafa", "rafa"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Palabra 'rafa' con punto final registrado ('Rafa.')",
                datoEntrada: "trans='rafa', esp='Rafa.'",
                esperado: true,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("rafa", "Rafa."));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Palabra 'Rafa.' vs transcripción 'rafa rafa'",
                datoEntrada: "trans='rafa rafa', esp='Rafa.'",
                esperado: true,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("rafa rafa", "Rafa."));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Rechazo estricto de palabra no autorizada 'mesa' vs 'rafa'",
                datoEntrada: "trans='mesa', esp='rafa'",
                esperado: false,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("mesa", "rafa"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Rechazo estricto de 'hola' vs 'rafa'",
                datoEntrada: "trans='hola', esp='rafa'",
                esperado: false,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("hola", "rafa"));

            runner.Evaluar(
                nombreEscenario: "Palabra clave: Rechazo de frase larga vs palabra corta",
                datoEntrada: "trans='mi voz es mi contrasena', esp='rafa'",
                esperado: false,
                accion: () => InvocarVerificarCoincidenciaPalabraClave("mi voz es mi contrasena", "rafa"));

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

        [Fact]
        public async System.Threading.Tasks.Task DiagnosticarAudiosReales()
        {
            string carpetaAudios = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "wwwroot", "audiosRegistro"));
            if (!Directory.Exists(carpetaAudios))
            {
                _output.WriteLine("Carpeta audiosRegistro no encontrada: " + carpetaAudios);
                return;
            }

            var archivos = Directory.GetFiles(carpetaAudios, "*.webm");
            _output.WriteLine($"Audios encontrados: {archivos.Length}");

            var servicioVoz = new trabajo.Service.ServicioEmbeddingVoz();
            var metodoVosk = typeof(LoginController).GetMethod("TranscribirAudioConVosk", BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(string) });
            var metodoPalabra = typeof(LoginController).GetMethod("VerificarCoincidenciaPalabraClave", BindingFlags.NonPublic | BindingFlags.Static);

            var embeddings = new System.Collections.Generic.Dictionary<string, float[]>();

            foreach (var archivo in archivos)
            {
                var fi = new FileInfo(archivo);
                _output.WriteLine($"\n--- ARCHIVO: {fi.Name} (Bytes: {fi.Length}) ---");

                using var stream = File.OpenRead(archivo);
                var formFile = new Microsoft.AspNetCore.Http.FormFile(stream, 0, fi.Length, "audio", fi.Name)
                {
                    Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                    ContentType = "audio/webm"
                };

                string? rutaWav = null;
                try
                {
                    rutaWav = await servicioVoz.ConvertirAudioAWavPcm16k(formFile);
                    var wavFi = new FileInfo(rutaWav);
                    _output.WriteLine($"  Convertido a WAV: {wavFi.Length} bytes");

                    string transcripcion = (string)metodoVosk!.Invoke(null, new object[] { rutaWav })!;
                    _output.WriteLine($"  Transcripción Vosk: '{transcripcion}'");

                    bool coincideRafa = (bool)metodoPalabra!.Invoke(null, new object[] { transcripcion, "rafa" })!;
                    bool coincideRafaPunto = (bool)metodoPalabra.Invoke(null, new object[] { transcripcion, "Rafa." })!;
                    _output.WriteLine($"  Coincide con 'rafa': {coincideRafa} | Coincide con 'Rafa.': {coincideRafaPunto}");

                    var candidatos = await servicioVoz.GenerarEmbeddingsCandidatos(rutaWav);
                    _output.WriteLine($"  Candidatos de embeddings: {candidatos.Count}");
                    if (candidatos.Count > 0)
                    {
                        embeddings[fi.Name] = candidatos[0];
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  ERROR procesando {fi.Name}: {ex.Message}");
                }
                finally
                {
                    if (rutaWav != null && File.Exists(rutaWav))
                    {
                        try { File.Delete(rutaWav); } catch { }
                    }
                }
            }

            // Comparar similitud cruzada entre todos los embeddings
            _output.WriteLine("\n=== MATRIZ DE SIMILITUD COSENO CRUZADA ===");
            var keys = new System.Collections.Generic.List<string>(embeddings.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                for (int j = i + 1; j < keys.Count; j++)
                {
                    double sim = InvocarCalcularSimilitudCoseno(embeddings[keys[i]], embeddings[keys[j]]);
                    _output.WriteLine($"  {keys[i]} vs {keys[j]}: Similitud = {sim:F4} (Umbral >= 0.65: {(sim >= 0.65 ? "PASA" : "FALLA")})");
                }
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task AuditarBaseDatosUsuariosVoz()
        {
            string secretsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "UserSecrets", "CrediPlus-Local-Development", "secrets.json"
            );

            if (!File.Exists(secretsPath))
            {
                _output.WriteLine("secrets.json no encontrado en: " + secretsPath);
                return;
            }

            string json = File.ReadAllText(secretsPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            string? connStr = null;

            if (doc.RootElement.TryGetProperty("ConnectionStrings:cadenaSQL", out var cadPropFlat))
            {
                connStr = cadPropFlat.GetString();
            }
            else if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csProp) &&
                csProp.TryGetProperty("cadenaSQL", out var cadProp))
            {
                connStr = cadProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(connStr))
            {
                _output.WriteLine("cadenaSQL no configurada en secrets.json");
                return;
            }

            var optionsBuilder = new DbContextOptionsBuilder<trabajo.Models.UsuarioContext>();
            optionsBuilder.UseMySql(connStr, ServerVersion.Parse("9.4.0-mysql"));

            using var ctx = new trabajo.Models.UsuarioContext(optionsBuilder.Options);
            var usuarios = await ctx.Usuario.AsNoTracking().ToListAsync();

            _output.WriteLine($"\n=== AUDITORÍA DE USUARIOS EN MYSQL ({usuarios.Count} registros) ===");
            var servicioVoz = new trabajo.Service.ServicioEmbeddingVoz();

            foreach (var u in usuarios)
            {
                bool tieneVoz = !string.IsNullOrWhiteSpace(u.FraseVoz) || u.EmbeddingVoz != null || !string.IsNullOrWhiteSpace(u.AudioRegistro);
                _output.WriteLine($"\n[Usuario ID: {u.Id}] DNI: {u.Dni} | Rol: {u.Rol} | Activo: {u.EstadoActivo}");
                _output.WriteLine($"  - VozHabilitadaLogin: {u.VozHabilitadaLogin}");
                _output.WriteLine($"  - FraseVoz: '{u.FraseVoz ?? "[NULL]"}'");
                _output.WriteLine($"  - AudioRegistro: '{u.AudioRegistro ?? "[NULL]"}'");
                _output.WriteLine($"  - EmbeddingVoz bytes: {(u.EmbeddingVoz?.Length ?? 0)} (Esperado: 768 bytes = 192 floats)");

                if (u.EmbeddingVoz != null && u.EmbeddingVoz.Length == 768)
                {
                    float[] embDb = new float[192];
                    Buffer.BlockCopy(u.EmbeddingVoz, 0, embDb, 0, 768);

                    double norma = 0;
                    bool tieneNanOInf = false;
                    for (int i = 0; i < 192; i++)
                    {
                        if (float.IsNaN(embDb[i]) || float.IsInfinity(embDb[i])) tieneNanOInf = true;
                        norma += embDb[i] * embDb[i];
                    }
                    norma = Math.Sqrt(norma);
                    _output.WriteLine($"  - EmbeddingVoz en DB: Válido = {!tieneNanOInf}, Norma L2 = {norma:F4}");

                    // Si existe el archivo en AudioRegistro, generar su embedding y comparar
                    if (!string.IsNullOrWhiteSpace(u.AudioRegistro))
                    {
                        string rutaFisica = Path.Combine(
                            AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "wwwroot",
                            u.AudioRegistro.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                        );
                        rutaFisica = Path.GetFullPath(rutaFisica);

                        if (File.Exists(rutaFisica))
                        {
                            var fi = new FileInfo(rutaFisica);
                            using var s = File.OpenRead(rutaFisica);
                            var formFile = new Microsoft.AspNetCore.Http.FormFile(s, 0, fi.Length, "audio", fi.Name)
                            {
                                Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                                ContentType = "audio/webm"
                            };

                            string? wavPath = null;
                            try
                            {
                                wavPath = await servicioVoz.ConvertirAudioAWavPcm16k(formFile);
                                var candidatos = await servicioVoz.GenerarEmbeddingsCandidatos(wavPath);
                                if (candidatos.Count > 0)
                                {
                                    double simAudioVsDb = InvocarCalcularSimilitudCoseno(embDb, candidatos[0]);
                                    _output.WriteLine($"  - Comparación DB vs AudioRegistro ({fi.Name}): Similitud = {simAudioVsDb:F4} (Match: {(simAudioVsDb >= 0.95 ? "IDÉNTICO / COMPATIBLE" : "DISCREPANCIA")})");
                                }
                            }
                            catch (Exception ex)
                            {
                                _output.WriteLine($"  - Error analizando AudioRegistro: {ex.Message}");
                            }
                            finally
                            {
                                if (wavPath != null && File.Exists(wavPath))
                                {
                                    try { File.Delete(wavPath); } catch { }
                                }
                            }
                        }
                        else
                        {
                            _output.WriteLine($"  - AudioRegistro NO EXISTE en disco: {rutaFisica}");
                        }
                    }
                }
            }
        }

        [Fact]
        public void ProbarConstructoresVosk()
        {
            var ctors = typeof(Vosk.VoskRecognizer).GetConstructors();
            foreach (var c in ctors)
            {
                _output.WriteLine("Constructor Vosk: " + c.ToString());
            }

            var methods = typeof(Vosk.VoskRecognizer).GetMethods();
            foreach (var m in methods)
            {
                if (m.Name.Contains("Grammar", StringComparison.OrdinalIgnoreCase))
                {
                    _output.WriteLine("Método Grammar: " + m.ToString());
                }
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task ProbarTranscripcionConGrammar()
        {
            string archivoWebm = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "wwwroot", "audiosRegistro", "648a682c-64db-455b-85dc-3b35db81e0f4.webm"));
            if (!File.Exists(archivoWebm)) return;

            var fi = new FileInfo(archivoWebm);
            using var s = File.OpenRead(archivoWebm);
            var formFile = new Microsoft.AspNetCore.Http.FormFile(s, 0, fi.Length, "audio", fi.Name)
            {
                Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                ContentType = "audio/webm"
            };

            var servicioVoz = new trabajo.Service.ServicioEmbeddingVoz();
            string rutaWav = await servicioVoz.ConvertirAudioAWavPcm16k(formFile);

            try
            {
                var metodoModelo = typeof(LoginController).GetMethod("ObtenerModeloVosk", BindingFlags.NonPublic | BindingFlags.Static);
                var model = (Vosk.Model)metodoModelo!.Invoke(null, null)!;

                // 1. Sin grammar (estándar actual)
                using (var recSin = new Vosk.VoskRecognizer(model, 16000.0f))
                {
                    byte[] bytes = File.ReadAllBytes(rutaWav);
                    recSin.AcceptWaveform(bytes, bytes.Length);
                    _output.WriteLine("Sin grammar: " + recSin.FinalResult());
                }

                // 2. Con grammar enfocado en la frase esperada
                string grammarJson = "[\"rafa\", \"rafa rafa\", \"[unk]\"]";
                using (var recCon = new Vosk.VoskRecognizer(model, 16000.0f, grammarJson))
                {
                    byte[] bytes = File.ReadAllBytes(rutaWav);
                    recCon.AcceptWaveform(bytes, bytes.Length);
                    _output.WriteLine("Con grammar [\"rafa\", \"rafa rafa\", \"[unk]\"] en 648a: " + recCon.FinalResult());
                }

                // Probar en 96d4bce7 (donde sin grammar decia 'mesa')
                string archMesa = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "wwwroot", "audiosRegistro", "96d4bce7-bd8d-4992-b4d9-4260b839e9a9.webm"));
                if (File.Exists(archMesa))
                {
                    var fiM = new FileInfo(archMesa);
                    using var sM = File.OpenRead(archMesa);
                    var ffM = new Microsoft.AspNetCore.Http.FormFile(sM, 0, fiM.Length, "audio", fiM.Name)
                    {
                        Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                        ContentType = "audio/webm"
                    };
                    string wavM = await servicioVoz.ConvertirAudioAWavPcm16k(ffM);
                    try
                    {
                        using var recMesaSin = new Vosk.VoskRecognizer(model, 16000.0f);
                        byte[] bM = File.ReadAllBytes(wavM);
                        recMesaSin.AcceptWaveform(bM, bM.Length);
                        _output.WriteLine("96d4bce7 Sin grammar: " + recMesaSin.FinalResult());

                        using var recMesaCon = new Vosk.VoskRecognizer(model, 16000.0f, grammarJson);
                        recMesaCon.AcceptWaveform(bM, bM.Length);
                        _output.WriteLine("96d4bce7 Con grammar: " + recMesaCon.FinalResult());
                    }
                    finally
                    {
                        if (File.Exists(wavM)) File.Delete(wavM);
                    }
                }
            }
            finally
            {
                if (File.Exists(rutaWav)) File.Delete(rutaWav);
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task InspeccionarVadYFrames()
        {
            var servicioVoz = new trabajo.Service.ServicioEmbeddingVoz();
            string p1 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "wwwroot", "audiosRegistro", "615f047e-0ad1-47dd-8f42-d8080dda6a13.webm"));
            string p2 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "wwwroot", "audiosRegistro", "648a682c-64db-455b-85dc-3b35db81e0f4.webm"));
            string p3 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "wwwroot", "audiosRegistro", "190afa5f-d4f4-4b1d-980b-999f5b7985f6.webm"));

            foreach (var path in new[] { p1, p2, p3 })
            {
                var fi = new FileInfo(path);
                using var s = File.OpenRead(path);
                var formFile = new Microsoft.AspNetCore.Http.FormFile(s, 0, fi.Length, "audio", fi.Name)
                {
                    Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                    ContentType = "audio/webm"
                };

                string wav = await servicioVoz.ConvertirAudioAWavPcm16k(formFile);
                try
                {
                    using var reader = new NAudio.Wave.WaveFileReader(wav);
                    _output.WriteLine($"\nArchivo: {fi.Name}");
                    _output.WriteLine($"  Duración total WAV: {reader.TotalTime.TotalSeconds:F2} segundos ({reader.SampleCount} muestras a {reader.WaveFormat.SampleRate} Hz)");

                    var candidatos = await servicioVoz.GenerarEmbeddingsCandidatos(wav);
                    _output.WriteLine($"  Candidatos generados: {candidatos.Count}");
                }
                finally
                {
                    if (File.Exists(wav)) File.Delete(wav);
                }
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task ProbarSegmentosVoceadosEnAudioLargo()
        {
            var servicioVoz = new trabajo.Service.ServicioEmbeddingVoz();
            string pReg = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "wwwroot", "audiosRegistro", "615f047e-0ad1-47dd-8f42-d8080dda6a13.webm"));
            string pLargo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "wwwroot", "audiosRegistro", "648a682c-64db-455b-85dc-3b35db81e0f4.webm"));

            if (!File.Exists(pReg) || !File.Exists(pLargo)) return;

            // Obtener embedding registrado limpio de 615f047e
            var fiReg = new FileInfo(pReg);
            using var sReg = File.OpenRead(pReg);
            var formReg = new Microsoft.AspNetCore.Http.FormFile(sReg, 0, fiReg.Length, "audio", fiReg.Name)
            {
                Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                ContentType = "audio/webm"
            };
            string wavReg = await servicioVoz.ConvertirAudioAWavPcm16k(formReg);
            var candsReg = await servicioVoz.GenerarEmbeddingsCandidatos(wavReg);
            float[] embReg = candsReg[0];
            File.Delete(wavReg);

            // Analizar 648a682c con ventanas deslizantes
            var fiLargo = new FileInfo(pLargo);
            using var sLargo = File.OpenRead(pLargo);
            var formLargo = new Microsoft.AspNetCore.Http.FormFile(sLargo, 0, fiLargo.Length, "audio", fiLargo.Name)
            {
                Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                ContentType = "audio/webm"
            };
            string wavLargo = await servicioVoz.ConvertirAudioAWavPcm16k(formLargo);

            try
            {
                using var reader = new NAudio.Wave.WaveFileReader(wavLargo);
                byte[] bytes = new byte[reader.Length];
                reader.Read(bytes, 0, bytes.Length);
                short[] muestras = new short[bytes.Length / 2];
                Buffer.BlockCopy(bytes, 0, muestras, 0, bytes.Length);

                float[] mono = new float[muestras.Length];
                for (int i = 0; i < muestras.Length; i++) mono[i] = muestras[i] / 32768f;

                // Buscar ventanas de 1 a 2 segundos (16000 a 32000 muestras) con alta energía
                const int tamVentana = 16000; // 1 segundo = 100 frames
                _output.WriteLine($"Audio largo: {mono.Length} muestras ({mono.Length / 16000.0:F2}s)");

                double maxSim = 0;
                int mejorOffset = 0;

                // Deslizar con paso de 0.25s (4000 muestras)
                var metodoCalcular = typeof(trabajo.Service.ServicioEmbeddingVoz).GetMethod("CalcularEmbeddingParaMuestras", BindingFlags.NonPublic | BindingFlags.Instance);

                for (int offset = 0; offset + tamVentana <= mono.Length; offset += 4000)
                {
                    float[] ventana = new float[tamVentana];
                    Array.Copy(mono, offset, ventana, 0, tamVentana);

                    // Energía de la ventana
                    double energia = 0;
                    for (int i = 0; i < tamVentana; i++) energia += ventana[i] * ventana[i];
                    energia /= tamVentana;

                    if (energia < 0.0001) continue; // silencio

                    int frames = (tamVentana - 400) / 160 + 1;
                    float[] embVentana = (float[])metodoCalcular!.Invoke(servicioVoz, new object[] { ventana, frames })!;

                    double sim = InvocarCalcularSimilitudCoseno(embReg, embVentana);
                    _output.WriteLine($"  Offset {offset / 16000.0:F2}s - Energia: {energia:F6} - Similitud con 615f: {sim:F4} (>=0.65: {(sim >= 0.65 ? "PASA" : "falla")})");

                    if (sim > maxSim)
                    {
                        maxSim = sim;
                        mejorOffset = offset;
                    }
                }

                _output.WriteLine($"\nMEJOR SIMILITUD ENCONTRADA EN AUDIO LARGO: {maxSim:F4} en offset {mejorOffset / 16000.0:F2}s");
            }
            finally
            {
                if (File.Exists(wavLargo)) File.Delete(wavLargo);
            }
        }
    }
}
