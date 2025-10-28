using NAudio.Wave;

namespace ACCRPMMonitor;

// Audio engine with mode-specific feedback strategies
public class DynAudioEng : IDisposable
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

    // Audio mode
    public enum AudioMode
    {
        Standard,           // Progressive beeping (slow → fast → solid)
        PerformanceLearning // Pitch-based guidance (low/high pitch for shift recommendation)
    }

    private AudioMode _currentMode = AudioMode.Standard;
    private int _recommendedShiftRPM = 0; // Used in Performance Learning mode

    public DynAudioEng()
    {
        _waveProvider = new TriangleWaveProvider();
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_waveProvider);
    }

    /// <summary>
    /// Sets the audio mode for different monitoring modes.
    /// </summary>
    public void SetMode(AudioMode mode)
    {
        _currentMode = mode;
    }

    /// <summary>
    /// Sets the recommended shift RPM for Performance Learning mode.
    /// </summary>
    public void SetRecommendedShiftRPM(int rpm)
    {
        _recommendedShiftRPM = rpm;
    }

    /// <summary>
    /// Updates audio based on current mode.
    /// </summary>
    public void UpdateRPM(int currentRPM, int threshold, int currentGear)
    {
        // Detect downshift and reset mute timer
        if (currentGear < _lastGear)
        {
            _lastDownshiftMuteTime = DateTime.Now;
        }
        _lastGear = currentGear;

        // Mute audio for 200ms after downshift
        if ((DateTime.Now - _lastDownshiftMuteTime).TotalMilliseconds < DownshiftMuteDurationMs)
        {
            Stop();
            return;
        }

        // No audio in 6th gear or higher
        if (currentGear >= 6)
        {
            Stop();
            return;
        }

        // Hard-coded minimum RPM threshold - never play audio below 6000 RPM
        if (currentRPM < 6000)
        {
            Stop();
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

        // Route to appropriate audio mode
        if (_currentMode == AudioMode.PerformanceLearning)
        {
            UpdatePerformanceLearningAudio(currentRPM, threshold, currentGear);
        }
        else
        {
            UpdateStandardAudio(currentRPM, threshold, currentGear);
        }
    }

    /// <summary>
    /// Standard/Adaptive mode: Progressive beeping that accelerates as RPM approaches threshold.
    /// Slow beeps → fast beeps → solid tone at threshold.
    /// </summary>
    private void UpdateStandardAudio(int currentRPM, int threshold, int currentGear)
    {
        // Calculate RPM rate and dynamic warning distance
        float rpmRate = CalculateRPMRate();
        int warningDistance = CalculateDynamicWarningDistance(rpmRate);
        int rpmFromThreshold = currentRPM - threshold;

        // Each gear gets its own frequency
        float frequency = 500f + (currentGear - 1) * 100f;

        // Only play when within warning distance
        if (rpmFromThreshold >= -warningDistance)
        {
            _waveProvider.SetFrequency(frequency);

            // Progressive beeping based on proximity to threshold
            if (rpmFromThreshold >= 0)
            {
                // At or above threshold - solid tone
                _waveProvider.SetBeeping(false, 0, 0);
            }
            else
            {
                // Below threshold - progressive beeping
                // Calculate beep rate based on proximity (0.0 = far, 1.0 = at threshold)
                float proximityRatio = 1.0f - (Math.Abs(rpmFromThreshold) / (float)warningDistance);

                // Beep timing: Far = 500ms on/500ms off, Close = 50ms on/50ms off, At threshold = solid
                int maxBeepMs = 500;
                int minBeepMs = 50;

                int beepOnMs = (int)(maxBeepMs - (proximityRatio * (maxBeepMs - minBeepMs)));
                int beepOffMs = beepOnMs; // Keep on/off equal for rhythm

                _waveProvider.SetBeeping(true, beepOnMs, beepOffMs);
            }

            if (!_isPlaying)
            {
                _waveOut.Play();
                _isPlaying = true;
            }
        }
        else
        {
            Stop();
        }
    }

    /// <summary>
    /// Performance Learning mode: Pitch-based guidance indicating shift earlier/later.
    /// High pitch = shift earlier (you're too high), Low pitch = shift later (you're too low), Medium pitch = optimal.
    /// </summary>
    private void UpdatePerformanceLearningAudio(int currentRPM, int threshold, int currentGear)
    {
        int warningDistance = 300; // Fixed distance for performance mode
        int rpmFromThreshold = currentRPM - threshold;

        // Only play when within warning distance
        if (rpmFromThreshold >= -warningDistance)
        {
            // Calculate difference from recommended shift point
            int rpmFromRecommended = currentRPM - _recommendedShiftRPM;

            // Base frequency for this gear
            float baseFrequency = 500f + (currentGear - 1) * 100f;

            // Modulate frequency based on recommendation:
            // - Currently shifting too late (above recommended): Higher pitch = shift earlier!
            // - Shifting at optimal point: Normal pitch
            // - Currently shifting too early (below recommended): Lower pitch = shift later!

            float frequency;
            if (_recommendedShiftRPM == 0)
            {
                // No recommendation yet - use normal frequency
                frequency = baseFrequency;
            }
            else if (currentRPM > _recommendedShiftRPM + 175)
            {
                // Currently above recommended - HIGH pitch = shift earlier!
                frequency = baseFrequency + 200f;
            }
            else if (currentRPM < _recommendedShiftRPM - 175)
            {
                // Currently below recommended - LOW pitch = shift later!
                frequency = baseFrequency - 200f;
            }
            else
            {
                // Within ±175 RPM of optimal - normal pitch (optimal zone)
                frequency = baseFrequency;
            }

            _waveProvider.SetFrequency(frequency);
            _waveProvider.SetBeeping(false, 0, 0); // Solid tone in performance mode

            if (!_isPlaying)
            {
                _waveOut.Play();
                _isPlaying = true;
            }
        }
        else
        {
            Stop();
        }
    }

    /// <summary>
    /// Calculates the current RPM rate of change in RPM/second.
    /// </summary>
    private float CalculateRPMRate()
    {
        if (_rpmHistory.Count < 2)
            return 0f;

        var oldest = _rpmHistory.First();
        var newest = _rpmHistory.Last();

        double timeDiffSeconds = (newest.timestamp - oldest.timestamp).TotalSeconds;
        if (timeDiffSeconds < 0.01)
            return 0f;

        int rpmDiff = newest.rpm - oldest.rpm;
        return (float)(rpmDiff / timeDiffSeconds);
    }

    /// <summary>
    /// Determines warning distance based on RPM acceleration.
    /// </summary>
    private int CalculateDynamicWarningDistance(float rpmRatePerSecond)
    {
        if (rpmRatePerSecond > 1500f) return 200;
        if (rpmRatePerSecond > 1000f) return 150;
        if (rpmRatePerSecond > 600f) return 120;
        if (rpmRatePerSecond > 300f) return 100;
        if (rpmRatePerSecond > 150f) return 80;
        if (rpmRatePerSecond > 50f) return 50;
        return 30;
    }

    public float GetCurrentRPMRate() => CalculateRPMRate();
    public int GetCurrentWarningDistance() => CalculateDynamicWarningDistance(CalculateRPMRate());

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
/// Generates triangle wave audio with configurable beeping patterns.
/// </summary>
internal class TriangleWaveProvider : ISampleProvider
{
    private float _frequency;
    private float _phase;
    private bool _isBeeping;
    private int _beepOnSamples;
    private int _beepOffSamples;
    private int _samplesSinceBeepToggle;
    private bool _beepOn = true;
    private const float Amplitude = 0.15f; // Constant volume

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

    public void SetFrequency(float frequency)
    {
        _frequency = frequency;
    }

    /// <summary>
    /// Sets beeping pattern. If not beeping, plays solid tone.
    /// </summary>
    /// <param name="isBeeping">Whether to beep or play solid tone</param>
    /// <param name="beepOnMs">Duration of beep in milliseconds</param>
    /// <param name="beepOffMs">Duration of silence in milliseconds</param>
    public void SetBeeping(bool isBeeping, int beepOnMs, int beepOffMs)
    {
        bool modeChanged = _isBeeping != isBeeping;

        _isBeeping = isBeeping;

        if (isBeeping)
        {
            _beepOnSamples = (int)(beepOnMs * WaveFormat.SampleRate / 1000.0);
            _beepOffSamples = (int)(beepOffMs * WaveFormat.SampleRate / 1000.0);

            if (modeChanged)
            {
                _samplesSinceBeepToggle = 0;
                _beepOn = true;
            }
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

                if (_beepOn && _samplesSinceBeepToggle >= _beepOnSamples)
                {
                    _beepOn = false;
                    _samplesSinceBeepToggle = 0;
                }
                else if (!_beepOn && _samplesSinceBeepToggle >= _beepOffSamples)
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

            // Generate triangle wave sample (constant amplitude)
            float phaseValue = _phase % 1.0f;

            if (phaseValue < 0.5f)
            {
                sample = (phaseValue * 4f - 1f) * Amplitude;
            }
            else
            {
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
