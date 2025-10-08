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

    public DynamicAudioEngine()
    {
        _waveProvider = new TriangleWaveProvider();
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_waveProvider);
    }

    // Updates audio with dynamic warning threshold based on RPM acceleration
    public void UpdateRPM(int currentRPM, int threshold, int currentGear)
    {
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
        int warningDistance = CalculateDynamicWarningDistance(rpmRate);
        int beepDistance = 100; // Beep always starts at 100 RPM below

        int rpmFromThreshold = currentRPM - threshold;

        // Each gear gets its own 100Hz frequency range, starting at 500Hz
        float baseFreq = 500f + (currentGear - 1) * 100f;
        float maxFreq = baseFreq + 100f;

        // Rising tone phase - distance is dynamic based on RPM rate
        if (rpmFromThreshold >= -warningDistance && rpmFromThreshold < -beepDistance)
        {
            // Calculate how far through the warning zone we are (0.0 to 1.0)
            float warningProgress = (float)(currentRPM - (threshold - warningDistance)) / (warningDistance - beepDistance);
            warningProgress = Math.Clamp(warningProgress, 0f, 1f);

            // Frequency rises from baseFreq to maxFreq based on progress
            float frequency = baseFreq + (maxFreq - baseFreq) * warningProgress;

            _waveProvider.SetFrequency(frequency);
            _waveProvider.SetBeeping(false);

            if (!_isPlaying)
            {
                _waveOut.Play();
                _isPlaying = true;
            }
        }
        // Beeping phase - starts at 100 RPM below threshold
        else if (rpmFromThreshold >= -beepDistance)
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

    // Determines how far before threshold to start warning based on RPM acceleration
    private int CalculateDynamicWarningDistance(float rpmRatePerSecond)
    {
        // Fast RPM increase (>500 RPM/sec) - warn earlier
        if (rpmRatePerSecond > 500f)
            return 500; // Start warning 500 RPM below threshold

        // Very fast (>1000 RPM/sec) - warn even earlier
        if (rpmRatePerSecond > 1000f)
            return 600;

        // Moderate increase (200-500 RPM/sec) - standard warning
        if (rpmRatePerSecond > 200f)
            return 300; // Default 300 RPM

        // Slow increase (<200 RPM/sec) - can warn later
        if (rpmRatePerSecond > 0f)
            return 200; // Only 200 RPM before threshold

        // RPMs decreasing or stable - no warning needed
        return 100; // Minimal warning
    }

    // Gets the current RPM rate for display purposes
    public float GetCurrentRPMRate()
    {
        return CalculateRPMRate();
    }

    // Gets the current dynamic warning distance for display
    public int GetCurrentWarningDistance()
    {
        return CalculateDynamicWarningDistance(CalculateRPMRate());
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
