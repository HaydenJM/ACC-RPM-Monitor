using NAudio.Wave;

namespace ACCRPMMonitor;

/// <summary>
/// Audio engine for shift indication - scientifically designed for straight-line acceleration
/// Maintains non-intrusiveness while providing precise shift timing with reaction time compensation
/// </summary>
public class AudioEngine : IDisposable
{
    private readonly WaveOutEvent _waveOut;
    private readonly TriangleWaveProvider _waveProvider;
    private bool _isPlaying;

    // RPM tracking for rate calculation and prediction
    private readonly Queue<(int rpm, DateTime timestamp)> _rpmHistory = new();
    private const int RPMHistoryWindowMs = 250; // Track last 250ms for accurate rate calc

    // Downshift muting (prevent audio spam on downshift)
    private DateTime _lastDownshiftTime = DateTime.MinValue;
    private int _lastGear = 0;
    private const int DownshiftMuteDurationMs = 200;

    // Human reaction time compensation (configurable: professional 75ms, average 125ms)
    private int _reactionTimeMs = 100; // Default: slightly faster than average

    // Audio modes
    public enum AudioMode
    {
        Standard,                  // Progressive beeping (slow → fast → solid)
        PerformanceLearning,       // Pitch-based real-time guidance
        FeedbackOptimization       // Post-shift feedback only
    }

    public enum AudioProfile
    {
        Normal,      // Standard responsiveness
        Endurance    // Lower-fatigue for long sessions
    }

    private AudioMode _currentMode = AudioMode.Standard;
    private AudioProfile _currentProfile = AudioProfile.Normal;
    private int _recommendedShiftRPM = 0;

    // Tone profiles - frequencies chosen for clarity without fatigue
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

    // Normal profile tones (Performance/Feedback modes)
    private readonly ToneProfile _toneTooEarly = new()
    {
        Frequency = 950f, DurationMs = 130, AttackMs = 5, DecayMs = 120,
        DecayLevel = 0.60f, RelativeDbLevel = 0.707f, WaveformType = "rounded",
        GlideFrequencyDelta = -10f, GlideDurationMs = 100
    };

    private readonly ToneProfile _toneOptimal = new()
    {
        Frequency = 600f, DurationMs = 140, AttackMs = 5, DecayMs = 135,
        DecayLevel = 0.55f, RelativeDbLevel = 1.0f, WaveformType = "sine"
    };

    private readonly ToneProfile _toneTooLate = new()
    {
        Frequency = 400f, DurationMs = 150, AttackMs = 5, DecayMs = 145,
        DecayLevel = 0.50f, RelativeDbLevel = 0.794f, WaveformType = "triangle"
    };

    // Standard mode tones - slightly higher frequencies, more frequent
    private readonly ToneProfile _toneStandardFar = new()
    {
        Frequency = 700f, DurationMs = 90, AttackMs = 5, DecayMs = 80,
        DecayLevel = 0.50f, RelativeDbLevel = 0.707f, WaveformType = "sine"
    };

    private readonly ToneProfile _toneStandardApproaching = new()
    {
        Frequency = 850f, DurationMs = 80, AttackMs = 5, DecayMs = 70,
        DecayLevel = 0.55f, RelativeDbLevel = 0.85f, WaveformType = "sine"
    };

    private readonly ToneProfile _toneStandardShiftNow = new()
    {
        Frequency = 1000f, DurationMs = 100, AttackMs = 5, DecayMs = 90,
        DecayLevel = 0.60f, RelativeDbLevel = 1.0f, WaveformType = "rounded",
        GlideFrequencyDelta = 50f, GlideDurationMs = 80
    };

    // Endurance profile - lower fatigue
    private readonly ToneProfile _toneEnduranceTooEarly = new()
    {
        Frequency = 650f, DurationMs = 110, AttackMs = 8, DecayMs = 130,
        DecayLevel = 0.57f, RelativeDbLevel = 0.707f, WaveformType = "sine",
        GlideFrequencyDelta = -10f, GlideDurationMs = 60
    };

    private readonly ToneProfile _toneEnduranceOptimal = new()
    {
        Frequency = 500f, DurationMs = 130, AttackMs = 10, DecayMs = 100,
        DecayLevel = 0.52f, RelativeDbLevel = 1.0f, WaveformType = "sine"
    };

    private readonly ToneProfile _toneEnduranceTooLate = new()
    {
        Frequency = 400f, DurationMs = 140, AttackMs = 8, DecayMs = 160,
        DecayLevel = 0.48f, RelativeDbLevel = 0.707f, WaveformType = "sine",
        GlideFrequencyDelta = -15f, GlideDurationMs = 120
    };

    // Performance audio tracking
    private DateTime _performanceAudioStartTime = DateTime.MinValue;
    private float _lastRPMRate = 0f;

    // Standard mode audio tracking
    private DateTime _standardToneEndTime = DateTime.MinValue;
    private int _lastProximityZone = -1;

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

    /// <summary>
    /// Set reaction time compensation (75ms = pro, 100ms = good, 125ms = average)
    /// </summary>
    public void SetReactionTimeMs(int ms) => _reactionTimeMs = Math.Clamp(ms, 50, 200);

    /// <summary>
    /// Main update - routes to appropriate audio mode
    /// </summary>
    public void UpdateRPM(int currentRPM, int threshold, int currentGear)
    {
        // Detect downshift and mute briefly
        if (currentGear < _lastGear)
            _lastDownshiftTime = DateTime.Now;
        _lastGear = currentGear;

        // Mute during downshift cooldown
        if ((DateTime.Now - _lastDownshiftTime).TotalMilliseconds < DownshiftMuteDurationMs)
        {
            Stop();
            return;
        }

        // No audio in 6th gear or higher (top gear)
        if (currentGear >= 6)
        {
            Stop();
            return;
        }

        // Never play below 6000 RPM (safety threshold)
        if (currentRPM < 6000)
        {
            Stop();
            return;
        }

        // Track RPM history for rate calculation
        DateTime now = DateTime.Now;
        _rpmHistory.Enqueue((currentRPM, now));

        // Remove old entries outside window
        while (_rpmHistory.Count > 0 && (now - _rpmHistory.Peek().timestamp).TotalMilliseconds > RPMHistoryWindowMs)
            _rpmHistory.Dequeue();

        // Route to mode-specific logic
        if (_currentMode == AudioMode.FeedbackOptimization)
            UpdateFeedbackOptimizationAudio(currentRPM, threshold, currentGear);
        else if (_currentMode == AudioMode.PerformanceLearning)
            UpdatePerformanceLearningAudio(currentRPM, threshold, currentGear);
        else
            UpdateStandardAudio(currentRPM, threshold, currentGear);
    }

    /// <summary>
    /// Standard mode: Pitch-based tones optimized for straight-line acceleration
    /// Non-intrusive with slightly higher frequencies and frequent playback
    ///
    /// PREDICTION MATH:
    /// - Calculate RPM rate: dRPM/dt (RPM per second)
    /// - Predict time to threshold: t = (threshold - currentRPM) / rpmRate
    /// - Compensate for human reaction time (75-125ms configurable)
    /// - Trigger audio when: predictedTime <= reactionTime
    ///
    /// DYNAMIC WARNING DISTANCE:
    /// Higher RPM rate = earlier warning (more distance needed)
    /// Lower RPM rate = later warning (less distance needed)
    ///
    /// TONE PROGRESSION:
    /// Far (0-33%): 700Hz, 90ms, gaps of 400ms = gentle reminder
    /// Approaching (33-66%): 850Hz, 80ms, gaps of 200ms = getting close
    /// Shift now (66-100%): 1000Hz, 100ms, gaps of 100ms = shift now!
    /// </summary>
    private void UpdateStandardAudio(int currentRPM, int threshold, int currentGear)
    {
        // Calculate current RPM rate (RPM/sec)
        float rpmRate = CalculateRPMRate();

        // Calculate dynamic warning distance based on RPM rate and reaction time
        int warningDistance = CalculatePredictiveWarningDistance(rpmRate, _reactionTimeMs);

        int rpmFromThreshold = currentRPM - threshold;

        // Only play when within warning distance (predictive zone)
        if (rpmFromThreshold >= -warningDistance)
        {
            // Calculate proximity ratio (0.0 = far, 1.0 = at threshold)
            float proximityRatio = 1.0f - (Math.Abs(rpmFromThreshold) / (float)warningDistance);

            // Determine tone and timing based on proximity zones
            ToneProfile toneToPlay;
            int gapMs; // Gap between tones
            int proximityZone;

            if (proximityRatio < 0.33f)
            {
                // Far zone: gentle 700Hz tone, longer gaps
                toneToPlay = _toneStandardFar;
                gapMs = 400;
                proximityZone = 0;
            }
            else if (proximityRatio < 0.66f)
            {
                // Approaching zone: 850Hz tone, medium gaps
                toneToPlay = _toneStandardApproaching;
                gapMs = 200;
                proximityZone = 1;
            }
            else
            {
                // Shift now zone: 1000Hz tone with glide, short gaps
                toneToPlay = _toneStandardShiftNow;
                gapMs = 100;
                proximityZone = 2;
            }

            DateTime now = DateTime.Now;

            // Check if we should play a new tone
            bool shouldPlayTone = false;

            if (!_isPlaying)
            {
                // Not currently playing - start new tone
                shouldPlayTone = true;
            }
            else if (_standardToneEndTime != DateTime.MinValue && now >= _standardToneEndTime)
            {
                // Previous tone finished, check if gap elapsed
                TimeSpan timeSinceEnd = now - _standardToneEndTime;
                if (timeSinceEnd.TotalMilliseconds >= gapMs)
                {
                    shouldPlayTone = true;
                }
            }
            else if (_lastProximityZone != proximityZone)
            {
                // Zone changed - play new tone immediately
                shouldPlayTone = true;
            }

            if (shouldPlayTone)
            {
                PlayTone(toneToPlay);
                _standardToneEndTime = now.AddMilliseconds(toneToPlay.DurationMs);
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
    /// Performance Learning mode: Real-time pitch guidance
    /// Different tones indicate shift quality relative to learned optimal point
    /// </summary>
    private void UpdatePerformanceLearningAudio(int currentRPM, int threshold, int currentGear)
    {
        int warningDistance = 300;
        int rpmFromThreshold = currentRPM - threshold;

        _lastRPMRate = CalculateRPMRate();

        // Only play when within warning distance and we have a recommendation
        if (rpmFromThreshold >= -warningDistance && _recommendedShiftRPM > 0)
        {
            // Select tone based on current RPM vs recommended
            ToneProfile toneToPlay;

            if (currentRPM < _recommendedShiftRPM - 175)
                toneToPlay = _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooEarly : _toneTooEarly;
            else if (currentRPM > _recommendedShiftRPM + 175)
                toneToPlay = _currentProfile == AudioProfile.Endurance ? _toneEnduranceTooLate : _toneTooLate;
            else
                toneToPlay = _currentProfile == AudioProfile.Endurance ? _toneEnduranceOptimal : _toneOptimal;

            // Stop audio if RPM rate drops too low (coasting/braking)
            const float RPMRateThresholdToStop = 50f;
            if (_lastRPMRate < RPMRateThresholdToStop)
            {
                Stop();
                _performanceAudioStartTime = DateTime.MinValue;
                return;
            }

            // Start or continue tone
            if (!_isPlaying || _performanceAudioStartTime == DateTime.MinValue)
            {
                _performanceAudioStartTime = DateTime.Now;
                PlayTone(toneToPlay);
            }
            else if ((DateTime.Now - _performanceAudioStartTime).TotalMilliseconds >= toneToPlay.DurationMs)
            {
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
    /// Feedback Optimization mode: SILENT during driving, audio ONLY for post-shift feedback
    /// Good shifts (within ±175 RPM) = silent (correct!)
    /// Bad shifts = tone indicating correction needed
    /// </summary>
    private void UpdateFeedbackOptimizationAudio(int currentRPM, int threshold, int currentGear)
    {
        DateTime now = DateTime.Now;
        double elapsedMs = (now - _shiftStateChangeTime).TotalMilliseconds;

        switch (_shiftEvalState)
        {
            case ShiftEvalState.Idle:
                // Detect upshift (gears 1-5 only)
                if (currentGear > _lastGearForShiftDetection && _lastGearForShiftDetection > 0 && _lastGearForShiftDetection < 6)
                {
                    _shiftFromGear = _lastGearForShiftDetection;
                    _shiftToGear = currentGear;

                    // Capture pre-shift RPM from history
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

                // Only play tone if shift was NOT optimal (±175 RPM tolerance)
                if (Math.Abs(shiftError) > 175)
                {
                    ToneProfile feedbackTone = GetShiftQualityTone(_shiftFromRPM, _recommendedShiftRPMAtShift);
                    PlayTone(feedbackTone);
                }
                // Otherwise silent = good shift!

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

    private ToneProfile GetShiftQualityTone(int shiftRPM, int recommendedRPM)
    {
        if (shiftRPM < recommendedRPM - 175)
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
    /// Calculate RPM rate of change (RPM/second)
    /// Uses linear regression over history window for accuracy
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
    /// Goal: Start audio at EXACTLY the right time for straight-line acceleration
    ///
    /// Math:
    /// 1. Time to reach threshold = (threshold - currentRPM) / rpmRate
    /// 2. Add reaction time compensation (75-125ms configurable)
    /// 3. Convert back to RPM distance = rpmRate * (reactionTimeSeconds + safetyMargin)
    ///
    /// Example:
    /// - RPM rate = 2000 RPM/sec
    /// - Reaction time = 100ms = 0.1sec
    /// - Warning distance = 2000 * 0.1 = 200 RPM
    /// - So audio starts 200 RPM before threshold
    ///
    /// This adapts automatically:
    /// - Fast acceleration (high RPM rate) = larger warning distance (earlier audio)
    /// - Slow acceleration (low RPM rate) = smaller warning distance (later audio)
    /// - Professional drivers (75ms) = less lead time needed
    /// - Average drivers (125ms) = more lead time needed
    /// </summary>
    private int CalculatePredictiveWarningDistance(float rpmRatePerSecond, int reactionTimeMs)
    {
        // Convert reaction time to seconds
        float reactionTimeSeconds = reactionTimeMs / 1000.0f;

        // Add small safety margin (50ms) for audio processing latency
        float totalCompensationSeconds = reactionTimeSeconds + 0.05f;

        // Calculate RPM distance needed
        // distance = rate * time
        int warningDistance = (int)(Math.Abs(rpmRatePerSecond) * totalCompensationSeconds);

        // Clamp to reasonable limits (min 30 RPM, max 400 RPM)
        // Min prevents audio when nearly at redline but coasting
        // Max prevents audio starting way too early on fast cars
        return Math.Clamp(warningDistance, 30, 400);
    }

    public float GetCurrentRPMRate() => CalculateRPMRate();
    public int GetCurrentWarningDistance() => CalculatePredictiveWarningDistance(CalculateRPMRate(), _reactionTimeMs);

    /// <summary>
    /// Play preview of tones for user to hear before selecting
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
            PlayTone(tooEarly);
            Thread.Sleep(tooEarly.DurationMs + 300);
            Stop();

            PlayTone(optimal);
            Thread.Sleep(optimal.DurationMs + 300);
            Stop();

            PlayTone(tooLate);
            Thread.Sleep(tooLate.DurationMs + 300);
            Stop();
        }
        else // Standard mode
        {
            // Demo three proximity zones
            Console.WriteLine("Far zone (700Hz):");
            PlayTone(_toneStandardFar);
            Thread.Sleep(_toneStandardFar.DurationMs + 400);
            Stop();

            Thread.Sleep(300);

            Console.WriteLine("Approaching zone (850Hz):");
            PlayTone(_toneStandardApproaching);
            Thread.Sleep(_toneStandardApproaching.DurationMs + 200);
            Stop();

            Thread.Sleep(300);

            Console.WriteLine("Shift now zone (1000Hz + glide):");
            PlayTone(_toneStandardShiftNow);
            Thread.Sleep(_toneStandardShiftNow.DurationMs + 100);
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
/// Wave generator with ADSR envelope, low-pass filter, and micro-glide support
/// Supports beeping patterns (Standard) and tone profiles (Performance modes)
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

    // Tone profile (ADSR envelope)
    private bool _useToneProfile = false;
    private int _toneDurationSamples = 0;
    private int _attackSamples = 0;
    private int _decaySamples = 0;
    private float _decayLevel = 1.0f;
    private float _relativeDbLevel = 1.0f;
    private int _samplesSinceToneStart = 0;
    private string _waveformType = "triangle";

    // Low-pass filter (removes harshness)
    private float _filterState = 0f;
    private const float FilterCutoffHz = 1800f;
    private float _filterAlpha;

    // Micro-glide (subtle pitch bend)
    private float _targetFrequency;
    private float _glideRate = 0f;
    private int _glideDurationSamples = 0;
    private int _glideSampleCount = 0;

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

    public TriangleWaveProvider()
    {
        // Calculate low-pass filter coefficient
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

        // Setup glide if specified
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

            // Tone profile mode (Performance modes)
            if (_useToneProfile)
            {
                // ADSR envelope
                float envelopeLevel = 1.0f;

                if (_samplesSinceToneStart < _attackSamples)
                {
                    // Attack: 0 → 1
                    envelopeLevel = (float)_samplesSinceToneStart / _attackSamples;
                }
                else if (_samplesSinceToneStart < _attackSamples + _decaySamples)
                {
                    // Decay: 1 → decayLevel
                    int decayProgress = _samplesSinceToneStart - _attackSamples;
                    envelopeLevel = 1.0f - ((1.0f - _decayLevel) * ((float)decayProgress / _decaySamples));
                }
                else
                {
                    // Sustain at decayLevel
                    envelopeLevel = _decayLevel;
                }

                // Generate waveform
                sample = GenerateWaveform(_waveformType, _phase) * envelopeLevel * _relativeDbLevel * BaseAmplitude;

                // Apply low-pass filter
                _filterState = (_filterState * (1f - _filterAlpha)) + (sample * _filterAlpha);
                sample = _filterState;

                // Apply glide
                if (_glideRate != 0f && _glideSampleCount < _glideDurationSamples)
                {
                    _frequency += _glideRate;
                    _glideSampleCount++;
                }

                _samplesSinceToneStart++;
            }
            // Beeping mode (Standard)
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
                // Solid tone
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
    /// Generate waveform: sine, triangle, or rounded (blend)
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
            // Blend triangle + sine for smoother sound
            float triangle = phaseValue < 0.5f ? (phaseValue * 4f - 1f) : (3f - phaseValue * 4f);
            float sine = MathF.Sin(phaseValue * 2f * MathF.PI);
            return (triangle * 0.6f) + (sine * 0.4f);
        }
        else // triangle
        {
            return phaseValue < 0.5f ? (phaseValue * 4f - 1f) : (3f - phaseValue * 4f);
        }
    }
}
