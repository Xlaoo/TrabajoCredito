import sounddevice as sd
import soundfile as sf
import time

ARCHIVO = "voz_prueba.wav"
DURACION = 5
FRECUENCIA = 16000

print("====================================")
print("       PRUEBA DE VOZ")
print("====================================")
print()
print("Prepárate...")
time.sleep(2)

print("🎤 HABLA AHORA durante 5 segundos")
audio = sd.rec(
    int(DURACION * FRECUENCIA),
    samplerate=FRECUENCIA,
    channels=1,
    dtype="float32"
)

sd.wait()

sf.write(ARCHIVO, audio, FRECUENCIA)

print()
print("✅ Grabación terminada.")
print(f"Archivo creado: {ARCHIVO}")