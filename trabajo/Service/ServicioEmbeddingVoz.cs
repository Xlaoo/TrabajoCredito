using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.Wave;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using System.Numerics;
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
            if (audio == null || audio.Length == 0)
                throw new Exception("No se recibió ningún audio.");

            string carpetaTemporal = Path.Combine(
                Path.GetTempPath(),
                "CrediPlusVoz"
            );

            Directory.CreateDirectory(carpetaTemporal);

            string nombreTemporal = Guid.NewGuid().ToString();

            string rutaWebM = Path.Combine(
                carpetaTemporal,
                nombreTemporal + ".webm"
            );

            string rutaAudio = Path.Combine(
                carpetaTemporal,
                nombreTemporal + ".wav"
            );

            try
            {
                // ==========================================
                // GUARDAR AUDIO WEBM
                // ==========================================

                using (var archivo = new FileStream(
                    rutaWebM,
                    FileMode.Create))
                {
                    await audio.CopyToAsync(archivo);
                }

                // ==========================================
                // CONFIGURAR FFmpeg
                // ==========================================

                string carpetaFFmpeg = Path.Combine(
                    carpetaTemporal,
                    "ffmpeg"
                );

                Directory.CreateDirectory(carpetaFFmpeg);

                // Descargar FFmpeg dentro de la carpeta indicada
                await FFmpegDownloader.GetLatestVersion(
                    FFmpegVersion.Official,
                    carpetaFFmpeg
                );

                // Indicar a Xabe dónde están los ejecutables
                FFmpeg.SetExecutablesPath(carpetaFFmpeg);

                // ==========================================
                // VERIFICAR QUE FFmpeg EXISTE
                // ==========================================

                string rutaFFmpegExe = Path.Combine(
                    carpetaFFmpeg,
                    "ffmpeg.exe"
                );

                if (!File.Exists(rutaFFmpegExe))
                {
                    throw new FileNotFoundException(
                        "FFmpeg no fue descargado correctamente.",
                        rutaFFmpegExe
                    );
                }

                // ==========================================
                // CONVERTIR WEBM → WAV
                // ==========================================

                var conversion =
                    FFmpeg.Conversions.New()
                        .AddParameter($"-i \"{rutaWebM}\"")
                        .AddParameter("-ac 1")
                        .AddParameter("-ar 16000")
                        .AddParameter("-c:a pcm_s16le")
                        .SetOutput(rutaAudio);

                await conversion.Start();

                // ==========================================
                // LEER WAV
                // ==========================================

                using var reader =
                    new WaveFileReader(rutaAudio);

                if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
                {
                    throw new Exception(
                        "El audio convertido no está en formato WAV PCM."
                    );
                }

                int sampleRate =
                    reader.WaveFormat.SampleRate;

                int canales =
                    reader.WaveFormat.Channels;

                byte[] bytes =
                    new byte[reader.Length];

                int leidos =
                    reader.Read(
                        bytes,
                        0,
                        bytes.Length
                    );

                if (leidos == 0)
                    throw new Exception(
                        "El audio está vacío."
                    );

                short[] muestras =
                    new short[leidos / 2];

                Buffer.BlockCopy(
                    bytes,
                    0,
                    muestras,
                    0,
                    leidos
                );

                // ==========================================
                // CONVERTIR A MONO
                // ==========================================

                float[] mono;

                if (canales > 1)
                {
                    int cantidadMuestras =
                        muestras.Length / canales;

                    mono =
                        new float[cantidadMuestras];

                    for (
                        int i = 0;
                        i < cantidadMuestras;
                        i++
                    )
                    {
                        float suma = 0;

                        for (
                            int c = 0;
                            c < canales;
                            c++
                        )
                        {
                            suma +=
                                muestras[
                                    i * canales + c
                                ];
                        }

                        mono[i] =
                            (suma / canales) /
                            32768f;
                    }
                }
                else
                {
                    mono =
                        new float[muestras.Length];

                    for (
                        int i = 0;
                        i < muestras.Length;
                        i++
                    )
                    {
                        mono[i] =
                            muestras[i] /
                            32768f;
                    }
                }

                // ==========================================
                // SAMPLE RATE 16 kHz
                // ==========================================

                if (sampleRate != 16000)
                {
                    mono =
                        CambiarSampleRate(
                            mono,
                            sampleRate,
                            16000
                        );
                }

                // ==========================================
                // GENERAR FBANK
                // ==========================================

                float[,] fbank =
                    GenerarFbank(
                        mono,
                        16000,
                        80,
                        201
                    );

                float[] datos =
                    new float[80 * 201];

                for (
                    int tiempo = 0;
                    tiempo < 201;
                    tiempo++
                )
                {
                    for (
                        int frecuencia = 0;
                        frecuencia < 80;
                        frecuencia++
                    )
                    {
                        datos[
                            tiempo * 80 + frecuencia
                        ] =
                            fbank[
                                frecuencia,
                                tiempo
                            ];
                    }
                }

                // ==========================================
                // TENSOR
                // ==========================================

                var tensorFeatures =
                    new DenseTensor<float>(
                        datos,
                        new int[]
                        {
                            1,
                            201,
                            80
                        }
                    );

                var tensorLens =
                    new DenseTensor<float>(
                        new float[]
                        {
                            201
                        },
                        new int[]
                        {
                            1
                        }
                    );

                var entradas =
                    new NamedOnnxValue[]
                    {
                        NamedOnnxValue.CreateFromTensor(
                            "features",
                            tensorFeatures
                        ),

                        NamedOnnxValue.CreateFromTensor(
                            "feature_lens",
                            tensorLens
                        )
                    };

                // ==========================================
                // EJECUTAR ECAPA
                // ==========================================

                using IDisposableReadOnlyCollection
                    <DisposableNamedOnnxValue> resultados =
                    _sesion.Run(entradas);

                var salida =
                    resultados
                        .First(x => x.Name == "embedding")
                        .AsTensor<float>();

                float[] embedding =
    salida.ToArray();

                if (embedding.Length != 192)
                {
                    throw new Exception(
                        $"El modelo devolvió {embedding.Length} valores. Se esperaban 192."
                    );
                }


                // ==========================================
                // NORMALIZACIÓN L2 DEL EMBEDDING
                // ==========================================

                double norma = 0;

                for (int i = 0;
                     i < embedding.Length;
                     i++)
                {
                    norma +=
                        embedding[i] *
                        embedding[i];
                }

                norma =
                    Math.Sqrt(norma);

                if (norma <= 1e-12)
                {
                    throw new Exception(
                        "El modelo generó un embedding inválido."
                    );
                }

                for (int i = 0;
                     i < embedding.Length;
                     i++)
                {
                    embedding[i] =
                        (float)(
                            embedding[i] /
                            norma
                        );
                }


                // ==========================================
                // DEBUG
                // ==========================================

                Console.WriteLine(
                    "Embedding generado correctamente."
                );

                Console.WriteLine(
                    $"Tamaño embedding: {embedding.Length}"
                );

                Console.WriteLine(
                    $"Primeros valores: " +
                    $"{embedding[0]:F4}, " +
                    $"{embedding[1]:F4}, " +
                    $"{embedding[2]:F4}, " +
                    $"{embedding[3]:F4}, " +
                    $"{embedding[4]:F4}"
                );

                return embedding;
            }
            finally
            {
                // ==========================================
                // ELIMINAR ARCHIVOS TEMPORALES
                // ==========================================

                if (File.Exists(rutaAudio))
                {
                    File.Delete(rutaAudio);
                }

                if (File.Exists(rutaWebM))
                {
                    File.Delete(rutaWebM);
                }
            }
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
