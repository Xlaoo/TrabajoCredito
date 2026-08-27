using System;
using System.IO;
using NAudio.Wave;
using Vosk;

namespace trabajo.Service
{
    public class ServicioVoz
    {
        private readonly Model modelo;

        public ServicioVoz()
        {
            string rutaModelo = Path.Combine(
                AppContext.BaseDirectory,
                "ModelosVoz",
                "vosk-model-small-es-0.42"
            );

            modelo = new Model(rutaModelo);
        }

        public string ReconocerTexto(byte[] audio)
        {
            using var reconocedor = new VoskRecognizer(modelo, 16000.0f);

            reconocedor.AcceptWaveform(audio, audio.Length);

            return reconocedor.FinalResult();
        }

        public byte[] GrabarAudio(int segundos)
        {
            using var grabador = new WaveInEvent();

            grabador.WaveFormat = new WaveFormat(16000, 1);

            using var memoria = new MemoryStream();

            grabador.DataAvailable += (sender, e) =>
            {
                memoria.Write(e.Buffer, 0, e.BytesRecorded);
            };

            grabador.StartRecording();

            System.Threading.Thread.Sleep(segundos * 1000);

            grabador.StopRecording();

            return memoria.ToArray();
        }
    }
}