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
- **Dynamic Audio Warning System**: Audio warning timing now adapts based on RPM rate of change
  - Fast RPM increase (>500 RPM/sec): Warns 500+ RPM early
  - Moderate increase (200-500 RPM/sec): Standard 300 RPM warning
  - Slow increase (<200 RPM/sec): Warns only 200 RPM early
  - Prevents late warnings in lower gears where RPMs climb quickly

- **Automatic Optimal Shift Point Detection**: System learns best shift points from your driving
  - Analyzes telemetry data during full-throttle acceleration
  - Calculates optimal upshift RPM per gear (lowest RPM achieving max speed)
  - Auto mode: Automatically collects data and updates configuration
  - Manual mode: Optional data collection via F1 key toggle
  - Saves vehicle-specific auto-generated configs

- **Automatic Vehicle Detection**: Detects current car from ACC and loads appropriate config
  - Reads vehicle name from ACC static shared memory
  - Auto-switches to correct vehicle configuration
  - Falls back to manual selection if detection fails

- **Dual Configuration Mode System**:
  - **Manual Mode**: Traditional user-defined RPM values (fully editable)
  - **Auto Mode**: Uses AI-learned optimal shift points (read-only, auto-updates)
  - Easy switching between modes via startup menu
  - Separate config files for each mode per vehicle

### Changed
- **Audio Frequencies**: Fixed gear 2 to use its own frequency range (600-700 Hz) instead of sharing with gear 1
  - Gear 1: 500-600 Hz (unchanged)
  - Gear 2: 600-700 Hz (now unique)
  - Gear 3: 700-800 Hz (shifted up from 600-700)
  - Gear 4+: Each subsequent gear increases by 100 Hz
  - This ensures each gear has a distinct audio cue for better feedback

- Replaced `AudioEngine` with `DynamicAudioEngine` for adaptive warning timing
- Enhanced status display with RPM rate and dynamic warning distance
- Config files now support metadata (last updated, data points, confidence levels)

### Removed
- Removed redundant `ACCSharedMemory.cs` file (project uses the simplified version)

### Technical Details
- New `OptimalShiftAnalyzer` class for shift point calculation
- New `VehicleDetector` class for automatic car identification
- New `DynamicAudioEngine` with RPM rate tracking (200ms window)
- Enhanced `GearRPMConfig` with auto-generation metadata
- Updated `ConfigManager` to handle dual-mode configurations
- F1 key toggles data collection during driving

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
