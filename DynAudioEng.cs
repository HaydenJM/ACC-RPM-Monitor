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
        Standard,                  // Progressive beeping (slow → fast → solid)
        PerformanceLearning,       // Pitch-based guidance (low/high pitch for shift recommendation)
        FeedbackOptimization       // Post-shift feedback: two short beeps after shift (Early/OnTime/Late)
    }

    // Audio profile for Performance Learning mode
    public enum AudioProfile
    {
        Normal,      // Standard tone profiles optimized for responsiveness
        Endurance    // Low-fatigue tone profiles for extended sessions
    }

    private AudioMode _currentMode = AudioMode.Standard;
    private AudioProfile _currentProfile = AudioProfile.Normal;
    private int _recommendedShiftRPM = 0; // Used in Performance Learning mode

    // Tone profiles for Performance Learning mode
    private class ToneProfile
    {
        public float Frequency { get; set; }
        public int DurationMs { get; set; }
        public int AttackMs { get; set; }
        public int DecayMs { get; set; }
        public float DecayLevel { get; set; } // 0.0 to 1.0
        public float RelativeDbLevel { get; set; } // Amplitude multiplier
        public string WaveformType { get; set; } = "triangle"; // "sine", "triangle", "rounded"
        public float GlideFrequencyDelta { get; set; } = 0f; // Frequency change for glide (Hz)
        public int GlideDurationMs { get; set; } = 0; // Duration of glide effect (ms)
    }

    // Performance Learning tone profiles: Too Early, Optimal, Too Late
    private readonly ToneProfile _toneTooEarly = new()
    {
        Frequency = 950f,
        DurationMs = 130,
        AttackMs = 5,
        DecayMs = 120,
        DecayLevel = 0.60f,
        RelativeDbLevel = 0.707f, // -3dB = 0.707 amplitude
        WaveformType = "rounded",
        GlideFrequencyDelta = -10f, // -10 Hz glide (descending)
        GlideDurationMs = 100 // Over 100 ms
    };

    private readonly ToneProfile _toneOptimal = new()
    {
        Frequency = 600f,
        DurationMs = 140,
        AttackMs = 5,
        DecayMs = 135,
        DecayLevel = 0.55f,
        RelativeDbLevel = 1.0f, // 0dB = reference level
        WaveformType = "sine"
    };

    private readonly ToneProfile _toneTooLate = new()
    {
        Frequency = 400f,
        DurationMs = 150,
        AttackMs = 5,
        DecayMs = 145,
        DecayLevel = 0.50f,
        RelativeDbLevel = 0.794f, // -2dB = 0.794 amplitude
        WaveformType = "triangle"
    };

    // Endurance tone profiles: Too Early, Optimal, Too Late
    // Lower-fatigue alternative for extended sessions
    private readonly ToneProfile _toneEnduranceTooEarly = new()
    {
        Frequency = 650f,
        DurationMs = 110,
        AttackMs = 8,
        DecayMs = 130,
        DecayLevel = 0.57f,
        RelativeDbLevel = 0.707f, // -3dB
        WaveformType = "sine",
        GlideFrequencyDelta = -10f, // -10 Hz glide (descending)
        GlideDurationMs = 60 // Over 60 ms
    };

    private readonly ToneProfile _toneEnduranceOptimal = new()
    {
        Frequency = 500f,
        DurationMs = 130,
        AttackMs = 10,
        DecayMs = 100,
        DecayLevel = 0.52f,
        RelativeDbLevel = 1.0f, // 0dB
        WaveformType = "sine"
    };

    private readonly ToneProfile _toneEnduranceTooLate = new()
    {
        Frequency = 400f,
        DurationMs = 140,
        AttackMs = 8,
        DecayMs = 160,
        DecayLevel = 0.48f,
        RelativeDbLevel = 0.707f, // -3dB
        WaveformType = "sine",
        GlideFrequencyDelta = -15f, // -15 Hz glide (descending)
        GlideDurationMs = 120 // Over 120 ms
    };

    // Track performance learning audio state
    private DateTime _performanceAudioStartTime = DateTime.MinValue;
    private int _performanceAudioStartRPM = 0;
    private float _lastRPMRate = 0f;

    // Post-shift evaluation model state
    private int _shiftFromGear = 0; // Gear we shifted FROM (for tracking)
    private int _shiftToGear = 0; // Gear we shifted TO
    private int _shiftFromRPM = 0; // RPM at moment of gear change
    private int _recommendedShiftRPMAtShift = 0; // Recommended RPM for the gear we shifted from

    // Shift detection state machine
    private enum ShiftEvaluationState
    {
        Idle,                    // No shift in progress
        PredictionWindow,        // 100-150ms before predicted shift (optional)
        DetectingGearChange,     // Waiting for telemetry to confirm gear change
        StabilizingNewGear,      // 200ms stabilization window after gear confirmed
        EvaluatingShiftQuality,  // Computing error and playing feedback tone
        LockoutPeriod            // 400-500ms lockout to prevent overlaps
    }

    private ShiftEvaluationState _shiftEvalState = ShiftEvaluationState.Idle;
    private DateTime _shiftStateChangeTime = DateTime.MinValue;
    private int _lastGearForShiftDetection = 0; // Track for shift detection

    // Timing constants for post-shift evaluation
    private const int PreShiftPredictionMs = 125; // 100-150ms pre-shift cue (mid-range)
    private const int GearStabilizationMs = 200; // Confirm new gear stable for 200ms
    private const int ShiftLockoutMs = 450; // 400-500ms lockout (mid-range)
    private const int ShiftDetectionTimeoutMs = 500; // Max time to detect gear change after prediction

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
    /// Sets the audio profile (Normal or Endurance) for Performance Learning mode.
    /// </summary>
    public void SetAudioProfile(AudioProfile profile)
    {
        _currentProfile = profile;
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
        if (_currentMode == AudioMode.FeedbackOptimization)
        {
            UpdateFeedbackOptimizationAudio(currentRPM, threshold, currentGear);
        }
        else if (_currentMode == AudioMode.PerformanceLearning)
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
    /// Performance Learning mode: Tone-based guidance with distinct audio profiles.
    /// - Too Early (950 Hz): Shift too late, needs earlier action
    /// - Optimal (600 Hz): Within optimal range (±175 RPM)
    /// - Too Late (400 Hz): Shift too early, needs later action
    /// </summary>
    private void UpdatePerformanceLearningAudio(int currentRPM, int threshold, int currentGear)
    {
        int warningDistance = 300; // Fixed distance for performance mode
        int rpmFromThreshold = currentRPM - threshold;

        // Calculate RPM rate for intelligent audio stopping
        _lastRPMRate = CalculateRPMRate();

        // Only play when within warning distance
        if (rpmFromThreshold >= -warningDistance && _recommendedShiftRPM > 0)
        {
            // Determine which tone to play based on RPM vs recommended shift point and audio profile
            ToneProfile toneToPlay;

            if (currentRPM < _recommendedShiftRPM - 175)
            {
                // Shifting too early - use "Too Early" tone (high pitch to indicate shift later)
                toneToPlay = _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooEarly : _toneTooEarly;
            }
            else if (currentRPM > _recommendedShiftRPM + 175)
            {
                // Shifting too late - use "Too Late" tone (low pitch to indicate shift earlier)
                toneToPlay = _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooLate : _toneTooLate;
            }
            else
            {
                // Within optimal range - use "Optimal" tone (mid pitch)
                toneToPlay = _currentProfile == AudioProfile.Endurance ? _toneEnduranceOptimal : _toneOptimal;
            }

            // Check if audio should stop due to RPM rise rate dropping
            const float RPMRateThresholdToStop = 50f; // RPM/sec below which we stop audio
            if (_lastRPMRate < RPMRateThresholdToStop)
            {
                Stop();
                _performanceAudioStartTime = DateTime.MinValue;
                return;
            }

            // Start new tone or continue current one
            if (!_isPlaying || _performanceAudioStartTime == DateTime.MinValue)
            {
                _performanceAudioStartTime = DateTime.Now;
                _performanceAudioStartRPM = currentRPM;
                PlayTone(toneToPlay);
            }
            // Check if current tone duration has expired
            else if ((DateTime.Now - _performanceAudioStartTime).TotalMilliseconds >= toneToPlay.DurationMs)
            {
                // Tone finished, stop audio and wait for next trigger
                Stop();
                _performanceAudioStartTime = DateTime.MinValue;
            }
        }
        else
        {
            Stop();
            _performanceAudioStartTime = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Feedback-Based Optimization mode: Post-shift evaluation with audio feedback.
    ///
    /// State flow:
    /// 1. Idle - Normal driving, SILENT monitoring
    /// 2. DetectingGearChange - Gear change detected via telemetry
    /// 3. StabilizingNewGear - Wait 200ms for new gear to stabilize
    /// 4. EvaluatingShiftQuality - Compute error and play feedback tone (if needed)
    /// 5. LockoutPeriod - 400-500ms lockout to prevent overlaps
    ///
    /// Feedback tones indicate shift quality based on error:
    /// - High Pitch (950 Hz) = Shifted too EARLY (before optimal, > -175 RPM)
    /// - NO SOUND = Shifted at OPTIMAL time (within ±175 RPM) ✓ CORRECT!
    /// - Low Pitch (400 Hz) = Shifted too LATE (after optimal, > +175 RPM)
    ///
    /// Key behavior: SILENT when shift is good, audio feedback only for corrections needed.
    /// </summary>
    private void UpdateFeedbackOptimizationAudio(int currentRPM, int threshold, int currentGear)
    {
        DateTime now = DateTime.Now;
        double elapsedMs = (now - _shiftStateChangeTime).TotalMilliseconds;

        // State machine for post-shift evaluation
        switch (_shiftEvalState)
        {
            case ShiftEvaluationState.Idle:
                // Monitor for upshift from gears 1-5 (skip 6th gear+)
                if (currentGear > _lastGearForShiftDetection && _lastGearForShiftDetection > 0 && _lastGearForShiftDetection < 6)
                {
                    // Upshift detected! Capture shift data immediately
                    // IMPORTANT: We're in the NEW gear now, but currentRPM hasn't updated yet from the last frame
                    // So we need to use the RPM history to get the pre-shift RPM
                    _shiftFromGear = _lastGearForShiftDetection;
                    _shiftToGear = currentGear;

                    // Get the RPM from before the gear change (use RPM history if available)
                    if (_rpmHistory.Count > 0)
                    {
                        // Use the most recent RPM from history (before this frame)
                        _shiftFromRPM = _rpmHistory.Last().rpm;
                    }
                    else
                    {
                        // Fallback to current RPM if no history
                        _shiftFromRPM = currentRPM;
                    }

                    _recommendedShiftRPMAtShift = _recommendedShiftRPM;

                    _shiftEvalState = ShiftEvaluationState.DetectingGearChange;
                    _shiftStateChangeTime = now;
                }
                break;

            case ShiftEvaluationState.DetectingGearChange:
                // Wait for gear change to be fully confirmed via telemetry
                // Once we see the new gear number confirmed, move to stabilization
                if (currentGear == _shiftToGear)
                {
                    _shiftEvalState = ShiftEvaluationState.StabilizingNewGear;
                    _shiftStateChangeTime = now;
                    // Don't update _shiftFromRPM here - we already captured it in Idle state
                }
                // Timeout if gear change takes too long
                else if (elapsedMs > ShiftDetectionTimeoutMs)
                {
                    _shiftEvalState = ShiftEvaluationState.Idle;
                }
                break;

            case ShiftEvaluationState.StabilizingNewGear:
                // Wait 200ms for new gear to stabilize before evaluating
                if (elapsedMs >= GearStabilizationMs)
                {
                    _shiftEvalState = ShiftEvaluationState.EvaluatingShiftQuality;
                    _shiftStateChangeTime = now;
                }
                break;

            case ShiftEvaluationState.EvaluatingShiftQuality:
                // Compute shift quality error
                int shiftError = _shiftFromRPM - _recommendedShiftRPMAtShift;

                // Only play feedback tone if shift was NOT optimal (outside ±175 RPM)
                // Optimal shifts (within ±175 RPM) = SILENT (no tone = correct shift!)
                if (Math.Abs(shiftError) > 175)
                {
                    ToneProfile feedbackTone = GetShiftQualityTone(_shiftFromRPM, _recommendedShiftRPMAtShift);
                    PlayTone(feedbackTone);
                }
                // Otherwise stay silent - good shift!

                _shiftEvalState = ShiftEvaluationState.LockoutPeriod;
                _shiftStateChangeTime = now;
                break;

            case ShiftEvaluationState.LockoutPeriod:
                // Lockout period (400-500ms) to prevent overlapping shift evaluations
                if (elapsedMs >= ShiftLockoutMs)
                {
                    _shiftEvalState = ShiftEvaluationState.Idle;
                }
                // Stay silent during lockout
                Stop();
                break;
        }

        // Always stay silent except during EvaluatingShiftQuality (which plays the tone)
        if (_shiftEvalState != ShiftEvaluationState.EvaluatingShiftQuality)
        {
            // Only stop if we're not already playing a tone
            if (!_isPlaying || _shiftEvalState == ShiftEvaluationState.LockoutPeriod)
            {
                Stop();
            }
        }

        _lastGearForShiftDetection = currentGear;
    }

    /// <summary>
    /// Determines which tone profile to play based on shift quality.
    /// Note: This should only be called when shift is NOT optimal (checked before calling).
    /// </summary>
    private ToneProfile GetShiftQualityTone(int shiftRPM, int recommendedRPM)
    {
        if (shiftRPM < recommendedRPM - 175)
        {
            // Shifted too early - use "Too Early" tone (high pitch, descending glide)
            // Tells user to shift LATER next time
            return _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooEarly : _toneTooEarly;
        }
        else // shiftRPM > recommendedRPM + 175
        {
            // Shifted too late - use "Too Late" tone (low pitch)
            // Tells user to shift EARLIER next time
            return _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooLate : _toneTooLate;
        }
    }

    /// <summary>
    /// Plays a tone with the specified profile (frequency, duration, envelope, etc.)
    /// </summary>
    private void PlayTone(ToneProfile tone)
    {
        // Configure wave provider with tone parameters, including glide effects
        _waveProvider.SetFrequency(tone.Frequency);
        _waveProvider.SetToneProfile(tone.DurationMs, tone.AttackMs, tone.DecayMs,
                                      tone.DecayLevel, tone.RelativeDbLevel, tone.WaveformType,
                                      tone.GlideFrequencyDelta, tone.GlideDurationMs);
        _waveProvider.SetBeeping(false, 0, 0); // Solid tone, no beeping pattern

        if (!_isPlaying)
        {
            _waveOut.Play();
            _isPlaying = true;
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
/// Generates triangle/sine wave audio with ADSR envelope, low-pass filter, and micro-glide support.
/// Supports both beeping patterns (Standard mode) and tone profiles (Performance Learning mode).
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
    private const float BaseAmplitude = 0.15f; // Constant base volume

    // Tone profile (ADSR + envelope)
    private bool _useToneProfile = false;
    private int _toneDurationSamples = 0;
    private int _attackSamples = 0;
    private int _decaySamples = 0;
    private float _decayLevel = 1.0f;
    private float _relativeDbLevel = 1.0f;
    private int _samplesSinceToneStart = 0;
    private string _waveformType = "triangle";

    // Low-pass filter state
    private float _filterState = 0f;
    private const float FilterCutoffHz = 1800f; // Gentle roll-off around 1.8 kHz
    private float _filterAlpha;

    // Micro-glide support (frequency, direction, and duration)
    private float _targetFrequency;
    private float _glideRate = 0f;
    private int _glideDurationSamples = 0; // Duration for glide effect
    private int _glideSampleCount = 0; // Current glide progress

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

    public TriangleWaveProvider()
    {
        // Calculate filter alpha for low-pass filter (1-pole RC filter)
        float dt = 1f / WaveFormat.SampleRate;
        _filterAlpha = (2f * MathF.PI * FilterCutoffHz * dt) / (1f + 2f * MathF.PI * FilterCutoffHz * dt);
    }

    public void SetFrequency(float frequency)
    {
        _frequency = frequency;
        _targetFrequency = frequency;
    }

    /// <summary>
    /// Sets tone profile for Performance Learning mode with ADSR envelope.
    /// </summary>
    public void SetToneProfile(int durationMs, int attackMs, int decayMs, float decayLevel, float relativeDbLevel, string waveformType, float glideFreqDelta = 0f, int glideDurationMs = 0)
    {
        _useToneProfile = true;
        _toneDurationSamples = (int)(durationMs * WaveFormat.SampleRate / 1000.0);
        _attackSamples = (int)(attackMs * WaveFormat.SampleRate / 1000.0);
        _decaySamples = (int)(decayMs * WaveFormat.SampleRate / 1000.0);
        _decayLevel = decayLevel;
        _relativeDbLevel = relativeDbLevel;
        _waveformType = waveformType;
        _samplesSinceToneStart = 0;
        _glideSampleCount = 0;

        // Setup micro-glide if specified
        if (glideFreqDelta != 0f && glideDurationMs > 0)
        {
            _targetFrequency = _frequency + glideFreqDelta;
            _glideDurationSamples = (int)(glideDurationMs * WaveFormat.SampleRate / 1000.0);
            _glideRate = glideFreqDelta / glideDurationMs * (WaveFormat.SampleRate / 1000f); // Change per sample
        }
        else
        {
            _glideRate = 0f;
            _glideDurationSamples = 0;
        }
    }

    /// <summary>
    /// Sets beeping pattern. If not beeping, plays solid tone.
    /// </summary>
    public void SetBeeping(bool isBeeping, int beepOnMs, int beepOffMs)
    {
        bool modeChanged = _isBeeping != isBeeping;

        _isBeeping = isBeeping;
        _useToneProfile = false;

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

            // Handle tone profile (Performance Learning mode)
            if (_useToneProfile)
            {
                // Calculate envelope (ADSR)
                float envelopeLevel = 1.0f;

                if (_samplesSinceToneStart < _attackSamples)
                {
                    // Attack phase: ramp from 0 to 1
                    envelopeLevel = (float)_samplesSinceToneStart / _attackSamples;
                }
                else if (_samplesSinceToneStart < _attackSamples + _decaySamples)
                {
                    // Decay phase: ramp from 1 to decay level
                    int decayProgress = _samplesSinceToneStart - _attackSamples;
                    envelopeLevel = 1.0f - ((1.0f - _decayLevel) * ((float)decayProgress / _decaySamples));
                }
                else
                {
                    // Sustain at decay level
                    envelopeLevel = _decayLevel;
                }

                // Generate waveform
                sample = GenerateWaveform(_waveformType, _phase) * envelopeLevel * _relativeDbLevel * BaseAmplitude;

                // Apply low-pass filter
                _filterState = (_filterState * (1f - _filterAlpha)) + (sample * _filterAlpha);
                sample = _filterState;

                // Apply micro-glide if enabled and within glide duration
                if (_glideRate != 0f && _glideSampleCount < _glideDurationSamples)
                {
                    _frequency += _glideRate;
                    _glideSampleCount++;
                }

                _samplesSinceToneStart++;
            }
            // Handle beeping mode (Standard mode)
            else if (_isBeeping)
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

                // Generate triangle wave
                sample = GenerateWaveform("triangle", _phase) * BaseAmplitude;
            }
            else
            {
                // Solid tone mode (no beeping)
                sample = GenerateWaveform("triangle", _phase) * BaseAmplitude;
            }

            buffer[offset + i] = sample;

            // Advance phase
            _phase += _frequency / WaveFormat.SampleRate;
            if (_phase >= 1.0f)
                _phase -= 1.0f;
        }

        return count;
    }

    /// <summary>
    /// Generates waveform sample based on type: triangle, sine, or rounded (rounded triangle/sine blend)
    /// </summary>
    private float GenerateWaveform(string type, float phase)
    {
        float phaseValue = phase % 1.0f;

        if (type == "sine")
        {
            return MathF.Sin(phaseValue * 2f * MathF.PI);
        }
        else if (type == "rounded")
        {
            // Blend of sine and triangle for smoother edges
            float triangle = phaseValue < 0.5f ? (phaseValue * 4f - 1f) : (3f - phaseValue * 4f);
            float sine = MathF.Sin(phaseValue * 2f * MathF.PI);
            return (triangle * 0.6f) + (sine * 0.4f);
        }
        else // triangle (default)
        {
            return phaseValue < 0.5f ? (phaseValue * 4f - 1f) : (3f - phaseValue * 4f);
        }
    }
}
