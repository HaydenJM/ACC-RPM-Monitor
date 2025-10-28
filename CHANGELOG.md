# Changelog

All notable changes to ACC RPM Monitor will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.4.0] - 2025-10-28

### Added

- **Sophisticated Performance Learning Audio System**: Replaced pitch modulation with carefully-tuned tone profiles
  - **Too Early (950 Hz)**: 130ms duration, 5ms attack, 120ms decay to 60%, -3 dB relative level
    - Sine/rounded triangle waveform
    - Micro-glide ±10 Hz over 100ms for smooth pitch variation
  - **Optimal (600 Hz)**: 140ms duration, 5ms attack, 135ms decay to 55%, 0 dB reference level
    - Pure sine waveform for clarity
  - **Too Late (400 Hz)**: 150ms duration, 5ms attack, 145ms decay to 50%, -2 dB relative level
    - Sine/triangle blend waveform
  - All tones use low-pass filter with gentle roll-off around 1.8 kHz
  - ADSR envelope shaping for natural, non-fatiguing audio

- **Intelligent Audio Stop Conditions**
  - Audio stops when RPM rise rate drops (player stops accelerating)
  - Audio respects tone duration limits (130-150ms per tone)
  - No timer reset on gear changes - cleaner, more intuitive behavior
  - Reduces audio fatigue by avoiding prolonged tones

### Changed

- **Off-Track Detection Threshold**: Changed lap invalidation logic for stricter enforcement
  - Off-track threshold: 0.5f = 50% off track (physical distance metric)
  - Lap invalidation: Now requires ≥3.0 seconds cumulative off-track time (was 2.0 seconds)
  - Tracks total off-track time per lap, not individual instances
  - Aligns with racing regulations allowing brief excursions off-line

- **Performance Learning Mode Audio**: Completely redesigned from pitch modulation to tone-based guidance
  - Removed ±200 Hz pitch modulation system
  - Replaced with distinct, scientifically-designed audio profiles
  - Each tone has unique frequency, duration, and envelope characteristics
  - Provides clearer feedback without listener fatigue

### Technical Implementation

- **Enhanced TriangleWaveProvider**:
  - New ADSR envelope support (Attack-Decay-Sustain-Release)
  - Low-pass filter implementation around 1.8 kHz
  - Frequency micro-glide support for smooth transitions
  - Waveform selection (sine, triangle, rounded triangle blends)
  - Relative amplitude/dB level support

- **Enhanced DynAudioEng**:
  - New tone profile system with all parameters per tone
  - RPM rise rate tracking for intelligent audio stopping
  - Separate audio logic paths for Standard and Performance Learning modes
  - State-based audio control (no persistent timers)

- **Enhanced PatternShift**:
  - Cumulative off-track time tracking (improved from instance-based)
  - Clearer lap validity reporting with off-track metrics

- **Dual Audio Profiles** for Performance Learning and Adaptive Modes
  - **Normal Profile**: Responsive tones with higher frequencies for quick feedback
    - Too Early: 950 Hz with +10 Hz glide over 100ms
    - Optimal: 600 Hz pure sine
    - Too Late: 400 Hz with triangle waveform
  - **Endurance Profile**: Low-fatigue tones for extended sessions
    - Too Early: 650 Hz with +10 Hz glide over 60ms
    - Optimal: 500 Hz pure sine
    - Too Late: 400 Hz with -15 Hz glide over 120ms (gently descending)
  - Users select profile when entering Performance Learning or Adaptive modes
  - Endurance profile uses all-sine waveforms for warmth and reduced fatigue

- **Lowered Minimum Valid Laps Requirement**
  - Changed from 5 valid laps to 2 valid laps to enable faster feedback
  - System provides analysis recommendations after 2 laps
  - Encourages users to continue for more refined results

- **Adaptive Mode Enhanced**
  - Now uses Performance Learning audio system (was Standard beeping)
  - Update interval reduced from 30 seconds to 15 seconds for faster adaptation
  - Same audio profiles available as Performance Learning mode
  - Provides quick, intuitive audio feedback during learning

### Performance & User Experience

- **Audio is now scientifically tuned** for racing feedback without listener fatigue
- **Tones stop naturally** when driver stops accelerating (real-world driving physics)
- **Stricter off-track enforcement** aligns with actual racing regulations (3-second threshold)
- **Cleaner tone design** with proper envelopes prevents jarring audio artifacts
- **Faster feedback cycles**: Adaptive mode updates every 15 seconds, Performance Learning generates analysis with just 2 valid laps
- **User choice**: Endurance profile specifically designed for competitive drivers doing long practice sessions
- **Consistent feedback**: Both Adaptive and Performance Learning modes now use the same intuitive audio guidance system

---

## [3.2.0] - 2025-10-27

### Added

- **Help Menu**: Comprehensive in-app help system accessible from main menu [6]
  - Quick start guide for new users
  - Detailed workflow overview for each feature
  - Audio feedback guide explaining beeping patterns and pitch guidance
  - Configuration storage information
  - Key controls reference
  - Troubleshooting section with common issues and solutions
  - No external documentation required for basic usage

### Changed

- **Main Menu Structure**: Added Help menu option
  - New menu layout: 7 options (was 6)
  - Help is option [6]
  - Exit is now option [7]
  - Help accessible from anywhere via main menu

---

## [1.0.0] - 2025-10-07

### Added
- Initial release of ACC RPM Monitor
- Real-time telemetry reading from ACC shared memory
- Progressive audio feedback system with triangle wave generation
- Two-stage warning system:
  - Rising tone from 300 RPM below shift point
  - Urgent beeping from 100 RPM below shift point
- Per-vehicle configuration management
- Per-gear RPM threshold customization (gears 1-8)
- Interactive console UI for vehicle selection
- Interactive console UI for RPM configuration
- Automatic configuration file management in AppData
- Gear-based frequency mapping for audio feedback
- Real-time status display showing:
  - ACC connection status
  - Current gear (1-8, N/R)
  - Current RPM
  - Configured threshold
  - Distance from shift point
  - Audio frequency during warning phase
- Session state detection (only active during live driving)
- Automatic reconnection on ACC restart
- ESC key to exit application

### Technical Implementation
- Simplified shared memory reader for physics data (gear, RPM)
- Graphics memory reader for session status
- NAudio-based triangle wave audio engine
- JSON configuration persistence
- Multi-vehicle profile support
- ~20Hz telemetry update rate
- 44.1kHz audio sample rate

### Fixed
- Corrected gear numbering display (ACC uses 0=reverse, 1=neutral, 2+=gears)
  - Changed neutral/reverse detection from `< 2` to `<= 1`
  - Added gear display offset to show correct gear numbers (1-8)
  - Updated all gear-dependent calculations to use adjusted values

### Audio Specifications
- Triangle wave synthesis for smooth, non-fatiguing audio
- Base frequencies:
  - Gear 1: 500-600 Hz
  - Gear 2+: Increases by 100 Hz per gear (600-700, 700-800, etc.)
- Beeping pattern: 100ms on / 100ms off
- Volume: 15% amplitude for comfortable listening
- Frequency rises smoothly over 200 RPM range (300 to 100 RPM below threshold)

### Configuration System
- Vehicle profiles stored as JSON in `%LOCALAPPDATA%\ACCRPMMonitor\powercurves\`
- Support for creating, loading, and deleting vehicle configurations
- Default RPM values:
  - Gear 1: 6000 RPM
  - Gear 2: 6500 RPM
  - Gears 3-8: 7000 RPM
- Per-gear customization via interactive menu

### Dependencies
- .NET 6.0 (Windows)
- NAudio 2.2.1
- SharpDX.DirectInput 4.2.0

## [2.0.0] - 2025-10-10

### Added
- **New Main Menu System**: Restructured UI with clear workflow options
  - Create Auto Configuration (Data Collection)
  - Create/Edit Manual Configuration
  - Select & Use Configuration (Start Monitoring)
  - Change Vehicle
  - Exit
  - Application now returns to main menu after completing tasks

- **Dedicated Auto Configuration Workflow**: Separate mode for data collection
  - Step-by-step instructions for hotlap sessions (Monza or Paul Ricard recommended)
  - F1 key controls data collection start/stop
  - Focuses on gears 1-6 for comprehensive shift point detection
  - Immediate analysis and feedback after collection
  - Option to retry collection if data insufficient
  - Returns to main menu when complete

- **Comprehensive Data Collection Reports**: Detailed analysis of every auto-config session
  - JSON and human-readable text reports saved for each session
  - Per-gear analysis showing:
    - Total data points collected
    - Full-throttle data points
    - RPM and speed ranges observed
    - Optimal shift point detected
    - Confidence score with detailed explanation
    - RPM distribution histograms
  - Overall success/failure status
  - Specific recommendations for improvement
  - Reports saved to `%LocalAppData%/ACCRPMMonitor/reports/`
  - Explains how confidence scores are calculated
  - Shows exactly what the application looks for in data

- **Enhanced Confidence Scoring System**: Transparent shift point validation
  - Requires all gears 1-6 to have valid shift points
  - Minimum confidence threshold of 0.50 (50 data points)
  - Confidence levels:
    - < 50 points: 0.00 (Insufficient data)
    - 50-99 points: 0.50 (Low confidence)
    - 100-199 points: 0.75 (Medium confidence)
    - 200+ points: 1.00 (High confidence)
  - Clear explanations for each gear's score
  - Automatic removal of low-confidence gears with retry option

- **Dynamic Audio Warning System**: Audio warning timing now adapts based on RPM rate of change
  - Very fast increase (>1500 RPM/sec): Beeps 400 RPM early
  - Fast increase (>1000 RPM/sec): Beeps 300 RPM early
  - Moderate-fast increase (>600 RPM/sec): Beeps 250 RPM early
  - Moderate increase (>300 RPM/sec): Beeps 200 RPM early
  - Slow-moderate increase (>150 RPM/sec): Beeps 150 RPM early
  - Slow increase (>50 RPM/sec): Beeps 100 RPM early
  - Very slow/stable: Beeps 50 RPM early
  - Prevents late warnings in lower gears where RPMs climb quickly

- **Automatic Optimal Shift Point Detection**: System learns best shift points from your driving
  - Analyzes telemetry data during full-throttle acceleration
  - Calculates optimal upshift RPM per gear (lowest RPM achieving max speed)
  - Saves vehicle-specific auto-generated configs
  - Detailed reporting on data quality and confidence

- **Automatic Vehicle Detection**: Detects current car from ACC and loads appropriate config
  - Reads vehicle name from ACC static shared memory
  - Auto-switches to correct vehicle configuration
  - Falls back to manual selection if detection fails

- **Dual Configuration Mode System**:
  - **Manual Mode**: Traditional user-defined RPM values (fully editable)
  - **Auto Mode**: Uses learned optimal shift points (read-only)
  - Easy switching between modes via menu
  - Separate config files for each mode per vehicle

### Changed
- **Audio System Completely Redesigned**:
  - Removed rising tone phase entirely
  - Only urgent beeping alert remains
  - Dynamic timing logic now applied to beeping (was applied to rising tone)
  - Beeping frequency is static per gear (no frequency ramping)
  - Beeping start point adapts to RPM acceleration rate
  - Simpler, more predictable audio behavior

- **Audio Frequencies**: Fixed gear 2 to use its own frequency range (600-700 Hz) instead of sharing with gear 1
  - Gear 1: 500 Hz
  - Gear 2: 600 Hz
  - Gear 3: 700 Hz
  - Gear 4+: Each subsequent gear increases by 100 Hz
  - This ensures each gear has a distinct audio cue for better feedback

- **Program Flow**: Complete restructure of main application loop
  - Menu-driven interface replaces linear flow
  - Monitoring mode separated into dedicated function
  - Removed data collection from monitoring mode
  - Cleaner separation of concerns
  - Auto-creates default config if none exists

- Enhanced status display with RPM rate and dynamic beeping distance
- Config files now support metadata (last updated, data points, confidence levels)
- Updated console output to show "Beep Dist" instead of "Warning Dist"

### Removed
- Removed redundant `ACCSharedMemory.cs` file (project uses the simplified version)
- Removed F1 data collection toggle from monitoring mode (now has dedicated workflow)
- Removed automatic config updates during monitoring

### Technical Details
- New `DataCollectionReport` class for comprehensive session reporting
- New `AutoConfigWorkflow` class for dedicated data collection mode
- New `OptimalShiftAnalyzer.GenerateDetailedReport()` for analysis transparency
- New `MainMenuChoice` enum for menu navigation
- New `VehicleDetector` class for automatic car identification
- Enhanced `OptimalShiftAnalyzer` with confidence scoring explanations
- Updated `DynamicAudioEngine` with simplified audio logic and adaptive beeping
- Enhanced `GearRPMConfig` with auto-generation metadata
- Updated `ConfigManager` to handle dual-mode configurations
- Refactored `Program.cs` into menu-driven architecture

### Planned Features
- GUI interface option
- DirectInput button integration for profile switching
- Visual indicators for multi-monitor setups
- Telemetry data logging and export
- Lap time analysis
- Optimal shift point suggestions based on power curves
- Custom audio profiles (different wave types, volumes)
- Pit limiter detection
- Fuel consumption monitoring

### Known Issues
- None currently reported

---

## [2.1.0] - 2025-10-19

### Added
- **Open Config Folder Menu Option**: New main menu option to open the configuration/reports folder in Windows Explorer
  - Quickly access power curve graphs, reports, and configuration files
  - Creates folder if it doesn't exist
  - Shows full path for easy navigation

- **Power Curve Graph Generation**: Automatically generates professional PNG graphs after successful auto-configuration
  - 1920x1080 high-resolution graphs
  - Shows acceleration curves for gears 1-5 with color-coded lines
  - Displays calculated gear ratios in annotation box
  - Marks optimal shift points with diamond markers
  - Timestamped filenames for easy tracking
  - Saved to `%LocalAppData%/ACCRPMMonitor/reports/`

- **Per-Gear Data Collection Display**: Real-time visibility into data collection progress
  - Shows individual gear data point counts: `G1:32 G2:28 G3:41 G4:26 G5:15`
  - Displays total data points collected
  - Clear status messages explaining why data is/isn't being collected
  - Shows exact throttle percentage and speed values
  - Diagnostic info showing ACC gear vs display gear

### Changed
- **Adaptive Mode Reverted to Time-Based Updates**: Changed from lap-based to 30-second interval updates
  - Updates shift points every 30 seconds based on live data
  - No longer requires completing laps
  - User manually exits when satisfied with configuration
  - More flexible for track configuration and testing

- **Audio System Redesigned - Steady Tone with Volume Ramping**: Replaced beeping pattern with smooth volume control
  - **Steady continuous tone** instead of on/off beeping
  - Volume gradually increases from quiet to full as RPM approaches threshold
  - Volume calculation: `volumePercent = 1.0 - (distance_from_threshold / warning_distance)`
  - More natural and less distracting audio feedback
  - Each gear still has distinct frequency (500Hz + gear * 100Hz)

- **Hard-Coded 6000 RPM Minimum Threshold**: Audio will never play below 6000 RPM
  - Prevents audio at low RPMs where shifting is never beneficial
  - Overrides all other threshold calculations
  - Applies to all gears and modes

- **Relaxed Data Collection Thresholds**: Made data collection more practical for real-world use
  - **Throttle threshold**: Lowered from 95% → 85% (more realistic for sim racing)
  - **Minimum data points**: Lowered from 50 → 30 per gear (faster config generation)
  - **Speed filter**: Changed from 0 km/h → 5 km/h (allows early acceleration data)
  - **Confidence scoring**: Adjusted to match new thresholds
    - 30-59 points: 60% confidence (Acceptable)
    - 60-119 points: 80% confidence (Good)
    - 120+ points: 100% confidence (High)

### Fixed
- **Critical Data Collection Bug**: Fixed gear validation preventing any data from being collected
  - Changed `if (displayGear < 6)` to `if (displayGear >= 1 && displayGear <= 5)`
  - Was allowing invalid gears (0, -1, 6+) to be processed
  - Now correctly validates gears 1-5 only
  - Applied fix to both Auto Config Workflow and Adaptive Mode

- **Speed Reading Bug**: Fixed incorrect shared memory offset causing speed to read as 0.0 km/h
  - Changed speed offset from 24 → 28 bytes in ACC physics memory structure
  - Was reading SteerAngle instead of SpeedKmh
  - Speed now displays correctly while moving
  - Fixes data collection filtering that was rejecting all data

- **Lap Count Reading**: Added proper lap completion tracking from ACC graphics memory
  - Reads completed laps from offset 76 in graphics shared memory
  - Enables lap-based features and statistics

### Technical Details
- Added ScottPlot 5.1.57 dependency for graph generation
- New `PowerCurveGraphGenerator` class for creating power curve visualizations
- New `GetDataPointCountForGear()` method in OptimalShiftAnalyzer for per-gear diagnostics
- Updated `ACCSharedMemorySimple.ReadFullTelemetry()` with correct speed offset (28 bytes)
- New `ACCSharedMemorySimple.ReadCompletedLaps()` method for lap tracking
- Enhanced `DynamicAudioEngine.UpdateRPM()` with volume ramping logic
- Added `SetVolume()` method to TriangleWaveProvider
- Changed TriangleWaveProvider amplitude calculation to use volume multiplier

### Dependencies Updated
- Added: ScottPlot 5.1.57 (graph generation)

---

## [3.0.0] - 2025-10-25

### Added - Machine Learning & Performance-Based Optimization

- **Performance Learning Mode**: Revolutionary performance-driven shift optimization that learns from actual lap performance
  - Combines physics-based acceleration analysis with real-world lap time correlation
  - Automatically detects gear shifts and tracks shift patterns
  - Correlates shift RPMs with lap times and off-track events
  - Continuously refines shift points to maximize lap performance
  - Adaptive learning rate (increases confidence with more data)
  - Real-time recommendations: "Try shifting 200 RPM earlier for better performance"

- **Intelligent Shift Detection System**: Automatically identifies and analyzes every gear change
  - Detects upshifts during acceleration (requires 30%+ throttle, >3000 RPM)
  - Detects downshifts while braking (filtered to exclude engine braking)
  - Records shift context: RPM, speed, throttle position, track location
  - Associates shifts with lap performance outcomes
  - Filters out invalid shifts (neutral, reverse, standing starts)

- **Lap Performance Tracking**: Comprehensive lap-by-lap analysis
  - Tracks lap times from ACC telemetry (parses MM:SS.mmm format)
  - Monitors off-track events and duration
  - Validates laps based on completion time and track adherence
  - Associates all shifts in a lap with that lap's performance
  - Builds historical database of lap performance

- **Shift Pattern Analysis Engine**: Correlates shift behavior with racing outcomes
  - Groups shifts by RPM buckets (200 RPM ranges)
  - Calculates average lap time for each shift RPM range
  - Identifies optimal shift points based on actual performance
  - Composite scoring: lap time + (off-track time × 1000ms penalty)
  - Requires minimum 5 valid laps for reliable recommendations
  - Per-gear analysis showing shift patterns vs. performance

- **Weighted Learning Algorithm**: Blends physics and performance data intelligently
  - **40% weight**: Physics-based acceleration analysis (theoretical optimal)
  - **60% weight**: Performance-based learning (actual results)
  - Adaptive learning rate: Conservative start (20%) → Aggressive (80% at 20+ laps)
  - Handles missing data gracefully (falls back to available source)
  - Confidence increases with data quantity

- **Comprehensive Performance Reports**: Detailed analysis saved after each session
  - **JSON format**: Structured data for programmatic analysis
  - **Human-readable text**: Detailed insights and recommendations
  - Per-gear shift statistics (min/max/avg RPM)
  - RPM bucket performance breakdown showing lap times and scores
  - Physics vs Performance comparison with interpretation
  - Overall session summary with learning metrics
  - Reports saved to `%LocalAppData%/ACCRPMMonitor/shift_analysis/`

- **Enhanced Telemetry Reading**: Expanded ACC shared memory integration
  - `ReadLapTiming()`: Current, last, and best lap times + completed lap count
  - `ReadPosition()`: Local position (X,Y,Z) and normalized track position (0.0-1.0)
  - Off-track detection using vertical position changes
  - Wide string parsing for ACC's Unicode time format
  - Lap time parsing to milliseconds for performance comparison

- **Real-Time Learning Display**: Live feedback during Performance Learning Mode
  - Total laps completed and valid laps analyzed
  - Total shifts recorded and categorized
  - Current learning rate percentage
  - Data quality assessment (Building/Good/Excellent)
  - Per-gear shift point recommendations with confidence
  - Live status showing optimal vs. current shift points

### Changed

- **Monitor Mode Selection**: Added third option for Performance Learning Mode
  - Standard Mode: Fixed shift points
  - Adaptive Mode: Continuous acceleration-based learning
  - **Performance Learning Mode**: Performance-driven lap time optimization

- **Main Monitor Loop Enhanced**: Performance mode includes comprehensive telemetry
  - Reads lap timing, position, and shift data simultaneously
  - Updates learning models every 15 seconds
  - Provides real-time recommendations during driving
  - Generates final report at session end

### Technical Implementation

- **New Classes**:
  - `ShiftPatternAnalyzer`: Detects shifts, tracks laps, correlates performance
  - `PerformanceLearningEngine`: Machine learning algorithm for shift optimization
  - `ShiftPatternReportGenerator`: Creates JSON and text performance reports
  - `LapTimingData`: Parses and stores lap timing information
  - `PositionData`: Tracks car position for off-track detection

- **New Data Models**:
  - `ShiftEvent`: Captures shift details (gears, RPMs, speed, position, throttle)
  - `LapPerformance`: Stores lap metrics (time, off-track data, validity)
  - `ShiftPerformanceData`: Links shifts to lap outcomes
  - `ShiftPatternReport`: Aggregates session-wide shift analysis
  - `LearningReport`: Compares physics vs performance recommendations
  - `ShiftPointRecommendation`: Real-time guidance for driver

- **Enhanced ACCSharedMemorySimple**:
  - Added `ReadLapTiming()` for comprehensive timing data
  - Added `ReadPosition()` for spatial tracking
  - Added `ParseTimeString()` helper for Unicode time parsing
  - Improved memory offsets for accurate data reading

- **Algorithm Details**:
  - Shift detection: Compares consecutive gear readings with validation filters
  - Off-track detection: Vertical position delta threshold (0.5 units)
  - RPM bucketing: 200 RPM ranges for statistical grouping
  - Performance scoring: `lap_time_ms + (off_track_seconds × 1000)`
  - Learning rate formula: `min(0.8, 0.2 + valid_laps / 25)`
  - Weighted blending: `(physics × 0.4 + performance × 0.6) / total_weight`

### User Controls (Performance Learning Mode)

- **ESC**: Exit session and optionally save learned configuration
- **F2**: Immediately save current learned shift points
- **F3**: Generate and save performance report during session

### Performance Benefits

- **Optimizes for real lap times** instead of theoretical acceleration
- **Adapts to driving style** (conservative vs aggressive shifting)
- **Accounts for track-specific factors** (elevation, grip, tire wear)
- **Learns from mistakes** (penalizes shifts that lead to off-track)
- **Converges on optimal points** (more laps = higher confidence)

### Example Workflow

1. Start Performance Learning Mode
2. Complete 3-5 clean laps with varied shift points
3. System analyzes: "Gear 3 performs best at 7200-7400 RPM"
4. Recommendation: "↓ Try shifting 150 RPM earlier for better performance"
5. Adjust shifting, complete more laps
6. Save learned configuration when satisfied
7. Review detailed report showing correlation between shifts and lap times

### Dependencies

- No new external dependencies required
- Uses existing .NET libraries for JSON serialization

### Known Limitations

- Off-track detection simplified (vertical position change only)
- Requires minimum 3-5 valid laps for meaningful learning
- Performance data resets each session (no persistent learning yet)
- Track position data may vary by circuit layout

### Future Enhancements

- Persistent learning database across sessions
- Track-specific shift point profiles
- Corner-by-corner shift analysis using track position
- Integration with telemetry logging systems
- Export learned data to external tools
- Visual performance graphs and heatmaps

---

## [3.1.0] - 2025-10-26

### Added - Enhanced Audio System, Lap Validation & Vehicle Detection

- **Validated Lap Tracking**: Proper lap validity detection using ACC's shared memory fields
  - Reads `graphics.validated_laps` (offset 112) to determine lap validity
  - Implements recommended pattern: `completed_laps` vs `validated_laps` comparison
  - Dual validation system: ACC's validation + sanity checks (both must pass)
  - New `WasLastLapValid()` method for easy validity checking
  - Added `IsValidByACC` and `IsValidByMetrics` flags to `LapPerformance` for diagnostics
  - Note: validated_laps may be unreliable during race sessions (works best in practice/qualifying)

- **Mode-Specific Audio System**: Two distinct audio feedback strategies
  - **Standard/Adaptive Mode**: Progressive beeping system
    - Far from threshold: Slow beeps (500ms on/off)
    - Approaching: Beeps accelerate progressively
    - Close to threshold: Fast beeps (50ms on/off)
    - At threshold: Solid tone
    - Intuitive rhythm that speeds up naturally

  - **Performance Learning Mode**: Pitch-based guidance
    - **High pitch** (+200 Hz): You're shifting too late → shift earlier
    - **Normal pitch**: Shifting at optimal point
    - **Low pitch** (-200 Hz): You're shifting too early → shift later
    - Solid tone (no beeping) for cleaner guidance
    - Real-time display shows pitch meaning

- **Audio Mode System**: `AudioMode` enum with `Standard` and `PerformanceLearning` modes
  - `SetMode()`: Configure audio behavior per monitoring mode
  - `SetRecommendedShiftRPM()`: Feed learning recommendations to audio engine
  - Automatic mode switching based on selected monitor mode

- **Vehicle Detection in Change Vehicle Menu**: Automatic car detection now integrated into vehicle selection
  - Shows detected vehicle with [detected] marker in vehicle list
  - One-click auto-select with [A] option
  - Re-detects vehicle each time menu is opened
  - Verified offset 68 from ACC SDK (ebnerdm/accshm repository)
  - Reads CarModel as `[33]uint16` (66 bytes) UTF-16 encoded string

- **RPM Rising Filter**: Prevents corner throttle data contamination
  - Only collects data when RPMs rising ≥100 RPM/sec
  - Filters out high-throttle corner maintenance (sustained RPM)
  - Ensures shift points calculated from actual acceleration, not corner speed

- **Optimal Shift Window Expanded**: Pitch guidance now uses ±175 RPM window (increased from ±100 RPM)
  - Larger "optimal zone" for more forgiving feedback
  - High pitch only when >175 RPM above optimal
  - Low pitch only when >175 RPM below optimal
  - Normal pitch within ±175 RPM range

### Changed

- **Audio System Completely Redesigned**:
  - **Removed volume modulation** - constant amplitude (0.15f) across all modes
  - Volume no longer ramps up as RPM approaches threshold
  - Replaced with **frequency-based** (beep speed) or **pitch-based** guidance
  - Each mode optimized for its specific use case

- **Progressive Beeping Implementation**:
  - Beep timing calculated based on proximity ratio (0.0 = far, 1.0 = at threshold)
  - Smooth acceleration from 500ms intervals to 50ms intervals
  - Equal on/off duration for consistent rhythm
  - Becomes solid tone exactly at threshold

- **Performance Learning Display Enhanced**:
  - Shows audio pitch indicator: "HIGH (shift earlier)" / "LOW (shift later)" / "NORMAL (optimal)"
  - Clearer feedback about what the audio is telling you
  - Recommendation message + audio pitch work together

- **Exit Controls Clarified**:
  - Performance Learning Mode: "ESC - Return to main menu (prompts to save)"
  - Clear indication that ESC goes back to menu, not just exit app
  - Consistent behavior across all modes

- **Console Window Size**: Adjusted to 82x60 (slightly larger than 80-character menu title)
  - Better fit for menu display
  - Prevents text wrapping on title bars

### Technical Implementation

- **Enhanced `DynamicAudioEngine`**:
  - New `AudioMode` enum: `Standard` vs `PerformanceLearning`
  - New `SetMode(AudioMode)` method
  - New `SetRecommendedShiftRPM(int)` method
  - Split audio logic: `UpdateStandardAudio()` vs `UpdatePerformanceLearningAudio()`
  - Removed volume ramping logic entirely

- **Enhanced `TriangleWaveProvider`**:
  - Removed `SetVolume()` method (no longer needed)
  - Constant amplitude: 0.15f
  - `SetBeeping(bool, int, int)` now takes millisecond timing parameters
  - Dynamically calculates sample counts from milliseconds

- **Enhanced `LapTimingData`**:
  - Added `ValidatedLaps` property
  - Added `WasLastLapValid()` method
  - Proper parsing of `graphics.numberOfLaps` field

- **Enhanced `ShiftPatternAnalyzer.CompleteLap()`**:
  - Uses `lapTiming.WasLastLapValid()` as primary validity check
  - Combines ACC validation with metric validation
  - Stores both validity flags for diagnostics
  - Added documentation about race session unreliability

- **Enhanced `VehicleDetector`**:
  - Removed unused ACCStatic struct (now uses direct memory reading)
  - Verified CarModel offset 68 from ACC SDK
  - Reads 66 bytes as Unicode string
  - Integrated into ConfigUI.ShowVehicleSelectionMenu()

- **Enhanced `OptimalShiftAnalyzer.AddDataPoint()`**:
  - Added RPM rate tracking (_lastRPM, _lastDataPointTime)
  - Calculates RPM/second between samples
  - Only collects when RPMs rising ≥100 RPM/sec
  - Prevents corner throttle contamination

### Performance & User Experience

- **More Intuitive Audio Feedback**:
  - Progressive beeping feels natural and predictable
  - Pitch guidance provides instant feedback without looking at screen
  - Constant volume prevents audio fatigue

- **More Reliable Lap Validation**:
  - Uses ACC's built-in validation system
  - Dual validation prevents false positives
  - Clear diagnostic flags for troubleshooting

- **Better Learning Quality**:
  - Only valid laps contribute to learning
  - More accurate performance correlation
  - Better shift point recommendations

### Known Improvements

- Audio feedback is now clearer and less fatiguing
- Pitch-based guidance makes Performance Learning mode more intuitive with ±175 RPM optimal zone
- Progressive beeping provides better timing awareness in Standard/Adaptive modes
- Validated laps ensure high-quality learning data
- RPM rising filter eliminates corner throttle contamination
- Vehicle detection now fully integrated and functional
- Larger console window prevents menu text wrapping

### Bug Fixes

- Fixed corner throttle data collection issue (high throttle but not accelerating)
- Fixed VehicleDetector not being used after initial startup
- Corrected pitch guidance logic (HIGH=earlier, LOW=later)
- Improved lap validation with fallback heuristics

---

## [3.1.1] - 2025-10-26

### Fixed - Qualifying Lap Detection

- **Critical Fix: Qualifying Lap Validation**: Fixed issue where valid qualifying laps were not being detected or counted
  - Implemented proper `is_valid_lap` field reading from ACC shared memory (offset 1408)
  - Field indicates if the current lap in progress is valid (works reliably in practice/qualifying)
  - Added state tracking in `ShiftPatternAnalyzer` to capture validity before lap completion
  - `_wasCurrentLapValid` now tracks lap validity throughout the lap
  - Validity status captured just before lap completes and passed to `CompleteLap()`
  - Works correctly in qualifying sessions where lap validation is consistent

### Changed

- **LapTimingData Structure Updated**:
  - Removed: `ValidatedLaps`, `LastLapWasInvalidated` (unreliable heuristics)
  - Added: `IsCurrentLapValid` (bool) - direct read from ACC shared memory
  - Added: `LastLapTimeMs` (int) - last lap time in milliseconds for direct comparison
  - Added: `SessionType` (int) - session type for context-aware validation
  - Simplified to focus on current lap validity rather than historical counts

- **ReadLapTiming() Method Redesigned**:
  - Reads from correct ACC graphics memory offsets based on SPageFileGraphic structure
  - Wide strings read as 30 bytes (15 wchar_t characters) instead of 32 bytes
  - `is_valid_lap` read directly from offset 1408
  - Increased memory view size to 2048 bytes to safely access later offsets
  - Removed heuristic-based lap validation logic

- **ShiftPatternAnalyzer.CompleteLap() Enhanced**:
  - Now takes `bool wasLapValid` parameter from tracked state
  - Uses ACC's `is_valid_lap` field as primary validation (tracked from previous frame)
  - Secondary validation: lap time sanity checks (> 0ms, < max value, < 2sec off-track)
  - Both validations must pass for lap to be considered valid
  - Added documentation noting reliability in practice/qualifying vs races

### Technical Implementation

- **Lap Validity State Tracking**:
  - Added `_wasCurrentLapValid` field to `ShiftPatternAnalyzer`
  - Updated every frame with current lap's validity status
  - Captured state used when lap completes (before it resets for next lap)
  - Ensures correct validity attribution to completed laps

- **ACC Graphics Memory Structure** (from ACC SDK):
  ```
  Offset 12:   current_time_str (wchar_t[15] = 30 bytes)
  Offset 42:   last_time_str (wchar_t[15] = 30 bytes)
  Offset 72:   best_time_str (wchar_t[15] = 30 bytes)
  Offset 132:  completed_lap (i32)
  Offset 144:  last_time (i32, milliseconds)
  ...
  Offset 1408: is_valid_lap (i32, boolean flag)
  ```

- **Debug Output Added**:
  - Shows `IsCurrentLapValid` status in Performance Learning Mode
  - Displays last lap time in both string and millisecond formats
  - Shows current lap time for live tracking
  - Helps diagnose lap validation issues during sessions

### Notes

- The `is_valid_lap` field is documented to be reliable in practice/qualifying sessions
- In race sessions, lap validation may use different criteria and be less consistent
- This fix specifically addresses the reported issue with qualifying lap detection
- Users should now see valid qualifying laps properly detected and counted

---

## Version History Summary

- **v3.2.0** (2025-10-27) - In-app help menu with comprehensive usage guide and troubleshooting
- **v3.1.1** (2025-10-26) - **Critical Fix**: Qualifying lap validation now properly detects valid laps
- **v3.1.0** (2025-10-26) - Enhanced audio system with progressive beeping and pitch guidance, validated lap tracking
- **v3.0.0** (2025-10-25) - **MAJOR RELEASE**: Machine learning performance optimization, shift pattern analysis, and intelligent recommendations
- **v2.1.0** (2025-10-19) - Major improvements to data collection, audio system, and diagnostics
- **v2.0.0** (2025-10-10) - Auto-configuration workflow, adaptive mode, and reporting system
- **v1.0.0** (2025-10-07) - Initial release with core audio feedback system and configuration management
