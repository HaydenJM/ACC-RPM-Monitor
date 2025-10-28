namespace ACCRPMMonitor;

/// <summary>
/// Detects gear shifts and analyzes shift patterns in relation to lap performance.
/// Correlates shift behavior with lap times and track position to optimize shift points.
/// </summary>
public class PatternShift
{
    private readonly List<ShiftEvent> _shiftHistory = new();
    private readonly List<LapPerformance> _lapHistory = new();
    private readonly Dictionary<int, List<ShiftPerformanceData>> _shiftPerformanceByGear = new();

    // Current state tracking
    private int _lastGear = 0;
    private int _lastRPM = 0;
    private float _lastSpeed = 0;
    private float _lastThrottle = 0;
    private int _currentLapNumber = 0;
    private DateTime _lapStartTime = DateTime.Now;
    private float _offTrackTime = 0;
    private int _offTrackCount = 0;
    private bool _wasOffTrack = false;
    private DateTime _lastUpdate = DateTime.Now;
    private bool _wasCurrentLapValid = false; // Track validity status of lap in progress

    // Shift detection parameters
    private const int MinRPMForShift = 3000; // Ignore shifts below this RPM (downshifts while braking)
    private const float MinThrottleForUpshift = 0.3f; // Must be accelerating for upshift to count

    /// <summary>
    /// Updates the analyzer with current telemetry data. Call this every frame (~20Hz).
    /// </summary>
    public void Update(int gear, int rpm, float throttle, float speed, float normalizedPosition,
                       LapTimingData? lapTiming, bool isOffTrack)
    {
        var now = DateTime.Now;
        float deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Detect gear changes
        if (_lastGear != 0 && gear != _lastGear)
        {
            bool isUpshift = gear > _lastGear;

            // Filter out invalid shifts (neutral, reverse, engine braking)
            if (_lastGear >= 1 && gear >= 1)
            {
                // For upshifts, require minimum throttle
                if (isUpshift && _lastThrottle >= MinThrottleForUpshift && _lastRPM >= MinRPMForShift)
                {
                    RecordShift(isUpshift, _lastGear, gear, _lastRPM, rpm, _lastSpeed, speed,
                               normalizedPosition, _lastThrottle);
                }
                // For downshifts, just check we're above minimum RPM
                else if (!isUpshift && rpm >= MinRPMForShift)
                {
                    RecordShift(isUpshift, _lastGear, gear, _lastRPM, rpm, _lastSpeed, speed,
                               normalizedPosition, _lastThrottle);
                }
            }
        }

        // Track off-track events
        if (isOffTrack && !_wasOffTrack)
        {
            _offTrackCount++;
        }
        if (isOffTrack)
        {
            _offTrackTime += deltaTime;
        }
        _wasOffTrack = isOffTrack;

        // Track lap validity status (this tells us if the CURRENT lap in progress is valid)
        if (lapTiming != null)
        {
            _wasCurrentLapValid = lapTiming.IsCurrentLapValid;
        }

        // Detect lap completion
        if (lapTiming != null && lapTiming.CompletedLaps > _currentLapNumber)
        {
            // When lap completes, use the validity status from BEFORE completion
            CompleteLap(lapTiming, _wasCurrentLapValid);
            _currentLapNumber = lapTiming.CompletedLaps;
            _lapStartTime = now;
            _offTrackTime = 0;
            _offTrackCount = 0;
            // Reset validity for new lap (will be updated in next frame)
            _wasCurrentLapValid = lapTiming.IsCurrentLapValid;
        }

        // Update state
        _lastGear = gear;
        _lastRPM = rpm;
        _lastSpeed = speed;
        _lastThrottle = throttle;
    }

    /// <summary>
    /// Records a shift event with context about the shift quality.
    /// </summary>
    private void RecordShift(bool isUpshift, int fromGear, int toGear, int fromRPM, int toRPM,
                            float fromSpeed, float toSpeed, float trackPosition, float throttle)
    {
        var shiftEvent = new ShiftEvent
        {
            Timestamp = DateTime.Now,
            IsUpshift = isUpshift,
            FromGear = fromGear,
            ToGear = toGear,
            FromRPM = fromRPM,
            ToRPM = toRPM,
            FromSpeed = fromSpeed,
            ToSpeed = toSpeed,
            TrackPosition = trackPosition,
            Throttle = throttle,
            LapNumber = _currentLapNumber
        };

        _shiftHistory.Add(shiftEvent);
    }

    /// <summary>
    /// Completes the current lap and associates all shifts with lap performance.
    /// Uses ACC's is_valid_lap field to determine lap validity.
    /// NOTE: is_valid_lap is reliable in practice/qualifying but less reliable in races.
    /// </summary>
    private void CompleteLap(LapTimingData lapTiming, bool wasLapValid)
    {
        if (_currentLapNumber == 0)
            return; // First lap, no data yet

        // Primary validity check: Use ACC's is_valid_lap field (tracked from previous frame)
        bool isValidByACC = wasLapValid;

        // Secondary validity check: Basic sanity checks on lap time and off-track
        // Off-track limit: 3.0 seconds cumulative (≥50% off track) invalidates lap
        bool isValidByMetrics = lapTiming.LastLapTimeMs < int.MaxValue &&
                                lapTiming.LastLapTimeMs > 0 &&
                                _offTrackTime < 3.0f;

        var lapPerformance = new LapPerformance
        {
            LapNumber = _currentLapNumber,
            LapTime = lapTiming.LastLapTimeMs,
            OffTrackTime = _offTrackTime,
            OffTrackCount = _offTrackCount,
            CompletionTime = DateTime.Now,
            IsValid = isValidByACC && isValidByMetrics, // Both checks must pass
            IsValidByACC = isValidByACC,
            IsValidByMetrics = isValidByMetrics
        };

        _lapHistory.Add(lapPerformance);

        // Associate shifts from this lap with the lap performance
        var lapShifts = _shiftHistory
            .Where(s => s.LapNumber == _currentLapNumber)
            .ToList();

        foreach (var shift in lapShifts)
        {
            // Only analyze upshifts for now
            if (!shift.IsUpshift)
                continue;

            int gear = shift.FromGear;
            if (!_shiftPerformanceByGear.ContainsKey(gear))
                _shiftPerformanceByGear[gear] = new List<ShiftPerformanceData>();

            _shiftPerformanceByGear[gear].Add(new ShiftPerformanceData
            {
                ShiftEvent = shift,
                LapPerformance = lapPerformance
            });
        }
    }

    /// <summary>
    /// Analyzes all collected shift data to find optimal shift points based on actual lap performance.
    /// Returns recommended shift RPMs for each gear.
    /// </summary>
    public Dictionary<int, int> AnalyzeOptimalShiftPoints(int minLapsRequired = 2)
    {
        var optimalShiftPoints = new Dictionary<int, int>();

        // Need enough valid laps to make meaningful recommendations
        var validLaps = _lapHistory.Where(l => l.IsValid).ToList();
        if (validLaps.Count < minLapsRequired)
            return optimalShiftPoints;

        // Analyze each gear
        for (int gear = 1; gear <= 6; gear++)
        {
            if (!_shiftPerformanceByGear.ContainsKey(gear))
                continue;

            var gearShifts = _shiftPerformanceByGear[gear]
                .Where(s => s.LapPerformance.IsValid)
                .ToList();

            if (gearShifts.Count < 5) // Need at least 5 shifts to analyze
                continue;

            // Group shifts by RPM buckets (200 RPM buckets)
            var shiftsByRPM = gearShifts
                .GroupBy(s => (s.ShiftEvent.FromRPM / 200) * 200)
                .Where(g => g.Count() >= 2) // Need at least 2 samples per bucket
                .ToList();

            if (shiftsByRPM.Count == 0)
                continue;

            // Find the RPM bucket with the best average lap performance
            var bestRPMBucket = shiftsByRPM
                .Select(g => new
                {
                    RPM = g.Key,
                    AvgLapTime = g.Average(s => s.LapPerformance.LapTime),
                    AvgOffTrackTime = g.Average(s => s.LapPerformance.OffTrackTime),
                    Count = g.Count(),
                    // Composite score: faster lap time (lower is better) + less off-track time
                    Score = g.Average(s => s.LapPerformance.LapTime + (s.LapPerformance.OffTrackTime * 1000))
                })
                .OrderBy(x => x.Score) // Lower score is better
                .FirstOrDefault();

            if (bestRPMBucket != null)
            {
                optimalShiftPoints[gear] = bestRPMBucket.RPM;
            }
        }

        return optimalShiftPoints;
    }

    /// <summary>
    /// Generates a detailed performance report showing how different shift patterns correlate with lap times.
    /// </summary>
    public ShiftPatternReport GeneratePerformanceReport()
    {
        var report = new ShiftPatternReport
        {
            TotalShifts = _shiftHistory.Count,
            TotalLaps = _lapHistory.Count,
            ValidLaps = _lapHistory.Count(l => l.IsValid),
            GeneratedAt = DateTime.Now
        };

        if (_lapHistory.Count == 0)
            return report;

        // Calculate lap statistics
        var validLaps = _lapHistory.Where(l => l.IsValid).ToList();
        if (validLaps.Count > 0)
        {
            report.BestLapTime = validLaps.Min(l => l.LapTime);
            report.AverageLapTime = (int)validLaps.Average(l => l.LapTime);
            report.TotalOffTrackEvents = validLaps.Sum(l => l.OffTrackCount);
        }

        // Analyze each gear
        foreach (var gearData in _shiftPerformanceByGear)
        {
            int gear = gearData.Key;
            var shifts = gearData.Value.Where(s => s.LapPerformance.IsValid).ToList();

            if (shifts.Count == 0)
                continue;

            var gearReport = new GearShiftReport
            {
                Gear = gear,
                TotalShifts = shifts.Count,
                MinShiftRPM = shifts.Min(s => s.ShiftEvent.FromRPM),
                MaxShiftRPM = shifts.Max(s => s.ShiftEvent.FromRPM),
                AvgShiftRPM = (int)shifts.Average(s => s.ShiftEvent.FromRPM)
            };

            // Group by RPM buckets and show performance correlation
            var bucketAnalysis = shifts
                .GroupBy(s => (s.ShiftEvent.FromRPM / 200) * 200)
                .Where(g => g.Count() >= 2)
                .Select(g => new RPMBucketAnalysis
                {
                    RPM = g.Key,
                    ShiftCount = g.Count(),
                    AvgLapTime = (int)g.Average(s => s.LapPerformance.LapTime),
                    AvgOffTrackTime = g.Average(s => s.LapPerformance.OffTrackTime),
                    PerformanceScore = g.Average(s => s.LapPerformance.LapTime + (s.LapPerformance.OffTrackTime * 1000))
                })
                .OrderBy(b => b.PerformanceScore)
                .ToList();

            gearReport.RPMBuckets = bucketAnalysis;

            if (bucketAnalysis.Count > 0)
            {
                gearReport.OptimalRPM = bucketAnalysis.First().RPM;
            }

            report.GearReports.Add(gearReport);
        }

        return report;
    }

    /// <summary>
    /// Clears all collected data (useful for starting a new session).
    /// </summary>
    public void Clear()
    {
        _shiftHistory.Clear();
        _lapHistory.Clear();
        _shiftPerformanceByGear.Clear();
        _currentLapNumber = 0;
        _offTrackTime = 0;
        _offTrackCount = 0;
    }

    public int GetTotalShifts() => _shiftHistory.Count;
    public int GetTotalLaps() => _lapHistory.Count;
    public int GetValidLaps() => _lapHistory.Count(l => l.IsValid);
}

// Data models for shift pattern analysis

public class ShiftEvent
{
    public DateTime Timestamp { get; set; }
    public bool IsUpshift { get; set; }
    public int FromGear { get; set; }
    public int ToGear { get; set; }
    public int FromRPM { get; set; }
    public int ToRPM { get; set; }
    public float FromSpeed { get; set; }
    public float ToSpeed { get; set; }
    public float TrackPosition { get; set; } // 0.0 to 1.0 along track
    public float Throttle { get; set; }
    public int LapNumber { get; set; }
}

public class LapPerformance
{
    public int LapNumber { get; set; }
    public int LapTime { get; set; } // in milliseconds
    public float OffTrackTime { get; set; } // seconds off track
    public int OffTrackCount { get; set; } // number of times went off track
    public DateTime CompletionTime { get; set; }
    public bool IsValid { get; set; } // Valid if both ACC and metrics checks pass
    public bool IsValidByACC { get; set; } // Validity from ACC's validated_laps field
    public bool IsValidByMetrics { get; set; } // Validity from our sanity checks
}

public class ShiftPerformanceData
{
    public ShiftEvent ShiftEvent { get; set; } = null!;
    public LapPerformance LapPerformance { get; set; } = null!;
}

public class ShiftPatternReport
{
    public int TotalShifts { get; set; }
    public int TotalLaps { get; set; }
    public int ValidLaps { get; set; }
    public int BestLapTime { get; set; }
    public int AverageLapTime { get; set; }
    public int TotalOffTrackEvents { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<GearShiftReport> GearReports { get; set; } = new();

    public string FormatLapTime(int milliseconds)
    {
        if (milliseconds == int.MaxValue)
            return "N/A";

        int totalSeconds = milliseconds / 1000;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        int ms = milliseconds % 1000;

        return $"{minutes}:{seconds:D2}.{ms:D3}";
    }
}

public class GearShiftReport
{
    public int Gear { get; set; }
    public int TotalShifts { get; set; }
    public int MinShiftRPM { get; set; }
    public int MaxShiftRPM { get; set; }
    public int AvgShiftRPM { get; set; }
    public int OptimalRPM { get; set; }
    public List<RPMBucketAnalysis> RPMBuckets { get; set; } = new();
}

public class RPMBucketAnalysis
{
    public int RPM { get; set; }
    public int ShiftCount { get; set; }
    public int AvgLapTime { get; set; }
    public double AvgOffTrackTime { get; set; }
    public double PerformanceScore { get; set; } // Lower is better
}
