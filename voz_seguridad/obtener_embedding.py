import sherpa_onnx
import soundfile as sf
import numpy as np

MODELO = "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx"
AUDIO = "voz_prueba.wav"

print("Cargando modelo de voz...")

config = sherpa_onnx.SpeakerEmbeddingExtractorConfig(
    model=MODELO,
    num_threads=2,
    debug=True,
    provider="cpu",
)

if not config.validate():
    raise RuntimeError("La configuración del modelo no es válida.")

extractor = sherpa_onnx.SpeakerEmbeddingExtractor(config)

print("Modelo cargado correctamente.")

audio, sample_rate = sf.read(
    AUDIO,
    dtype="float32",
    always_2d=True
)

audio = audio[:, 0]
audio = np.ascontiguousarray(audio)

print(f"Audio: {AUDIO}")
print(f"Frecuencia: {sample_rate} Hz")
print(f"Muestras: {len(audio)}")

stream = extractor.create_stream()

stream.accept_waveform(
    sample_rate=sample_rate,
    waveform=audio
)

stream.input_finished()

if not extractor.is_ready(stream):
    raise RuntimeError(
        "El audio es demasiado corto para obtener el embedding."
    )

embedding = extractor.compute(stream)
embedding = np.array(embedding, dtype=np.float32)

print()
print("====================================")
print(" EMBEDDING OBTENIDO CORRECTAMENTE")
print("====================================")
print(f"Dimensión: {len(embedding)}")
print(f"Primeros valores: {embedding[:10]}")
print()
print("La prueba de extracción de voz FUNCIONA.")