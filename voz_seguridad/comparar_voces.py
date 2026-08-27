import sherpa_onnx
import soundfile as sf
import numpy as np

MODELO = "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx"


def cargar_extractor():
    config = sherpa_onnx.SpeakerEmbeddingExtractorConfig(
        model=MODELO,
        num_threads=1,
        debug=False,
        provider="cpu"
    )

    if not config.validate():
        raise RuntimeError("La configuración del modelo no es válida.")

    return sherpa_onnx.SpeakerEmbeddingExtractor(config)


def obtener_embedding(extractor, archivo):
    audio, sample_rate = sf.read(
        archivo,
        dtype="float32",
        always_2d=True
    )

    # Usamos solamente el primer canal
    audio = audio[:, 0]

    stream = extractor.create_stream()

    stream.accept_waveform(
        sample_rate=sample_rate,
        waveform=audio
    )

    stream.input_finished()

    if not extractor.is_ready(stream):
        raise RuntimeError(
            f"El audio {archivo} es demasiado corto."
        )

    embedding = extractor.compute(stream)

    return np.array(embedding, dtype=np.float32)


def similitud_coseno(a, b):
    return float(
        np.dot(a, b)
        / (np.linalg.norm(a) * np.linalg.norm(b))
    )


print("====================================")
print(" COMPARACIÓN DE VOCES")
print("====================================")
print()

print("Cargando modelo...")

extractor = cargar_extractor()

print("Modelo cargado correctamente.")
print()

print("Procesando voz_prueba.wav...")
voz1 = obtener_embedding(
    extractor,
    "voz_prueba.wav"
)

print("Procesando voz_misma.wav...")
voz2 = obtener_embedding(
    extractor,
    "voz_misma.wav"
)

similitud = similitud_coseno(voz1, voz2)

print()
print("====================================")
print(" RESULTADO")
print("====================================")
print(f"Embedding voz 1: {len(voz1)} valores")
print(f"Embedding voz 2: {len(voz2)} valores")
print(f"Similitud: {similitud:.4f}")
print()

if similitud >= 0.60:
    print("VOZ PARECIDA ✅")
else:
    print("VOZ DIFERENTE ❌")

print("====================================")