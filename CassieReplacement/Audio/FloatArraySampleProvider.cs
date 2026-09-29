namespace CassieReplacement.Audio
{
    using System;
    using NAudio.Wave;

    /// <summary>Jednorazowy provider nad współdzielonym (tylko do odczytu) buforem próbek z cache.</summary>
    internal sealed class FloatArraySampleProvider : ISampleProvider
    {
        private readonly float[] samples;
        private int position;

        public FloatArraySampleProvider(float[] samples, int sampleRate, int channels)
        {
            this.samples = samples ?? Array.Empty<float>();
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int toCopy = Math.Min(count, samples.Length - position);
            if (toCopy <= 0)
            {
                return 0;
            }

            Array.Copy(samples, position, buffer, offset, toCopy);
            position += toCopy;
            return toCopy;
        }
    }
}
