using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

namespace VoiceInkWin.Services;

public class AudioAnalysisService
{
    private const int BandCount = 12;
    private const int FftSize = 1024;

    public float CurrentRms { get; private set; }
    public float[] FrequencyBands { get; private set; } = new float[BandCount];

    public void Analyze(float[] samples)
    {
        if (samples.Length == 0) return;

        // RMS
        double sum = 0;
        foreach (var s in samples)
            sum += s * s;
        CurrentRms = (float)Math.Sqrt(sum / samples.Length);

        // FFT for frequency bands
        int fftLength = Math.Min(samples.Length, FftSize);
        var complex = new Complex[FftSize];
        for (int i = 0; i < FftSize; i++)
        {
            float val = i < fftLength ? samples[samples.Length - fftLength + i] : 0;
            // Apply Hann window
            float window = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftSize - 1))));
            complex[i] = new Complex(val * window, 0);
        }

        Fourier.Forward(complex, FourierOptions.Matlab);

        // Split into 12 bands (logarithmic spacing)
        int halfSize = FftSize / 2;
        var magnitudes = new float[halfSize];
        for (int i = 0; i < halfSize; i++)
            magnitudes[i] = (float)complex[i].Magnitude;

        // Logarithmic band boundaries
        var bands = new float[BandCount];
        for (int b = 0; b < BandCount; b++)
        {
            int start = (int)(halfSize * Math.Pow(b / (double)BandCount, 2));
            int end = (int)(halfSize * Math.Pow((b + 1) / (double)BandCount, 2));
            start = Math.Max(start, 0);
            end = Math.Max(end, start + 1);
            end = Math.Min(end, halfSize);

            float bandSum = 0;
            int count = 0;
            for (int i = start; i < end; i++)
            {
                bandSum += magnitudes[i];
                count++;
            }
            bands[b] = count > 0 ? bandSum / count : 0;
        }

        // Normalize to 0-1 range
        float maxBand = bands.Max();
        if (maxBand > 0.001f)
        {
            for (int i = 0; i < BandCount; i++)
                bands[i] /= maxBand;
        }

        FrequencyBands = bands;
    }
}
