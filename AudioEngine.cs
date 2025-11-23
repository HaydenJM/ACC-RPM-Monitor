using NAudio.Wave;

namespace ACCRPMMonitor;

/// <summary>
/// Audio engine for shift indication - soft chirping design with adaptive tolerance
/// Maximizes silence while providing precise shift timing with reaction time compensation
/// </summary>
public class AudioEngine : IDisposable
{
    private readonly WaveOutEvent _waveOut;
    private readonly TriangleWaveProvider _waveProvider;
    private bool _isPlaying;

    // RPM tracking for rate calculation and prediction
    private readonly Queue<(int rpm, DateTime timestamp)> _rpmHistory = new();
    private const int RPMHistoryWindowMs = 250;

    // Downshift muting
    private DateTime _lastDownshiftTime = DateTime.MinValue;
    private int _lastGear = 0;
    private const int DownshiftMuteDurationMs = 200;

    // Human reaction time compensation (75ms pro, 100ms default, 125ms average)
    private int _reactionTimeMs = 100;

    // Adaptive tolerance system - learns driver consistency (Feedback mode only)
    private readonly Queue<bool> _shiftSuccessHistory = new(); // Last 20 shifts: true = within tolerance
    private const int ShiftHistorySize = 20;
    private int _adaptiveTolerance = 175; // Starts at 175, adapts down to 100 based on consistency

    // Audio modes
    public enum AudioMode
    {
        Standard,              // Soft chirps with alert pitch
        PerformanceLearning,   // Occasional chirps for guidance
        FeedbackOptimization   // Post-shift feedback only
    }

    public enum AudioProfile
    {
        Normal,      // Alert responsiveness
        Endurance    // Gentle for long sessions
    }

    private AudioMode _currentMode = AudioMode.Standard;
    private AudioProfile _currentProfile = AudioProfile.Normal;
    private int _recommendedShiftRPM = 0;

    // Tone profiles - short chirps designed for minimal intrusion
    private class ToneProfile
    {
        public float Frequency { get; set; }
        public int DurationMs { get; set; }
        public int AttackMs { get; set; }
        public int DecayMs { get; set; }
        public float DecayLevel { get; set; }
        public float RelativeDbLevel { get; set; }
        public string WaveformType { get; set; } = "triangle";
        public float GlideFrequencyDelta { get; set; } = 0f;
        public int GlideDurationMs { get; set; } = 0;
    }

    // Normal profile - alert chirps (Performance/Feedback modes)
    private readonly ToneProfile _toneTooEarly = new()
    {
        Frequency = 1050f, DurationMs = 60, AttackMs = 3, DecayMs = 55,
        DecayLevel = 0.45f, RelativeDbLevel = 0.707f, WaveformType = "rounded",
        GlideFrequencyDelta = -15f, GlideDurationMs = 50
    };

    private readonly ToneProfile _toneOptimal = new()
    {
        Frequency = 850f, DurationMs = 65, AttackMs = 3, DecayMs = 60,
        DecayLevel = 0.50f, RelativeDbLevel = 0.85f, WaveformType = "sine"
    };

    private readonly ToneProfile _toneTooLate = new()
    {
        Frequency = 550f, DurationMs = 70, AttackMs = 3, DecayMs = 65,
        DecayLevel = 0.40f, RelativeDbLevel = 0.707f, WaveformType = "sine"
    };

    // Standard mode chirps - alert pitch, soft delivery
    private readonly ToneProfile _toneStandardFar = new()
    {
        Frequency = 800f, DurationMs = 50, AttackMs = 3, DecayMs = 45,
        DecayLevel = 0.40f, RelativeDbLevel = 0.6f, WaveformType = "sine"
    };

    private readonly ToneProfile _toneStandardApproaching = new()
    {
        Frequency = 950f, DurationMs = 55, AttackMs = 3, DecayMs = 50,
        DecayLevel = 0.45f, RelativeDbLevel = 0.75f, WaveformType = "sine"
    };

    private readonly ToneProfile _toneStandardShiftNow = new()
    {
        Frequency = 1100f, DurationMs = 60, AttackMs = 3, DecayMs = 55,
        DecayLevel = 0.50f, RelativeDbLevel = 0.85f, WaveformType = "rounded",
        GlideFrequencyDelta = 30f, GlideDurationMs = 50
    };

    // Endurance profile - gentle chirps for long sessions
    private readonly ToneProfile _toneEnduranceTooEarly = new()
    {
        Frequency = 700f, DurationMs = 55, AttackMs = 5, DecayMs = 50,
        DecayLevel = 0.40f, RelativeDbLevel = 0.6f, WaveformType = "sine",
        GlideFrequencyDelta = -10f, GlideDurationMs = 45
    };

    private readonly ToneProfile _toneEnduranceOptimal = new()
    {
        Frequency = 600f, DurationMs = 60, AttackMs = 5, DecayMs = 55,
        DecayLevel = 0.45f, RelativeDbLevel = 0.707f, WaveformType = "sine"
    };

    private readonly ToneProfile _toneEnduranceTooLate = new()
    {
        Frequency = 450f, DurationMs = 65, AttackMs = 5, DecayMs = 60,
        DecayLevel = 0.40f, RelativeDbLevel = 0.6f, WaveformType = "sine"
    };

    // Audio tracking
    private DateTime _performanceAudioStartTime = DateTime.MinValue;
    private DateTime _standardToneEndTime = DateTime.MinValue;
    private int _lastProximityZone = -1;
    private float _lastRPMRate = 0f;

    // Post-shift evaluation state machine
    private enum ShiftEvalState
    {
        Idle, DetectingGearChange, StabilizingNewGear,
        EvaluatingShiftQuality, LockoutPeriod
    }

    private ShiftEvalState _shiftEvalState = ShiftEvalState.Idle;
    private DateTime _shiftStateChangeTime = DateTime.MinValue;
    private int _lastGearForShiftDetection = 0;
    private int _shiftFromGear = 0;
    private int _shiftToGear = 0;
    private int _shiftFromRPM = 0;
    private int _recommendedShiftRPMAtShift = 0;

    private const int GearStabilizationMs = 200;
    private const int ShiftLockoutMs = 450;
    private const int ShiftDetectionTimeoutMs = 500;

    public AudioEngine()
    {
        _waveProvider = new TriangleWaveProvider();
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_waveProvider);
    }

    public void SetMode(AudioMode mode) => _currentMode = mode;
    public void SetAudioProfile(AudioProfile profile) => _currentProfile = profile;
    public void SetRecommendedShiftRPM(int rpm) => _recommendedShiftRPM = rpm;
    public void SetReactionTimeMs(int ms) => _reactionTimeMs = Math.Clamp(ms, 50, 200);
    public int GetAdaptiveTolerance() => _adaptiveTolerance;

    /// <summary>
    /// Main update - routes to appropriate audio mode
    /// </summary>
    public void UpdateRPM(int currentRPM, int threshold, int currentGear)
    {
        // Detect downshift and mute briefly
        if (currentGear < _lastGear)
            _lastDownshiftTime = DateTime.Now;
        _lastGear = currentGear;

        if ((DateTime.Now - _lastDownshiftTime).TotalMilliseconds < DownshiftMuteDurationMs)
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

        // Never play below 6000 RPM
        if (currentRPM < 6000)
        {
            Stop();
            return;
        }

        // Track RPM history
        DateTime now = DateTime.Now;
        _rpmHistory.Enqueue((currentRPM, now));

        while (_rpmHistory.Count > 0 && (now - _rpmHistory.Peek().timestamp).TotalMilliseconds > RPMHistoryWindowMs)
            _rpmHistory.Dequeue();

        // Route to mode
        if (_currentMode == AudioMode.FeedbackOptimization)
            UpdateFeedbackOptimizationAudio(currentRPM, threshold, currentGear);
        else if (_currentMode == AudioMode.PerformanceLearning)
            UpdatePerformanceLearningAudio(currentRPM, threshold, currentGear);
        else
            UpdateStandardAudio(currentRPM, threshold, currentGear);
    }

    /// <summary>
    /// Standard mode: Soft chirping with alert pitch
    ///
    /// CHIRP TIMING (maximizes silence):
    /// Far (0-50%): Single chirp every 1200ms
    /// Approaching (50-80%): Single chirp every 800ms
    /// Shift zone (80-100%): Single chirp every 400ms
    ///
    /// Goal: Occasional reminders, not constant sound
    /// </summary>
    private void UpdateStandardAudio(int currentRPM, int threshold, int currentGear)
    {
        float rpmRate = CalculateRPMRate();
        int warningDistance = CalculatePredictiveWarningDistance(rpmRate, _reactionTimeMs);
        int rpmFromThreshold = currentRPM - threshold;

        if (rpmFromThreshold >= -warningDistance)
        {
            float proximityRatio = 1.0f - (Math.Abs(rpmFromThreshold) / (float)warningDistance);

            // Determine chirp and timing - focus on LONG GAPS
            ToneProfile chirp;
            int gapMs;
            int proximityZone;

            if (proximityRatio < 0.50f)
            {
                // Far zone: single chirp, very long gap (maximize silence)
                chirp = _toneStandardFar;
                gapMs = 1200; // 1.2 seconds between chirps
                proximityZone = 0;
            }
            else if (proximityRatio < 0.80f)
            {
                // Approaching: single chirp, medium gap
                chirp = _toneStandardApproaching;
                gapMs = 800; // 0.8 seconds
                proximityZone = 1;
            }
            else
            {
                // Shift zone: single chirp, shorter gap
                chirp = _toneStandardShiftNow;
                gapMs = 400; // 0.4 seconds
                proximityZone = 2;
            }

            DateTime now = DateTime.Now;
            bool shouldChirp = false;

            if (!_isPlaying)
            {
                shouldChirp = true;
            }
            else if (_standardToneEndTime != DateTime.MinValue && now >= _standardToneEndTime)
            {
                // Chirp finished, check if gap elapsed
                TimeSpan timeSinceEnd = now - _standardToneEndTime;
                if (timeSinceEnd.TotalMilliseconds >= gapMs)
                    shouldChirp = true;
            }
            else if (_lastProximityZone != proximityZone && proximityZone > _lastProximityZone)
            {
                // Moved to more urgent zone - chirp immediately
                shouldChirp = true;
            }

            if (shouldChirp)
            {
                PlayTone(chirp);
                _standardToneEndTime = now.AddMilliseconds(chirp.DurationMs);
                _lastProximityZone = proximityZone;
            }
        }
        else
        {
            Stop();
            _standardToneEndTime = DateTime.MinValue;
            _lastProximityZone = -1;
        }
    }

    /// <summary>
    /// Performance Learning mode: Occasional chirps for real-time guidance
    /// Uses fixed 175 RPM tolerance (no adaptive learning in this mode)
    /// </summary>
    private void UpdatePerformanceLearningAudio(int currentRPM, int threshold, int currentGear)
    {
        int warningDistance = 300;
        int rpmFromThreshold = currentRPM - threshold;

        _lastRPMRate = CalculateRPMRate();

        // Only chirp when close to recommended shift point
        if (rpmFromThreshold >= -warningDistance && _recommendedShiftRPM > 0)
        {
            // Use fixed 175 RPM tolerance
            ToneProfile chirp;

            if (currentRPM < _recommendedShiftRPM - 175)
                chirp = _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooEarly : _toneTooEarly;
            else if (currentRPM > _recommendedShiftRPM + 175)
                chirp = _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooLate : _toneTooLate;
            else
                chirp = _currentProfile == AudioProfile.Endurance ? _toneEnduranceOptimal : _toneOptimal;

            // Stop if RPM rate drops (coasting/braking)
            const float RPMRateThresholdToStop = 50f;
            if (_lastRPMRate < RPMRateThresholdToStop)
            {
                Stop();
                _performanceAudioStartTime = DateTime.MinValue;
                return;
            }

            // Single chirp with long gap (1 second)
            if (!_isPlaying || _performanceAudioStartTime == DateTime.MinValue)
            {
                _performanceAudioStartTime = DateTime.Now;
                PlayTone(chirp);
            }
            else if ((DateTime.Now - _performanceAudioStartTime).TotalMilliseconds >= chirp.DurationMs + 1000)
            {
                // Chirp + 1 second gap elapsed, allow next chirp
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
    /// Feedback Optimization mode: SILENT during driving, gentle post-shift feedback only
    /// Uses adaptive tolerance based on driver consistency
    ///
    /// ADAPTIVE TOLERANCE LEARNING (only in Feedback mode):
    /// Tracks last 20 shifts and counts successes (shifts within current tolerance)
    /// - Starts at 175 RPM tolerance
    /// - If 80%+ shifts within 175 RPM window → reduces to 125 RPM
    /// - If 80%+ shifts within 125 RPM window → reduces to 100 RPM
    /// - If shift outside tolerance → immediately bounces back to wider tolerance
    /// - Only learns from correct shifts (when optimal tone would play)
    /// </summary>
    private void UpdateFeedbackOptimizationAudio(int currentRPM, int threshold, int currentGear)
    {
        DateTime now = DateTime.Now;
        double elapsedMs = (now - _shiftStateChangeTime).TotalMilliseconds;

        switch (_shiftEvalState)
        {
            case ShiftEvalState.Idle:
                if (currentGear > _lastGearForShiftDetection && _lastGearForShiftDetection > 0 && _lastGearForShiftDetection < 6)
                {
                    _shiftFromGear = _lastGearForShiftDetection;
                    _shiftToGear = currentGear;

                    if (_rpmHistory.Count > 0)
                        _shiftFromRPM = _rpmHistory.Last().rpm;
                    else
                        _shiftFromRPM = currentRPM;

                    _recommendedShiftRPMAtShift = _recommendedShiftRPM;
                    _shiftEvalState = ShiftEvalState.DetectingGearChange;
                    _shiftStateChangeTime = now;
                }
                break;

            case ShiftEvalState.DetectingGearChange:
                if (currentGear == _shiftToGear)
                {
                    _shiftEvalState = ShiftEvalState.StabilizingNewGear;
                    _shiftStateChangeTime = now;
                }
                else if (elapsedMs > ShiftDetectionTimeoutMs)
                    _shiftEvalState = ShiftEvalState.Idle;
                break;

            case ShiftEvalState.StabilizingNewGear:
                if (elapsedMs >= GearStabilizationMs)
                {
                    _shiftEvalState = ShiftEvalState.EvaluatingShiftQuality;
                    _shiftStateChangeTime = now;
                }
                break;

            case ShiftEvalState.EvaluatingShiftQuality:
                int shiftError = _shiftFromRPM - _recommendedShiftRPMAtShift;

                // Update adaptive tolerance based on this shift
                UpdateAdaptiveTolerance(shiftError);

                // Only chirp if shift was outside adaptive tolerance
                if (Math.Abs(shiftError) > _adaptiveTolerance)
                {
                    ToneProfile feedbackChirp = GetShiftQualityTone(_shiftFromRPM, _recommendedShiftRPMAtShift);
                    PlayTone(feedbackChirp);
                }
                // Otherwise: SILENT = correct shift!

                _shiftEvalState = ShiftEvalState.LockoutPeriod;
                _shiftStateChangeTime = now;
                break;

            case ShiftEvalState.LockoutPeriod:
                if (elapsedMs >= ShiftLockoutMs)
                    _shiftEvalState = ShiftEvalState.Idle;
                Stop();
                break;
        }

        if (_shiftEvalState != ShiftEvalState.EvaluatingShiftQuality)
        {
            if (!_isPlaying || _shiftEvalState == ShiftEvalState.LockoutPeriod)
                Stop();
        }

        _lastGearForShiftDetection = currentGear;
    }

    /// <summary>
    /// Updates adaptive tolerance based on driver shift consistency (Feedback mode only)
    ///
    /// Algorithm:
    /// 1. Check if shift was within current tolerance window
    /// 2. If OUTSIDE tolerance → immediate bounce-back to wider tolerance
    ///    - At 100 RPM → bounce to 125 RPM
    ///    - At 125 RPM → bounce to 175 RPM
    /// 3. If WITHIN tolerance → record success and check consistency
    /// 4. If 80%+ of last 20 shifts are successful → tighten tolerance
    ///    - At 175 RPM with 80%+ success → reduce to 125 RPM
    ///    - At 125 RPM with 80%+ success → reduce to 100 RPM
    ///
    /// This rewards consistent shifting and immediately penalizes errors
    /// </summary>
    private void UpdateAdaptiveTolerance(int shiftError)
    {
        int absError = Math.Abs(shiftError);
        bool withinTolerance = absError <= _adaptiveTolerance;

        // Check for immediate error bounce-back
        if (!withinTolerance)
        {
            // Shift was outside tolerance - bounce back to wider tolerance
            if (_adaptiveTolerance == 100)
            {
                _adaptiveTolerance = 125;
                _shiftSuccessHistory.Clear(); // Reset learning
            }
            else if (_adaptiveTolerance == 125)
            {
                _adaptiveTolerance = 175;
                _shiftSuccessHistory.Clear(); // Reset learning
            }
            // If already at 175, stay there and reset
            else
            {
                _shiftSuccessHistory.Clear();
            }
            return;
        }

        // Shift was within tolerance - record success
        _shiftSuccessHistory.Enqueue(true);

        // Keep only last 20 shifts
        while (_shiftSuccessHistory.Count > ShiftHistorySize)
            _shiftSuccessHistory.Dequeue();

        // Need at least 15 shifts to start tightening tolerance
        if (_shiftSuccessHistory.Count < 15)
            return;

        // Calculate success rate (all recent shifts should be successes if we got here)
        float successRate = _shiftSuccessHistory.Count(s => s) / (float)_shiftSuccessHistory.Count;

        // Tighten tolerance if consistency is high (80%+)
        if (successRate >= 0.80f)
        {
            if (_adaptiveTolerance == 175)
            {
                _adaptiveTolerance = 125;
                _shiftSuccessHistory.Clear(); // Reset to re-prove at new tolerance
            }
            else if (_adaptiveTolerance == 125)
            {
                _adaptiveTolerance = 100;
                _shiftSuccessHistory.Clear(); // Reset to re-prove at new tolerance
            }
            // Already at 100 (tightest), stay there
        }
    }

    private ToneProfile GetShiftQualityTone(int shiftRPM, int recommendedRPM)
    {
        // Determine which tone based on shift timing
        // This is only called when shift is outside tolerance (chirp is played)
        if (shiftRPM < recommendedRPM - _adaptiveTolerance)
            return _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooEarly : _toneTooEarly;
        else
            return _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooLate : _toneTooLate;
    }

    private void PlayTone(ToneProfile tone)
    {
        _waveProvider.SetFrequency(tone.Frequency);
        _waveProvider.SetToneProfile(tone.DurationMs, tone.AttackMs, tone.DecayMs,
                                      tone.DecayLevel, tone.RelativeDbLevel, tone.WaveformType,
                                      tone.GlideFrequencyDelta, tone.GlideDurationMs);
        _waveProvider.SetBeeping(false, 0, 0);

        if (!_isPlaying)
        {
            _waveOut.Play();
            _isPlaying = true;
        }
    }

    /// <summary>
    /// Calculate RPM rate (RPM/second) using linear regression over history
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
    /// PREDICTIVE WARNING DISTANCE CALCULATION
    ///
    /// Goal: Start audio at exactly the right time for straight-line acceleration
    ///
    /// Math:
    /// 1. Time to threshold = (threshold - currentRPM) / rpmRate
    /// 2. Add reaction time compensation (75-125ms configurable)
    /// 3. Add safety margin (50ms for audio latency)
    /// 4. Convert to RPM distance = rpmRate * totalTime
    ///
    /// Example:
    /// - RPM rate = 2000 RPM/sec
    /// - Reaction time = 100ms = 0.1sec
    /// - Safety = 50ms = 0.05sec
    /// - Warning distance = 2000 * (0.1 + 0.05) = 300 RPM
    ///
    /// Adapts automatically:
    /// - Fast accel (high RPM rate) = larger distance (earlier warning)
    /// - Slow accel (low RPM rate) = smaller distance (later warning)
    /// - Pro drivers (75ms) = less lead time
    /// - Average drivers (125ms) = more lead time
    /// </summary>
    private int CalculatePredictiveWarningDistance(float rpmRatePerSecond, int reactionTimeMs)
    {
        float reactionTimeSeconds = reactionTimeMs / 1000.0f;
        float totalCompensationSeconds = reactionTimeSeconds + 0.05f; // +50ms safety
        int warningDistance = (int)(Math.Abs(rpmRatePerSecond) * totalCompensationSeconds);
        return Math.Clamp(warningDistance, 30, 400); // Min 30, Max 400 RPM
    }

    public float GetCurrentRPMRate() => CalculateRPMRate();
    public int GetCurrentWarningDistance() => CalculatePredictiveWarningDistance(CalculateRPMRate(), _reactionTimeMs);

    /// <summary>
    /// Play preview chirps for user selection
    /// </summary>
    public void PlayTonePreview(AudioMode mode, AudioProfile profile)
    {
        ToneProfile tooEarly, optimal, tooLate;

        if (profile == AudioProfile.Endurance)
        {
            tooEarly = _toneEnduranceTooEarly;
            optimal = _toneEnduranceOptimal;
            tooLate = _toneEnduranceTooLate;
        }
        else
        {
            tooEarly = _toneTooEarly;
            optimal = _toneOptimal;
            tooLate = _toneTooLate;
        }

        if (mode == AudioMode.PerformanceLearning || mode == AudioMode.FeedbackOptimization)
        {
            Console.WriteLine("Shift early chirp:");
            PlayTone(tooEarly);
            Thread.Sleep(tooEarly.DurationMs + 500);
            Stop();

            Console.WriteLine("Optimal chirp:");
            PlayTone(optimal);
            Thread.Sleep(optimal.DurationMs + 500);
            Stop();

            Console.WriteLine("Shift late chirp:");
            PlayTone(tooLate);
            Thread.Sleep(tooLate.DurationMs + 500);
            Stop();
        }
        else // Standard mode
        {
            Console.WriteLine("Far zone chirp (800Hz):");
            PlayTone(_toneStandardFar);
            Thread.Sleep(_toneStandardFar.DurationMs + 800);
            Stop();

            Console.WriteLine("Approaching chirp (950Hz):");
            PlayTone(_toneStandardApproaching);
            Thread.Sleep(_toneStandardApproaching.DurationMs + 600);
            Stop();

            Console.WriteLine("Shift zone chirp (1100Hz + glide):");
            PlayTone(_toneStandardShiftNow);
            Thread.Sleep(_toneStandardShiftNow.DurationMs + 400);
            Stop();
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

/// <summary>
/// Wave generator with ADSR envelope, low-pass filter, and micro-glide
/// Optimized for short chirps
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
    private const float BaseAmplitude = 0.15f;

    // Tone profile (ADSR)
    private bool _useToneProfile = false;
    private int _toneDurationSamples = 0;
    private int _attackSamples = 0;
    private int _decaySamples = 0;
    private float _decayLevel = 1.0f;
    private float _relativeDbLevel = 1.0f;
    private int _samplesSinceToneStart = 0;
    private string _waveformType = "triangle";

    // Low-pass filter
    private float _filterState = 0f;
    private const float FilterCutoffHz = 1800f;
    private float _filterAlpha;

    // Micro-glide
    private float _targetFrequency;
    private float _glideRate = 0f;
    private int _glideDurationSamples = 0;
    private int _glideSampleCount = 0;

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

    public TriangleWaveProvider()
    {
        float dt = 1f / WaveFormat.SampleRate;
        _filterAlpha = (2f * MathF.PI * FilterCutoffHz * dt) / (1f + 2f * MathF.PI * FilterCutoffHz * dt);
    }

    public void SetFrequency(float frequency)
    {
        _frequency = frequency;
        _targetFrequency = frequency;
    }

    public void SetToneProfile(int durationMs, int attackMs, int decayMs, float decayLevel,
                                float relativeDbLevel, string waveformType,
                                float glideFreqDelta = 0f, int glideDurationMs = 0)
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

        if (glideFreqDelta != 0f && glideDurationMs > 0)
        {
            _targetFrequency = _frequency + glideFreqDelta;
            _glideDurationSamples = (int)(glideDurationMs * WaveFormat.SampleRate / 1000.0);
            _glideRate = glideFreqDelta / glideDurationMs * (WaveFormat.SampleRate / 1000f);
        }
        else
        {
            _glideRate = 0f;
            _glideDurationSamples = 0;
        }
    }

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

            if (_useToneProfile)
            {
                // ADSR envelope
                float envelopeLevel = 1.0f;

                if (_samplesSinceToneStart < _attackSamples)
                    envelopeLevel = (float)_samplesSinceToneStart / _attackSamples;
                else if (_samplesSinceToneStart < _attackSamples + _decaySamples)
                {
                    int decayProgress = _samplesSinceToneStart - _attackSamples;
                    envelopeLevel = 1.0f - ((1.0f - _decayLevel) * ((float)decayProgress / _decaySamples));
                }
                else
                    envelopeLevel = _decayLevel;

                sample = GenerateWaveform(_waveformType, _phase) * envelopeLevel * _relativeDbLevel * BaseAmplitude;

                // Low-pass filter
                _filterState = (_filterState * (1f - _filterAlpha)) + (sample * _filterAlpha);
                sample = _filterState;

                // Glide
                if (_glideRate != 0f && _glideSampleCount < _glideDurationSamples)
                {
                    _frequency += _glideRate;
                    _glideSampleCount++;
                }

                _samplesSinceToneStart++;
            }
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

                sample = GenerateWaveform("triangle", _phase) * BaseAmplitude;
            }
            else
            {
                sample = GenerateWaveform("triangle", _phase) * BaseAmplitude;
            }

            buffer[offset + i] = sample;

            _phase += _frequency / WaveFormat.SampleRate;
            if (_phase >= 1.0f)
                _phase -= 1.0f;
        }

        return count;
    }

    private float GenerateWaveform(string type, float phase)
    {
        float phaseValue = phase % 1.0f;

        if (type == "sine")
            return MathF.Sin(phaseValue * 2f * MathF.PI);
        else if (type == "rounded")
        {
            float triangle = phaseValue < 0.5f ? (phaseValue * 4f - 1f) : (3f - phaseValue * 4f);
            float sine = MathF.Sin(phaseValue * 2f * MathF.PI);
            return (triangle * 0.6f) + (sine * 0.4f);
        }
        else // triangle
            return phaseValue < 0.5f ? (phaseValue * 4f - 1f) : (3f - phaseValue * 4f);
    }
}
