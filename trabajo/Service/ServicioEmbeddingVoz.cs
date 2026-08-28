using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.Wave;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

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
            int tamanoFrame = 400;
            int saltoFrame = 160;

            float[,] resultado =
                new float[
                    cantidadFiltros,
                    cantidadFrames
                ];

            for (
                int frame = 0;
                frame < cantidadFrames;
                frame++
            )
            {
                int inicio =
                    frame * saltoFrame;

                float[] energia =
                    new float[257];

                for (
                    int k = 0;
                    k < 257;
                    k++
                )
                {
                    double frecuencia =
                        (double)k *
                        sampleRate /
                        512.0;

                    int muestra =
                        inicio +
                        Math.Min(
                            k,
                            tamanoFrame - 1
                        );

                    if (muestra >= audio.Length)
                        muestra =
                            audio.Length - 1;

                    if (muestra >= 0)
                    {
                        float valor =
                            audio[muestra];

                        energia[k] =
                            valor * valor;
                    }
                }

                for (
                    int filtro = 0;
                    filtro < cantidadFiltros;
                    filtro++
                )
                {
                    int inicioFiltro =
                        filtro * 3;

                    float suma = 0;

                    for (
                        int k = 0;
                        k < 257;
                        k++
                    )
                    {
                        int distancia =
                            Math.Abs(
                                k -
                                inicioFiltro
                            );

                        float peso =
                            Math.Max(
                                0,
                                1f -
                                distancia / 10f
                            );

                        suma +=
                            energia[k] *
                            peso;
                    }

                    resultado[
                        filtro,
                        frame
                    ] =
                        (float)Math.Log(
                            suma + 1e-10
                        );
                }
            }

            return resultado;
        }
    }
}
