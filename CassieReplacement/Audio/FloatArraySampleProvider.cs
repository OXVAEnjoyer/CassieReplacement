namespace CassieReplacement.Audio
{
    using System;
    using NAudio.Wave;

    internal sealed class FloatArraySampleProvider : ISampleProvider
    {
        private readonly float[] samples;
        private int position;

        public FloatArraySampleProvider(float[] samples, int sampleRate = 48000, int channels = 1)
        {
            this.samples = samples ?? Array.Empty<float>();
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int remaining = samples.Length - position;
            if (remaining <= 0)
            {
                return 0;
            }

            int toCopy = Math.Min(count, remaining);
            Array.Copy(samples, position, buffer, offset, toCopy);
            position += toCopy;
            return toCopy;
        }
    }
}
