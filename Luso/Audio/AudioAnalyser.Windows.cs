using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Luso.Audio {
    public class AudioAnalyser : FourierAnalysis, IAudioAnalyser
    {



        public void Init() {
            var deviceEnumerator = new MMDeviceEnumerator();
            var defaultCaptureDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            

            var capture = new WasapiCapture(defaultCaptureDevice);
                        

            capture.DataAvailable += AnalyseSound;

            capture.StartRecording();

        }


        private void AnalyseSound(Object sender, WaveInEventArgs e){
            
            float[] buffer = new float[e.Buffer.Length / 4]; // Assuming 32-bit audio

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = BitConverter.ToSingle(e.Buffer, i * 4);
            }

            // Convert the float array to Complex
            var complexBuffer = new Complex[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                complexBuffer[i] = new Complex(buffer[i], 0.0);
            }



            GetVolume(complexBuffer, ((WasapiCapture)sender).WaveFormat.SampleRate);
        }

        

        public double GetHighLevel() {
            return highLevel;
        }

        public double GetMidLevel() {
            return midLevel;
        }

        public double GetLowLevel() {
            return lowLevel;
        }
    }
}
