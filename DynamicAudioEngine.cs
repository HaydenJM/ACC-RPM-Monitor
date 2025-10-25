using NAudio.Wave;

namespace ACCRPMMonitor;

// Audio engine with dynamic warning timing based on RPM rate of change
public class DynamicAudioEngine : IDisposable
{
    private readonly WaveOutEvent _waveOut;
    private readonly TriangleWaveProvider _waveProvider;
    private bool _isPlaying;

    // RPM rate tracking
    private readonly Queue<(int rpm, DateTime timestamp)> _rpmHistory = new();
    private const int RPMHistoryWindowMs = 200; // Track last 200ms of RPM changes

    // Downshift mute tracking
    private DateTime _lastDownshiftMuteTime = DateTime.MinValue;
    private int _lastGear = 0;
    private const int DownshiftMuteDurationMs = 200; // Mute audio for 200ms after downshift

    public DynamicAudioEngine()
    {
        _waveProvider = new TriangleWaveProvider();
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_waveProvider);
    }

    // Updates audio with dynamic volume ramping based on proximity to threshold
    public void UpdateRPM(int currentRPM, int threshold, int currentGear)
    {
        // Detect downshift and reset mute timer (don't add to it, just reset to 200ms)
        if (currentGear < _lastGear)
        {
            _lastDownshiftMuteTime = DateTime.Now;
        }
        _lastGear = currentGear;

        // Mute audio for 200ms after downshift
        if ((DateTime.Now - _lastDownshiftMuteTime).TotalMilliseconds < DownshiftMuteDurationMs)
        {
            if (_isPlaying)
            {
                _waveOut.Stop();
                _isPlaying = false;
            }
            return;
        }

        // No audio in 6th gear or higher (no 7th gear exists)
        if (currentGear >= 6)
        {
            if (_isPlaying)
            {
                _waveOut.Stop();
                _isPlaying = false;
            }
            return;
        }

        // Hard-coded minimum RPM threshold - never play audio below 6000 RPM
        if (currentRPM < 6000)
        {
            if (_isPlaying)
            {
                _waveOut.Stop();
                _isPlaying = false;
            }
            return;
        }

        // Track RPM history for rate calculation
        DateTime now = DateTime.Now;
        _rpmHistory.Enqueue((currentRPM, now));

        // Remove old entries outside the window
        while (_rpmHistory.Count > 0 && (now - _rpmHistory.Peek().timestamp).TotalMilliseconds > RPMHistoryWindowMs)
        {
            _rpmHistory.Dequeue();
        }

        // Calculate RPM rate of change (RPM per second)
        float rpmRate = CalculateRPMRate();

        // Dynamic warning distance based on RPM acceleration
        int warningDistance = CalculateDynamicBeepDistance(rpmRate);

        int rpmFromThreshold = currentRPM - threshold;

        // Each gear gets its own frequency, starting at 500Hz
        float frequency = 500f + (currentGear - 1) * 100f;

        // Start playing steady tone when approaching threshold
        if (rpmFromThreshold >= -warningDistance)
        {
            // Calculate volume based on proximity to threshold (0.0 to 1.0)
            // Volume increases from quiet to full as we approach threshold
            float volumePercent;
            if (rpmFromThreshold >= 0)
            {
                // At or above threshold - full volume
                volumePercent = 1.0f;
            }
            else
            {
                // Below threshold - ramp volume from 0 to 1 over the warning distance
                volumePercent = 1.0f - (Math.Abs(rpmFromThreshold) / (float)warningDistance);
                volumePercent = Math.Max(0.0f, Math.Min(1.0f, volumePercent)); // Clamp to 0-1
            }

            _waveProvider.SetFrequency(frequency);
            _waveProvider.SetVolume(volumePercent);
            _waveProvider.SetBeeping(false); // Steady tone, not beeping

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

    // Calculates the current RPM rate of change in RPM/second
    private float CalculateRPMRate()
    {
        if (_rpmHistory.Count < 2)
            return 0f;

        var oldest = _rpmHistory.First();
        var newest = _rpmHistory.Last();

        double timeDiffSeconds = (newest.timestamp - oldest.timestamp).TotalSeconds;
        if (timeDiffSeconds < 0.01) // Avoid division by very small numbers
            return 0f;

        int rpmDiff = newest.rpm - oldest.rpm;
        return (float)(rpmDiff / timeDiffSeconds);
    }

    // Determines how far before threshold to start beeping based on RPM acceleration
    private int CalculateDynamicBeepDistance(float rpmRatePerSecond)
    {
        // Very fast RPM increase (>1500 RPM/sec) - beep earlier to give reaction time
        if (rpmRatePerSecond > 1500f)
            return 200; // Start beeping 200 RPM below threshold

        // Fast RPM increase (>1000 RPM/sec)
        if (rpmRatePerSecond > 1000f)
            return 150; // Start beeping 150 RPM below threshold

        // Moderate-fast increase (>600 RPM/sec)
        if (rpmRatePerSecond > 600f)
            return 120;

        // Moderate increase (>300 RPM/sec)
        if (rpmRatePerSecond > 300f)
            return 100;

        // Slow-moderate increase (>150 RPM/sec)
        if (rpmRatePerSecond > 150f)
            return 80;

        // Slow increase (>50 RPM/sec) - beep close to threshold
        if (rpmRatePerSecond > 50f)
            return 50;

        // Very slow or stable - beep right at threshold
        return 30;
    }

    // Gets the current RPM rate for display purposes
    public float GetCurrentRPMRate()
    {
        return CalculateRPMRate();
    }

    // Gets the current dynamic beeping distance for display
    public int GetCurrentWarningDistance()
    {
        return CalculateDynamicBeepDistance(CalculateRPMRate());
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

// Generates triangle wave audio with optional beeping and volume control
internal class TriangleWaveProvider : ISampleProvider
{
    private float _frequency;
    private float _phase;
    private bool _isBeeping;
    private float _volume = 1.0f; // Volume multiplier (0.0 to 1.0)
    private int _samplesSinceBeepToggle;
    private bool _beepOn = true;
    private const int BeepOnSamples = 4410; // ~100ms at 44.1kHz
    private const int BeepOffSamples = 4410;
    private const float BaseAmplitude = 0.15f;

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

    public void SetFrequency(float frequency)
    {
        _frequency = frequency;
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Max(0.0f, Math.Min(1.0f, volume)); // Clamp to 0-1
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
                sample = (phaseValue * 4f - 1f) * BaseAmplitude * _volume;
            }
            else
            {
                // Falling edge: 0.5 to 1 maps to 1 to -1
                sample = (3f - phaseValue * 4f) * BaseAmplitude * _volume;
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
