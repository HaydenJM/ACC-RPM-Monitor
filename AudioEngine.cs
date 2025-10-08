using NAudio.Wave;

namespace ACCRPMMonitor;

// Handles the audio feedback for RPM shift warnings using triangle waves
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

    // Updates the audio based on RPM, threshold, and current gear
    public void UpdateRPM(int currentRPM, int threshold, int currentGear)
    {
        int rpmFromThreshold = currentRPM - threshold;

        // Each gear gets its own 100Hz frequency range, starting at 500Hz
        float baseFreq = 500f + (currentGear - 1) * 100f;
        float maxFreq = baseFreq + 100f;

        // Rising tone phase: 300 RPM to 100 RPM below threshold
        if (rpmFromThreshold >= -300 && rpmFromThreshold < -100)
        {
            // Frequency rises smoothly from baseFreq to maxFreq over 200 RPM
            float frequency = baseFreq + ((currentRPM - (threshold - 300)) / 2f);

            _waveProvider.SetFrequency(frequency);
            _waveProvider.SetBeeping(false);

            if (!_isPlaying)
            {
                _waveOut.Play();
                _isPlaying = true;
            }
        }
        // Beeping phase: 100 RPM below threshold and above
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
        // Too far below threshold - stop audio
        else
        {
            if (_isPlaying)
            {
                _waveOut.Stop();
                _isPlaying = false;
            }
        }
    }

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

// Generates triangle wave audio with optional beeping
internal class TriangleWaveProvider : ISampleProvider
{
    private float _frequency;
    private float _phase;
    private bool _isBeeping;
    private int _samplesSinceBeepToggle;
    private bool _beepOn = true;
    private const int BeepOnSamples = 4410; // ~100ms at 44.1kHz
    private const int BeepOffSamples = 4410;
    private const float Amplitude = 0.15f;

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

            // Handle beeping mode (100ms on/off pattern)
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

            // Generate triangle wave sample
            float phaseValue = _phase % 1.0f;

            if (phaseValue < 0.5f)
            {
                // Rising edge: 0 to 0.5 maps to -1 to 1
                sample = (phaseValue * 4f - 1f) * Amplitude;
            }
            else
            {
                // Falling edge: 0.5 to 1 maps to 1 to -1
                sample = (3f - phaseValue * 4f) * Amplitude;
            }

            buffer[offset + i] = sample;

            // Advance phase
            _phase += _frequency / WaveFormat.SampleRate;
            if (_phase >= 1.0f)
                _phase -= 1.0f;
        }

        return count;
    }
}
