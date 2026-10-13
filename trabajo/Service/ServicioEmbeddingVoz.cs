using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.Wave;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using System.Numerics;
using System.Diagnostics;
namespace trabajo.Service
{
    public class ServicioEmbeddingVoz
    {
        private readonly InferenceSession _sesion;

        public ServicioEmbeddingVoz()
        {
            string rutaModelo = Path.Combine(
                AppContext.BaseDirectory,
                "ModelosVoz",
                "ecapa-speaker-v1.onnx"
            );

            if (!File.Exists(rutaModelo))
            {
                throw new FileNotFoundException(
                    "No se encontró el modelo ECAPA.",
                    rutaModelo
                );
            }

            _sesion = new InferenceSession(rutaModelo);
        }

        public async Task<float[]> GenerarEmbeddingDesdeAudio(IFormFile audio)
        {
            var candidatos = await GenerarEmbeddingsCandidatos(audio);
            if (candidatos == null || candidatos.Count == 0)
            {
                throw new Exception("No se pudo generar el embedding de voz.");
            }
            return candidatos[0];
        }

        public async Task<string> ConvertirAudioAWavPcm16k(IFormFile audio)
        {
            if (audio == null || audio.Length == 0)
                throw new Exception("No se recibió ningún audio.");

            string carpetaTemporal = Path.Combine(
                Path.GetTempPath(),
                "CrediPlusVoz"
            );

            Directory.CreateDirectory(carpetaTemporal);

            string nombreTemporal = Guid.NewGuid().ToString();

            string extension = Path.GetExtension(audio.FileName ?? ".webm");
            if (string.IsNullOrWhiteSpace(extension)) extension = ".webm";

            string rutaWebM = Path.Combine(
                carpetaTemporal,
                nombreTemporal + extension
            );

            string rutaAudio = Path.Combine(
                carpetaTemporal,
                nombreTemporal + ".wav"
            );

            try
            {
                using (var archivo = new FileStream(
                    rutaWebM,
                    FileMode.Create))
                {
                    await audio.CopyToAsync(archivo);
                }

                if (OperatingSystem.IsWindows())
                {
                    string carpetaFFmpeg = Path.Combine(
                        carpetaTemporal,
                        "ffmpeg"
                    );

                    Directory.CreateDirectory(carpetaFFmpeg);

                    await FFmpegDownloader.GetLatestVersion(
                        FFmpegVersion.Official,
                        carpetaFFmpeg
                    );

                    FFmpeg.SetExecutablesPath(carpetaFFmpeg);
                }
                else
                {
                    FFmpeg.SetExecutablesPath("/usr/bin");
                }

                var conversion =
                    FFmpeg.Conversions.New()
                        .AddParameter($"-i \"{rutaWebM}\"")
                        .AddParameter("-ac 1")
                        .AddParameter("-ar 16000")
                        .AddParameter("-c:a pcm_s16le")
                        .SetOutput(rutaAudio);

                await conversion.Start();

                if (!File.Exists(rutaAudio))
                {
                    throw new Exception("FFmpeg no generó el archivo de audio convertido.");
                }

                return rutaAudio;
            }
            finally
            {
                if (File.Exists(rutaWebM))
                {
                    try { File.Delete(rutaWebM); } catch { }
                }
            }
        }

        public async Task<List<float[]>> GenerarEmbeddingsCandidatos(IFormFile audio)
        {
            string rutaWav = await ConvertirAudioAWavPcm16k(audio);
            try
            {
                return await GenerarEmbeddingsCandidatos(rutaWav);
            }
            finally
            {
                if (File.Exists(rutaWav))
                {
                    try { File.Delete(rutaWav); } catch { }
                }
            }
        }

        public async Task<List<float[]>> GenerarEmbeddingsCandidatos(string rutaAudio)
        {
            if (string.IsNullOrWhiteSpace(rutaAudio) || !File.Exists(rutaAudio))
                throw new FileNotFoundException("No se encontró el archivo de audio WAV.", rutaAudio);

            var cronometro = Stopwatch.StartNew();
            long heapInicio = GC.GetTotalMemory(forceFullCollection: false);
            long ramInicio = Process.GetCurrentProcess().WorkingSet64;

            try
            {
                // ==========================================
                // LEER WAV
                // ==========================================

                int sampleRate;
                int canales;
                short[] muestras;

                using (var reader = new WaveFileReader(rutaAudio))
                {
                    if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
                    {
                        throw new Exception("El audio convertido no está en formato WAV PCM.");
                    }

                    sampleRate = reader.WaveFormat.SampleRate;
                    canales = reader.WaveFormat.Channels;

                    byte[] bytes = new byte[reader.Length];
                    int leidos = reader.Read(bytes, 0, bytes.Length);

                    if (leidos == 0)
                        throw new Exception("El audio está vacío.");

                    muestras = new short[leidos / 2];
                    Buffer.BlockCopy(bytes, 0, muestras, 0, leidos);
                }

                // ==========================================
                // CONVERTIR A MONO NORMALIZADO [-1.0, 1.0]
                // ==========================================

                float[] mono;
                if (canales > 1)
                {
                    int cantidadMuestras = muestras.Length / canales;
                    mono = new float[cantidadMuestras];
                    for (int i = 0; i < cantidadMuestras; i++)
                    {
                        float suma = 0;
                        for (int c = 0; c < canales; c++)
                        {
                            suma += muestras[i * canales + c];
                        }
                        mono[i] = (suma / canales) / 32768f;
                    }
                }
                else
                {
                    mono = new float[muestras.Length];
                    for (int i = 0; i < muestras.Length; i++)
                    {
                        mono[i] = muestras[i] / 32768f;
                    }
                }

                if (sampleRate != 16000)
                {
                    mono = CambiarSampleRate(mono, sampleRate, 16000);
                }

                // ==========================================
                // VALIDACIÓN DE ENERGÍA Y NIVEL DE VOZ
                // ==========================================

                double sumaCuadrados = 0;
                float maxAmplitud = 0;
                for (int i = 0; i < mono.Length; i++)
                {
                    float absVal = Math.Abs(mono[i]);
                    if (absVal > maxAmplitud) maxAmplitud = absVal;
                    sumaCuadrados += mono[i] * mono[i];
                }
                double rms = Math.Sqrt(sumaCuadrados / Math.Max(1, mono.Length));

                if (maxAmplitud < 0.015f || rms < 0.0015)
                {
                    throw new InvalidOperationException("No se detectó nivel de voz suficiente en la grabación. Asegúrate de hablar claramente cerca del micrófono.");
                }

                // ==========================================
                // RECORTE DE SILENCIOS INICIALES Y FINALES
                // (VAD - Voice Activity Detection adaptativo)
                // ==========================================

                float[] monoUtil = RecortarSilencios(mono);

                // ==========================================
                // GENERACIÓN DE EMBEDDINGS CANDIDATOS
                // Permite compatibilidad 100% tanto con registros
                // dinámicos nuevos como con registros anteriores de 201 frames
                // ==========================================

                var listaCandidatos = new List<float[]>();

                // Candidato 1: Voz útil con longitud dinámica completa
                int framesDinamicos = Math.Clamp((monoUtil.Length - 400) / 160 + 1, 50, 450);
                float[] embDinamico = CalcularEmbeddingParaMuestras(monoUtil, framesDinamicos);
                listaCandidatos.Add(embDinamico);

                // Candidato 2: Voz útil alineada a 201 frames (para comparar con registros fijos de 201 frames)
                if (framesDinamicos != 201)
                {
                    float[] emb201Util = CalcularEmbeddingParaMuestras(monoUtil, 201);
                    listaCandidatos.Add(emb201Util);
                }

                // Candidato 3: Audio original sin recortar con 201 frames (patrón legado estricto)
                if (mono.Length >= 32560)
                {
                    float[] embLegado201 = CalcularEmbeddingParaMuestras(mono, 201);
                    listaCandidatos.Add(embLegado201);
                }

                Console.WriteLine($"Embeddings generados correctamente. Candidatos: {listaCandidatos.Count}, Frames dinámicos: {framesDinamicos}");
                return listaCandidatos;
            }
            finally
            {
                // ==========================================
                // MEDICIÓN TÉCNICA SEGURA DE MEMORIA Y TIEMPO
                // ==========================================
                cronometro.Stop();
                long heapFin = GC.GetTotalMemory(forceFullCollection: false);
                long ramFin = Process.GetCurrentProcess().WorkingSet64;

                Console.WriteLine($"[DIAGNÓSTICO MEMORIA ECAPA] Duración: {cronometro.ElapsedMilliseconds} ms | Heap .NET: {heapInicio / 1048576.0:F1} MB -> {heapFin / 1048576.0:F1} MB | RAM Proceso (WorkingSet): {ramInicio / 1048576.0:F1} MB -> {ramFin / 1048576.0:F1} MB");
            }
        }

        private float[] RecortarSilencios(float[] audio)
        {
            const int tamanoVentana = 320; // 20 ms a 16 kHz
            int totalVentanas = audio.Length / tamanoVentana;
            if (totalVentanas <= 10) return audio;

            double[] energias = new double[totalVentanas];
            double energiaMax = 0;

            for (int v = 0; v < totalVentanas; v++)
            {
                double suma = 0;
                int inicio = v * tamanoVentana;
                for (int i = 0; i < tamanoVentana; i++)
                {
                    suma += audio[inicio + i] * audio[inicio + i];
                }
                energias[v] = suma / tamanoVentana;
                if (energias[v] > energiaMax)
                {
                    energiaMax = energias[v];
                }
            }

            if (energiaMax < 1e-5) return audio;

            // Umbral adaptativo: 1.5% de la energía máxima del audio o piso de ruido
            double umbral = Math.Max(energiaMax * 0.015, 0.00004);

            int primerFrame = -1;
            int ultimoFrame = -1;

            for (int v = 0; v < totalVentanas; v++)
            {
                if (energias[v] >= umbral)
                {
                    if (primerFrame == -1) primerFrame = v;
                    ultimoFrame = v;
                }
            }

            if (primerFrame == -1 || ultimoFrame == -1) return audio;

            // Margen de seguridad: 150 ms antes y 200 ms después para no cortar consonantes
            int margenAntes = 150 * 16;  // 2400 muestras
            int margenDespues = 200 * 16; // 3200 muestras

            int inicioMuestra = Math.Max(0, (primerFrame * tamanoVentana) - margenAntes);
            int finMuestra = Math.Min(audio.Length, ((ultimoFrame + 1) * tamanoVentana) + margenDespues);

            int longitudRecortada = finMuestra - inicioMuestra;
            if (longitudRecortada < 8000) // Al menos 0.5 s de audio
            {
                return audio;
            }

            float[] recortado = new float[longitudRecortada];
            Array.Copy(audio, inicioMuestra, recortado, 0, longitudRecortada);
            return recortado;
        }

        private float[] CalcularEmbeddingParaMuestras(float[] muestras, int cantidadFrames)
        {
            float[,] fbank = GenerarFbank(muestras, 16000, 80, cantidadFrames);

            float[] datos = new float[80 * cantidadFrames];
            for (int tiempo = 0; tiempo < cantidadFrames; tiempo++)
            {
                for (int frecuencia = 0; frecuencia < 80; frecuencia++)
                {
                    datos[tiempo * 80 + frecuencia] = fbank[frecuencia, tiempo];
                }
            }

            var tensorFeatures = new DenseTensor<float>(
                datos,
                new int[] { 1, cantidadFrames, 80 }
            );

            var tensorLens = new DenseTensor<float>(
                new float[] { 1.0f },
                new int[] { 1 }
            );

            var entradas = new NamedOnnxValue[]
            {
                NamedOnnxValue.CreateFromTensor("features", tensorFeatures),
                NamedOnnxValue.CreateFromTensor("feature_lens", tensorLens)
            };

            using var resultados = _sesion.Run(entradas);
            var salida = resultados.First(x => x.Name == "embedding").AsTensor<float>();
            float[] embedding = salida.ToArray();

            if (embedding.Length != 192)
            {
                throw new Exception($"El modelo devolvió {embedding.Length} valores. Se esperaban 192.");
            }

            // Normalización L2
            double norma = 0;
            for (int i = 0; i < embedding.Length; i++)
            {
                norma += embedding[i] * embedding[i];
            }
            norma = Math.Sqrt(norma);

            if (norma > 1e-12)
            {
                for (int i = 0; i < embedding.Length; i++)
                {
                    embedding[i] = (float)(embedding[i] / norma);
                }
            }

            return embedding;
        }

        private float[] CambiarSampleRate(
            float[] entrada,
            int sampleRateOriginal,
            int nuevoSampleRate)
        {
            double proporcion =
                (double)nuevoSampleRate /
                sampleRateOriginal;

            int nuevaCantidad =
                (int)(
                    entrada.Length *
                    proporcion
                );

            float[] salida =
                new float[nuevaCantidad];

            for (
                int i = 0;
                i < nuevaCantidad;
                i++
            )
            {
                double posicion =
                    i / proporcion;

                int izquierda =
                    (int)posicion;

                int derecha =
                    Math.Min(
                        izquierda + 1,
                        entrada.Length - 1
                    );

                double fraccion =
                    posicion - izquierda;

                salida[i] =
                    (float)(
                        entrada[izquierda] *
                        (1 - fraccion)
                        +
                        entrada[derecha] *
                        fraccion
                    );
            }

            return salida;
        }

        private float[,] GenerarFbank(
     float[] audio,
     int sampleRate,
     int cantidadFiltros,
     int cantidadFrames)
        {
            // ==========================================
            // CONFIGURACIÓN ECAPA
            // ==========================================

            const int tamanoFrame = 400; // 25 ms a 16 kHz
            const int saltoFrame = 160;  // 10 ms
            const int fftSize = 512;

            float[,] resultado =
                new float[cantidadFiltros, cantidadFrames];

            // ==========================================
            // CREAR BANCO MEL
            // ==========================================

            double frecuenciaMinima = 20.0;
            double frecuenciaMaxima = sampleRate / 2.0;

            double melMin =
                HertzAMel(frecuenciaMinima);

            double melMax =
                HertzAMel(frecuenciaMaxima);

            double[] puntosMel =
                new double[cantidadFiltros + 2];

            int[] bins =
                new int[cantidadFiltros + 2];

            for (int i = 0;
                 i < puntosMel.Length;
                 i++)
            {
                puntosMel[i] =
                    melMin +
                    (
                        (melMax - melMin) *
                        i /
                        (cantidadFiltros + 1)
                    );

                double hz =
                    MelAHertz(
                        puntosMel[i]
                    );

                bins[i] =
                    (int)Math.Floor(
                        (fftSize + 1) *
                        hz /
                        sampleRate
                    );

                bins[i] =
                    Math.Clamp(
                        bins[i],
                        0,
                        fftSize / 2
                    );
            }

            // ==========================================
            // RECORRER FRAMES
            // ==========================================

            for (int frame = 0;
                 frame < cantidadFrames;
                 frame++)
            {
                int inicio =
                    frame * saltoFrame;

                Complex[] fft =
                    new Complex[fftSize];

                // ==========================================
                // VENTANA HAMMING
                // ==========================================

                for (int i = 0;
                     i < tamanoFrame;
                     i++)
                {
                    float muestra = 0f;

                    int indiceAudio =
                        inicio + i;

                    if (indiceAudio >= 0 &&
                        indiceAudio < audio.Length)
                    {
                        muestra =
                            audio[indiceAudio];
                    }

                    // Hamming
                    double ventana =
                        0.54 -
                        0.46 *
                        Math.Cos(
                            2.0 *
                            Math.PI *
                            i /
                            (tamanoFrame - 1)
                        );

                    fft[i] =
                        new Complex(
                            muestra * ventana,
                            0
                        );
                }

                // Resto ya queda como cero:
                // zero-padding hasta 512.

                // ==========================================
                // FFT
                // ==========================================

                EjecutarFFT(fft);

                // ==========================================
                // ESPECTRO DE POTENCIA
                // ==========================================

                double[] potencia =
                    new double[
                        fftSize / 2 + 1
                    ];

                for (int k = 0;
                     k < potencia.Length;
                     k++)
                {
                    double real =
                        fft[k].Real;

                    double imaginario =
                        fft[k].Imaginary;

                    potencia[k] =
                        (
                            real * real +
                            imaginario * imaginario
                        ) /
                        fftSize;
                }

                // ==========================================
                // FILTROS MEL
                // ==========================================

                for (int filtro = 0;
                     filtro < cantidadFiltros;
                     filtro++)
                {
                    int izquierda =
                        bins[filtro];

                    int centro =
                        bins[filtro + 1];

                    int derecha =
                        bins[filtro + 2];

                    double energiaMel = 0;

                    // Parte ascendente
                    if (centro > izquierda)
                    {
                        for (int k = izquierda;
                             k < centro;
                             k++)
                        {
                            double peso =
                                (double)(
                                    k - izquierda
                                ) /
                                (
                                    centro - izquierda
                                );

                            energiaMel +=
                                potencia[k] *
                                peso;
                        }
                    }

                    // Parte descendente
                    if (derecha > centro)
                    {
                        for (int k = centro;
                             k < derecha;
                             k++)
                        {
                            double peso =
                                (double)(
                                    derecha - k
                                ) /
                                (
                                    derecha - centro
                                );

                            energiaMel +=
                                potencia[k] *
                                peso;
                        }
                    }

                    // ==========================================
                    // LOG MEL
                    // ==========================================

                    resultado[
                        filtro,
                        frame
                    ] =
                        (float)Math.Log(
                            Math.Max(
                                energiaMel,
                                1e-10
                            )
                        );
                }
            }

            // ==========================================
            // NORMALIZACIÓN POR FRECUENCIA
            // Cepstral Mean Normalization simplificada
            // ==========================================

            for (int filtro = 0;
                 filtro < cantidadFiltros;
                 filtro++)
            {
                double media = 0;

                for (int frame = 0;
                     frame < cantidadFrames;
                     frame++)
                {
                    media +=
                        resultado[
                            filtro,
                            frame
                        ];
                }

                media /=
                    cantidadFrames;

                for (int frame = 0;
                     frame < cantidadFrames;
                     frame++)
                {
                    resultado[
                        filtro,
                        frame
                    ] -=
                        (float)media;
                }
            }

            return resultado;
        }
        private static double HertzAMel(
    double hz)
        {
            return 2595.0 *
                Math.Log10(
                    1.0 +
                    hz / 700.0
                );
        }


        private static double MelAHertz(
            double mel)
        {
            return 700.0 *
                (
                    Math.Pow(
                        10.0,
                        mel / 2595.0
                    ) -
                    1.0
                );
        }


        private static void EjecutarFFT(
            Complex[] buffer)
        {
            int n =
                buffer.Length;

            // ==========================================
            // BIT REVERSAL
            // ==========================================

            int j = 0;

            for (int i = 1;
                 i < n;
                 i++)
            {
                int bit =
                    n >> 1;

                while (
                    (j & bit) != 0)
                {
                    j ^= bit;
                    bit >>= 1;
                }

                j ^= bit;

                if (i < j)
                {
                    Complex temporal =
                        buffer[i];

                    buffer[i] =
                        buffer[j];

                    buffer[j] =
                        temporal;
                }
            }

            // ==========================================
            // FFT COOLEY-TUKEY
            // ==========================================

            for (int longitud = 2;
                 longitud <= n;
                 longitud <<= 1)
            {
                double angulo =
                    -2.0 *
                    Math.PI /
                    longitud;

                Complex wLongitud =
                    new Complex(
                        Math.Cos(angulo),
                        Math.Sin(angulo)
                    );

                for (int i = 0;
                     i < n;
                     i += longitud)
                {
                    Complex w =
                        Complex.One;

                    int mitad =
                        longitud / 2;

                    for (int k = 0;
                         k < mitad;
                         k++)
                    {
                        Complex u =
                            buffer[
                                i + k
                            ];

                        Complex v =
                            buffer[
                                i + k + mitad
                            ] * w;

                        buffer[
                            i + k
                        ] =
                            u + v;

                        buffer[
                            i + k + mitad
                        ] =
                            u - v;

                        w *=
                            wLongitud;
                    }
                }
            }
        }
    }
}
