using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Audio_Signal_Processing_1
{
    class Equalizer : IWaveProvider
    {
        private IWaveProvider sourceProvider;
        private float cutOffFreq;
        private int channels;
        private BiQuadFilter[] filters;

        public Equalizer(IWaveProvider sourceProvider, int cutOffFreq)
        {
            this.sourceProvider = sourceProvider;
            this.cutOffFreq = cutOffFreq;

            channels = sourceProvider.WaveFormat.Channels;
            filters = new BiQuadFilter[channels];
            CreateFilters();
        }

        private void CreateFilters()
        {
            for (int n = 0; n < channels; n++)
                if (filters[n] == null)
                    filters[n] = BiQuadFilter.LowPassFilter(44100, cutOffFreq, 1);
                else
                    filters[n].SetLowPassFilter(44100, cutOffFreq, 1);
        }

        public WaveFormat WaveFormat { get { return sourceProvider.WaveFormat; } }

        public int Read(byte[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i++)
                buffer[offset + i] = (byte)filters[(i % channels)].Transform(buffer[offset + i]);

            return samplesRead;
        }
    }
}
