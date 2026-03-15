using Android.Bluetooth;
using Android.Media;
using Android.Media.Audiofx;
using Android.Media.Metrics;
using Luso.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Luso.Audio
{
    public class AudioAnalyser : FourierAnalysis,IAudioAnalyser {

        private bool micOk = false;

        private const int SampleRate = 48000;
        private const ChannelIn ChannelConfig = ChannelIn.Mono;
        private const Android.Media.Encoding AudioFormat = Android.Media.Encoding.Pcm16bit;
        private readonly int BufferSize = AudioRecord.GetMinBufferSize(SampleRate, ChannelConfig, AudioFormat);

        private AudioRecord audioRecord;
        private bool isRecording;
        public event EventHandler<double> AudioLevelChanged;


        private readonly Thread th;

        public AudioAnalyser() {

            th = new Thread(RecordThread);

        }


        public async void Init() {
            PermissionStatus micPermision = await Permissions.CheckStatusAsync<Permissions.Microphone>();

            if (micPermision != PermissionStatus.Granted) {
                micPermision = await Permissions.RequestAsync<Permissions.Microphone>();
            }

            if (micPermision != PermissionStatus.Granted) {
                micOk = false;
                return;
            }

            audioRecord = new AudioRecord(AudioSource.Unprocessed, SampleRate, ChannelConfig, AudioFormat, BufferSize);

            if (NoiseSuppressor.IsAvailable){
                NoiseSuppressor.Create(audioRecord.AudioSessionId);
            }

            micOk = true;

            th.Start();
        }

        private void RecordThread() {
            if (audioRecord.State == Android.Media.State.Initialized) {
                isRecording = true;
                audioRecord.StartRecording();

                byte[] buffer = new byte[BufferSize];

                while (isRecording && micOk) {
                    audioRecord.Read(buffer, 0, BufferSize);

                    var complexBuffer = new Complex[buffer.Length / 2];

                    for (int i = 0; i < complexBuffer.Length; i++){
                        double v = BitConverter.ToInt16(buffer, i * 2);
                        complexBuffer[i] = new Complex( v/1000, 0.0);
                    }

                    GetVolume(complexBuffer, SampleRate);
                }
            }
        }

        public double GetHighLevel(){
            return highLevel;
        }

        public double GetMidLevel(){
            return midLevel;
        }

        public double GetLowLevel(){
            return lowLevel;
        }

    }
}
