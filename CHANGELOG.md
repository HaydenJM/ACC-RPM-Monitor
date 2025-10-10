# Changelog

All notable changes to ACC RPM Monitor will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

## [Unreleased]

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

## Version History Summary

- **v1.0.0** (2025-10-07) - Initial release with core audio feedback system and configuration management
