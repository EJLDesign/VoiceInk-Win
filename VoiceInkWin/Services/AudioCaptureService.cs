using NAudio.Wave;
using static VoiceInkWin.App;

namespace VoiceInkWin.Services;

public class AudioCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;
    private readonly List<float> _audioBuffer = new();
    private readonly object _bufferLock = new();
    private bool _isRecording;
    private DateTime _lastSoundTime;
    private float _silenceThreshold = 0.003f;
    private float _maxDuration = 120f;
    private CancellationTokenSource? _cts;

    public event Action<float[]>? AudioDataAvailable; // raw samples for visualization
    public event Action? SilenceTimeout;
    public event Action? MaxDurationReached;

    public bool IsRecording => _isRecording;
    public bool SilenceDetectionEnabled { get; set; } = true;

    public void Configure(string? deviceId, float silenceThreshold, float maxDuration)
    {
        _silenceThreshold = silenceThreshold;
        _maxDuration = maxDuration;
    }

    public void StartRecording(string? deviceId = null)
    {
        if (_isRecording) return;

        lock (_bufferLock)
            _audioBuffer.Clear();

        _cts = new CancellationTokenSource();
        _lastSoundTime = DateTime.UtcNow;

        // Find device number
        int deviceNumber = -1; // default
        App.Log($"Looking for audio device: \"{deviceId}\"");
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            App.Log($"  Device[{i}]: \"{caps.ProductName}\"");
        }
        if (!string.IsNullOrEmpty(deviceId))
        {
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                // Use StartsWith for partial match — NAudio truncates names to 31 chars
                if (caps.ProductName == deviceId ||
                    deviceId.StartsWith(caps.ProductName) ||
                    caps.ProductName.StartsWith(deviceId))
                {
                    deviceNumber = i;
                    break;
                }
            }
        }
        App.Log($"Selected device number: {deviceNumber}");

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz mono 16-bit
            BufferMilliseconds = 50
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
        _waveIn.StartRecording();
        _isRecording = true;

        // Start max duration timer
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_maxDuration), _cts.Token);
                if (_isRecording)
                    MaxDurationReached?.Invoke();
            }
            catch (OperationCanceledException) { }
        });
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Convert 16-bit PCM to float samples
        int sampleCount = e.BytesRecorded / 2;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i * 2);
            samples[i] = sample / 32768f;
        }

        lock (_bufferLock)
            _audioBuffer.AddRange(samples);

        AudioDataAvailable?.Invoke(samples);

        // Check silence (disabled in PTT mode)
        if (SilenceDetectionEnabled)
        {
            float rms = CalculateRms(samples);
            if (rms > _silenceThreshold)
            {
                _lastSoundTime = DateTime.UtcNow;
            }
            else if ((DateTime.UtcNow - _lastSoundTime).TotalSeconds > 2.0)
            {
                SilenceTimeout?.Invoke();
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // cleanup handled in StopRecording
    }

    public float[] StopRecording()
    {
        if (!_isRecording) return [];

        _isRecording = false;
        _cts?.Cancel();

        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;

        lock (_bufferLock)
            return _audioBuffer.ToArray();
    }

    private static float CalculateRms(float[] samples)
    {
        if (samples.Length == 0) return 0;
        double sum = 0;
        foreach (var s in samples)
            sum += s * s;
        return (float)Math.Sqrt(sum / samples.Length);
    }

    public static List<(int Index, string Name)> GetInputDevices()
    {
        var devices = new List<(int, string)>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add((i, caps.ProductName));
        }
        return devices;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
    }
}
