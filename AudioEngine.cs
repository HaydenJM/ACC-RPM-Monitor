using NAudio.Wave;

namespace ACCRPMMonitor;

/// <summary>
/// Audio engine for RPM shift feedback using triangle wave tones
/// </summary>
public class AudioEngine : IDisposable
{
    private readonly WaveOutEvent _waveOut;
    private readonly TriangleWaveProvider _waveProvider;
    private bool _isPlaying;

    public AudioEngine()
    {
        _waveProvider = new TriangleWaveProvider();
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_waveProvider);
    }

    /// <summary>
    /// Updates audio based on current RPM, threshold, and gear
    /// </summary>
    /// <param name="currentRPM">Current RPM value</param>
    /// <param name="threshold">RPM threshold for shifting</param>
    /// <param name="currentGear">Current gear (1-8)</param>
    public void UpdateRPM(int currentRPM, int threshold, int currentGear)
    {
        int rpmFromThreshold = currentRPM - threshold;

        // Calculate base frequency based on gear
        // Gears 1-2: 500-600 Hz
        // Gear 3+: increases by 100 Hz per gear
        float baseFreq = currentGear <= 2 ? 500f : 500f + (currentGear - 2) * 100f;
        float maxFreq = baseFreq + 100f;

        // Start rising tone at 300 RPM below threshold
        if (rpmFromThreshold >= -300 && rpmFromThreshold < -100)
        {
            // Map RPM range [-300, -100] to frequency range [baseFreq, maxFreq] Hz
            // Formula: baseFreq + ((currentRPM - (threshold - 300)) / 2)
            float frequency = baseFreq + ((currentRPM - (threshold - 300)) / 2f);

            _waveProvider.SetFrequency(frequency);
            _waveProvider.SetBeeping(false);

            if (!_isPlaying)
            {
                _waveOut.Play();
                _isPlaying = true;
            }
        }
        // Start beeping at 100 RPM below threshold
        else if (rpmFromThreshold >= -100)
        {
            _waveProvider.SetFrequency(maxFreq);
            _waveProvider.SetBeeping(true);

            if (!_isPlaying)
            {
                _waveOut.Play();
                _isPlaying = true;
            }
        }
        // Below trigger range - stop audio
        else
        {
            if (_isPlaying)
            {
                _waveOut.Stop();
                _isPlaying = false;
            }
        }
    }

    /// <summary>
    /// Stops all audio playback
    /// </summary>
    public void Stop()
    {
        if (_isPlaying)
        {
            _waveOut.Stop();
            _isPlaying = false;
        }
    }

    public void Dispose()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
    }
}

/// <summary>
/// Triangle wave generator for audio feedback
/// </summary>
internal class TriangleWaveProvider : ISampleProvider
{
    private float _frequency;
    private float _phase;
    private bool _isBeeping;
    private int _samplesSinceBeepToggle;
    private bool _beepOn = true;
    private const int BeepOnSamples = 4410; // ~100ms at 44.1kHz
    private const int BeepOffSamples = 4410; // ~100ms at 44.1kHz
    private const float Amplitude = 0.15f; // Keep volume reasonable

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

    public void SetFrequency(float frequency)
    {
        _frequency = frequency;
    }

    public void SetBeeping(bool isBeeping)
    {
        if (_isBeeping != isBeeping)
        {
            _isBeeping = isBeeping;
            _samplesSinceBeepToggle = 0;
            _beepOn = true;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float sample = 0f;

            // Handle beeping mode
            if (_isBeeping)
            {
                _samplesSinceBeepToggle++;

                if (_beepOn && _samplesSinceBeepToggle >= BeepOnSamples)
                {
                    _beepOn = false;
                    _samplesSinceBeepToggle = 0;
                }
                else if (!_beepOn && _samplesSinceBeepToggle >= BeepOffSamples)
                {
                    _beepOn = true;
                    _samplesSinceBeepToggle = 0;
                }

                if (!_beepOn)
                {
                    buffer[offset + i] = 0f;
                    continue;
                }
            }

            // Generate triangle wave
            // Triangle wave: rises from -1 to 1, then falls from 1 to -1
            float phaseValue = _phase % 1.0f;

            if (phaseValue < 0.5f)
            {
                // Rising: 0 to 0.5 maps to -1 to 1
                sample = (phaseValue * 4f - 1f) * Amplitude;
            }
            else
            {
                // Falling: 0.5 to 1 maps to 1 to -1
                sample = (3f - phaseValue * 4f) * Amplitude;
            }

            buffer[offset + i] = sample;

            // Increment phase
            _phase += _frequency / WaveFormat.SampleRate;
            if (_phase >= 1.0f)
                _phase -= 1.0f;
        }

        return count;
    }
}
