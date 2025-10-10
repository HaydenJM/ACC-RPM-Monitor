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

    // Updates audio with dynamic beeping threshold based on RPM acceleration
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

        // Dynamic beeping distance based on RPM acceleration
        int beepDistance = CalculateDynamicBeepDistance(rpmRate);

        int rpmFromThreshold = currentRPM - threshold;

        // Each gear gets its own frequency, starting at 500Hz
        float frequency = 500f + (currentGear - 1) * 100f;

        // Beeping phase - starts at dynamic distance based on RPM rate
        if (rpmFromThreshold >= -beepDistance)
        {
            _waveProvider.SetFrequency(frequency);
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

    // Determines how far before threshold to start beeping based on RPM acceleration
    private int CalculateDynamicBeepDistance(float rpmRatePerSecond)
    {
        // Very fast RPM increase (>1500 RPM/sec) - beep much earlier
        if (rpmRatePerSecond > 1500f)
            return 400; // Start beeping 400 RPM below threshold

        // Fast RPM increase (>1000 RPM/sec) - beep earlier
        if (rpmRatePerSecond > 1000f)
            return 300; // Start beeping 300 RPM below threshold

        // Moderate-fast increase (>600 RPM/sec)
        if (rpmRatePerSecond > 600f)
            return 250;

        // Moderate increase (>300 RPM/sec)
        if (rpmRatePerSecond > 300f)
            return 200;

        // Slow-moderate increase (>150 RPM/sec)
        if (rpmRatePerSecond > 150f)
            return 150;

        // Slow increase (>50 RPM/sec) - beep close to threshold
        if (rpmRatePerSecond > 50f)
            return 100;

        // Very slow or stable - beep right at threshold
        return 50;
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
