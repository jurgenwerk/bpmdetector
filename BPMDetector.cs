using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NAudio.Wave;

namespace RZP
{
    class BPMDetector
    {
        private string filename = null;
        private short[] leftChn;
        private short[] rightChn;
        private double BPM;
        private double sampleRate = 44100;
        private double trackLength = 0;

        public double getBPM()
        {
            return BPM;
        }

        public BPMDetector(string filename)
        {
            this.filename = filename;
            Detect();
        }

        public BPMDetector(short[] leftChn, short[] rightChn)
        {
            this.leftChn = leftChn;
            this.rightChn = rightChn;
            Detect();
        }

        private void Detect()
        {
            if (filename != null)
            {
                using (WaveFileReader reader = new WaveFileReader(filename))
                {
                    byte[] buffer = new byte[reader.Length];
                    int read = reader.Read(buffer, 0, buffer.Length);
                    short[] sampleBuffer = new short[read / 2];
                    Buffer.BlockCopy(buffer, 0, sampleBuffer, 0, read);

                    List<short> chan1 = new List<short>();
                    List<short> chan2 = new List<short>();

                    for (int i = 0; i < sampleBuffer.Length; i += 2)
                    {
                        chan1.Add(sampleBuffer[i]);
                        chan2.Add(sampleBuffer[i + 1]);
                    }

                    leftChn = chan1.ToArray();
                    rightChn = chan2.ToArray();
                }
            }

            trackLength = (float)leftChn.Length / sampleRate;

            // 0.1s window ... 0.1*44100 = 4410 samples, lets adjust this to 3600 
            int sampleStep = 3600;
            
            // calculate energy over windows of size sampleSetep
            List<double> energies = new List<double>();
            for (int i = 0; i < leftChn.Length - sampleStep - 1; i += sampleStep)
            {
                energies.Add(rangeQuadSum(leftChn, i, i + sampleStep));
            }

            int beats = 0;
            double average = 0;
            double sumOfSquaresOfDifferences = 0;
            double variance = 0;
            double newC = 0;
            List<double> variances = new List<double>();

            // how many energies before and after index for local energy average
            int offset = 10;

            for (int i = offset; i <= energies.Count - offset - 1; i++)
            {
                // calculate local energy average
                double currentEnergy = energies[i];
                double qwe = rangeSum(energies.ToArray(), i - offset, i - 1) + currentEnergy + rangeSum(energies.ToArray(), i + 1, i + offset);
                qwe /= offset * 2 + 1;

                // calculate energy variance of nearby energies
                List<double> nearbyEnergies = energies.Skip(i - 5).Take(5).Concat(energies.Skip(i + 1).Take(5)).ToList<double>();
                average = nearbyEnergies.Average();
                sumOfSquaresOfDifferences = nearbyEnergies.Select(val => (val - average) * (val - average)).Sum();
                variance = (sumOfSquaresOfDifferences / nearbyEnergies.Count) / Math.Pow(10, 22);

                // experimental linear regression - constant calculated according to local energy variance
                newC = variance * 0.009 + 1.385;
                if (currentEnergy > newC * qwe)
                    beats++;
            }

            BPM = beats / (trackLength / 60);

        }

        private static double rangeQuadSum(short[] samples, int start, int stop)
        {
            double tmp = 0;
            for (int i = start; i <= stop; i++)
            {
                tmp += Math.Pow(samples[i], 2);
            }

            return tmp;
        }

        private static double rangeSum(double[] data, int start, int stop)
        {
            double tmp = 0;
            for (int i = start; i <= stop; i++)
            {
                tmp += data[i];
            }

            return tmp;
        }
    }
}
