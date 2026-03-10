using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

namespace VoiceInkWin.Services;

public class AudioAnalysisService
{
    private const int BandCount = 24;
    private const int FftSize = 1024;
    private const float NoiseFloor = 0.02f; // Minimum magnitude to register as non-silence

    public float CurrentRms { get; private set; }
    private float[] _frequencyBands = new float[BandCount];
    public float[] FrequencyBands => _frequencyBands;

    public void Analyze(float[] samples)
    {
        if (samples.Length == 0) return;

        // RMS
        double sum = 0;
        foreach (var s in samples)
            sum += s * s;
        CurrentRms = (float)Math.Sqrt(sum / samples.Length);

        // Skip FFT on silence — saves CPU when not speaking
        if (CurrentRms < 0.001f)
        {
            Interlocked.Exchange(ref _frequencyBands, new float[BandCount]);
            return;
        }

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

        // Get magnitudes (skip DC bin 0)
        int halfSize = FftSize / 2;
        var magnitudes = new float[halfSize];
        for (int i = 0; i < halfSize; i++)
            magnitudes[i] = (float)complex[i].Magnitude;

        // Split into bands using logarithmic frequency spacing
        // Use log-spaced boundaries from bin 2 to halfSize (skip DC and near-DC)
        var bands = new float[BandCount];
        double minFreqBin = 2;
        double maxFreqBin = halfSize;
        double logMin = Math.Log(minFreqBin);
        double logMax = Math.Log(maxFreqBin);

        for (int b = 0; b < BandCount; b++)
        {
            int start = (int)Math.Exp(logMin + (logMax - logMin) * b / BandCount);
            int end = (int)Math.Exp(logMin + (logMax - logMin) * (b + 1) / BandCount);
            start = Math.Clamp(start, 0, halfSize - 1);
            end = Math.Clamp(end, start + 1, halfSize);

            float bandMax = 0;
            for (int i = start; i < end; i++)
            {
                if (magnitudes[i] > bandMax)
                    bandMax = magnitudes[i];
            }
            bands[b] = bandMax;
        }

        // Apply noise floor — anything below threshold is zero
        float maxBand = 0;
        for (int i = 0; i < BandCount; i++)
        {
            if (bands[i] < NoiseFloor)
                bands[i] = 0;
            if (bands[i] > maxBand)
                maxBand = bands[i];
        }

        // Normalize to 0-1 range, only if there's real signal above noise floor
        if (maxBand > NoiseFloor)
        {
            for (int i = 0; i < BandCount; i++)
                bands[i] /= maxBand;
        }
        else
        {
            Array.Clear(bands);
        }

        Interlocked.Exchange(ref _frequencyBands, bands);
    }
}
