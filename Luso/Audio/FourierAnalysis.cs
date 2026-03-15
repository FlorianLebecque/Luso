using MathNet.Numerics.IntegralTransforms;
using System;
using System.Numerics;



namespace Luso.Audio
{
    public class FourierAnalysis{

        protected double lowLevel;
        protected double midLevel;
        protected double highLevel;

        public static double sensitivity = 1;
        public const double reverseSensitivity = 101;
        
        protected void GetVolume(Complex[] buffer,int sampleRate){


            // Perform FFT
            //buffer = ForwardNormalized(buffer);
            Fourier.Forward(buffer);

            double[] frequencies = Fourier.FrequencyScale(buffer.Length, sampleRate);

            int low_end = GetIndexOfFrequency(frequencies, 200);
            int mid_end = GetIndexOfFrequency(frequencies, 2000);

            int high_end = GetIndexOfFrequency(frequencies, 8000);

            if(high_end == -1){
                high_end = GetLastPositiveFrequency(frequencies);
            }

            lowLevel  = ComputeRangeFrequencyLevel(buffer,frequencies, 0, low_end) * sensitivity;
            midLevel  = ComputeRangeFrequencyLevel(buffer,frequencies, low_end+1, mid_end) * sensitivity;
            highLevel = ComputeRangeFrequencyLevel(buffer, frequencies, mid_end + 1, high_end) * sensitivity;
        }

        private int GetIndexOfFrequency(double[] frequencies,double f) {

            for(int i = 0; i< frequencies.Length; i++){

                if(frequencies[i] >= f){
                    return i;
                }
            }

            return -1;
        }

        private int GetLastPositiveFrequency(double[] frequencies){

            int lastPositiveIndex = 0;
            for(int i = 0; i< frequencies.Length; i++)
            {
                if (frequencies[i] < 0) {
                    break;
                }
                lastPositiveIndex = i;

            }

            return lastPositiveIndex;
        }

        private double ComputeRangeFrequencyLevel(Complex[] fft, double[] frequencies ,int startFrenquencyIndex, int endFrequencyIndex){

            // Calculate the low volume level
            double lowVolumeLevel = 0.0;
            for (int i = startFrenquencyIndex; i <= endFrequencyIndex; i++){
                lowVolumeLevel += fft[i].Magnitude;
            }

            // Normalize the low volume level to a range between 0 and 1
            lowVolumeLevel /= (endFrequencyIndex - startFrenquencyIndex );

            return lowVolumeLevel;
        }


        private Complex[] ForwardNormalized(Complex[] buffer){

            Complex[] x = Forward(buffer);         

            return x;
        }

        private Complex[] Forward(Complex[] x){
            int N = x.Length;

            if (N <= 1)
            {
                return x; // Base case: if the input size is 0 or 1, return the input itself
            }

            Complex[] X = new Complex[N];

            // Divide the input into even and odd parts
            Complex[] xEven = new Complex[N / 2];
            Complex[] xOdd = new Complex[N / 2];
            for (int i = 0; i < N / 2; i++)
            {
                xEven[i] = x[2 * i];
                xOdd[i] = x[2 * i + 1];
            }

            // Recursive FFT on even and odd parts
            Complex[] XEven = Forward(xEven);
            Complex[] XOdd = Forward(xOdd);

            // Combine the results
            for (int k = 0; k < N / 2; k++)
            {
                Complex t = Complex.FromPolarCoordinates(1, -2 * Math.PI * k / N) * XOdd[k];
                X[k] = XEven[k] + t;
                X[k + N / 2] = XEven[k] - t;
            }

            return X;
        }
    }
}
