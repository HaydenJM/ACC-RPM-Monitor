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

### Changed
- **Audio Frequencies**: Fixed gear 2 to use its own frequency range (600-700 Hz) instead of sharing with gear 1
  - Gear 1: 500-600 Hz (unchanged)
  - Gear 2: 600-700 Hz (now unique)
  - Gear 3: 700-800 Hz (shifted up from 600-700)
  - Gear 4+: Each subsequent gear increases by 100 Hz
  - This ensures each gear has a distinct audio cue for better feedback

### Removed
- Removed redundant `ACCSharedMemory.cs` file (project uses the simplified version)

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
