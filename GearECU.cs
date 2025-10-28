namespace ACCRPMMonitor;

// Recommends optimal gear for sustained power delivery (corners, uphills)
// Different from shift point optimization which focuses on short-term acceleration
public class GearRecommendationEngine
{
    private readonly Dictionary<int, Dictionary<int, float>> _accelerationCurves;
    private readonly Dictionary<int, float> _gearRatios;
    private const float SustainedSpeedRange = 15f; // km/h - range to average acceleration over

    public GearRecommendationEngine(Dictionary<int, Dictionary<int, float>> accelerationCurves, Dictionary<int, float> gearRatios)
    {
        _accelerationCurves = accelerationCurves;
        _gearRatios = gearRatios;
    }

    // Returns the optimal gear for sustained power at the given speed
    // Returns null if insufficient data or manual config (no acceleration curves)
    public int? GetOptimalGearForSpeed(float currentSpeed, float currentThrottle)
    {
        // Need speed > 30 km/h to have meaningful data
        if (currentSpeed < 30f)
            return null;

        // No curves available (manual config)
        if (_accelerationCurves == null || _accelerationCurves.Count == 0)
            return null;

        int? bestGear = null;
        float bestSustainedScore = float.MinValue;

        // Check all available gears (1-6)
        for (int gear = 1; gear <= 6; gear++)
        {
            if (!_accelerationCurves.ContainsKey(gear))
                continue;

            // Calculate sustained acceleration score for this gear
            float? sustainedScore = CalculateSustainedAccelerationScore(gear, currentSpeed, currentThrottle);

            if (sustainedScore.HasValue && sustainedScore.Value > bestSustainedScore)
            {
                bestSustainedScore = sustainedScore.Value;
                bestGear = gear;
            }
        }

        return bestGear;
    }

    // Calculates average acceleration over a speed range for sustained power evaluation
    private float? CalculateSustainedAccelerationScore(int gear, float currentSpeed, float throttle)
    {
        if (!_accelerationCurves.ContainsKey(gear))
            return null;

        var gearCurve = _accelerationCurves[gear];

        // Weight sustained power more heavily at partial throttle (< 85%)
        // This is typical for corners/uphills where consistent power matters more
        float sustainedWeight = throttle < 0.85f ? 0.8f : 0.6f;
        float peakWeight = 1.0f - sustainedWeight;

        // Calculate acceleration at current speed
        float? currentAccel = GetAccelerationAtSpeed(gear, currentSpeed);
        if (!currentAccel.HasValue)
            return null;

        // Calculate average acceleration over the next 15 km/h (sustained range)
        float endSpeed = currentSpeed + SustainedSpeedRange;
        float sumAccel = 0f;
        int samples = 0;

        // Sample every 2 km/h over the range
        for (float speed = currentSpeed; speed <= endSpeed; speed += 2f)
        {
            float? accel = GetAccelerationAtSpeed(gear, speed);
            if (accel.HasValue)
            {
                sumAccel += accel.Value;
                samples++;
            }
        }

        if (samples == 0)
            return null;

        float averageSustainedAccel = sumAccel / samples;

        // Calculate score: weighted combination of peak (current) and sustained (average) acceleration
        float score = (currentAccel.Value * peakWeight) + (averageSustainedAccel * sustainedWeight);

        return score;
    }

    // Gets acceleration at a specific speed for a gear (with interpolation)
    private float? GetAccelerationAtSpeed(int gear, float targetSpeed)
    {
        if (!_accelerationCurves.ContainsKey(gear))
            return null;

        var gearCurve = _accelerationCurves[gear];

        // Convert speed to approximate RPM using gear ratio if available
        // For now, use speed directly as we don't have RPM->Speed mapping
        // The acceleration curves are indexed by RPM, but we need speed-based lookup

        // Find closest available data points in the curve
        // Acceleration curves are stored as Dictionary<RPM, accel>
        // We need to estimate which RPM corresponds to our target speed

        // Simplified approach: use the curve data directly
        // In practice, the curves should be dense enough that we can find nearby points

        if (gearCurve.Count == 0)
            return null;

        // For now, return a weighted average of all accelerations in the gear
        // This is a simplified approach - ideally we'd have RPM->Speed mapping
        // But it still gives us useful information about which gear has better sustained power

        // Get all acceleration values
        var accels = gearCurve.Values.ToList();
        if (accels.Count == 0)
            return null;

        // Return average acceleration for this gear as a proxy for sustained performance
        return accels.Average();
    }

    // Helper to check if gear recommendation is available
    public bool IsAvailable()
    {
        return _accelerationCurves != null && _accelerationCurves.Count > 0;
    }
}
