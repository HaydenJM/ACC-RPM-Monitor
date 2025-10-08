# ACC RPM Monitor

A real-time audio feedback system for Assetto Corsa Competizione that helps you optimize gear shifts through progressive audio cues.

## Overview

ACC RPM Monitor reads telemetry data directly from ACC's shared memory to provide auditory shift indicators customized for each vehicle. Instead of constantly watching the HUD, you'll hear escalating audio cues that tell you when to shift, allowing you to focus on the racing line and track conditions.

## Features

- **Progressive Audio Feedback**: Rising triangle wave tone that increases in pitch as you approach optimal shift RPM
- **Two-Stage Warning System**:
  - Rising tone starts at 300 RPM below target shift point
  - Urgent beeping alert starts at 100 RPM below target
- **Per-Vehicle Configuration**: Save unique RPM shift points for each car in your garage
- **Per-Gear Customization**: Configure different shift points for each gear (1-8)
- **Real-Time Telemetry**: Direct shared memory integration with ACC for instant feedback
- **Gear-Based Frequency**: Audio pitch increases with higher gears for better situational awareness
- **Interactive Setup**: Easy-to-use console menus for vehicle selection and RPM configuration

## How It Works

1. **Connect**: Launches automatically and waits for ACC to start
2. **Monitor**: Reads current gear and RPM data in real-time (~20Hz update rate)
3. **Alert**: Provides progressive audio feedback:
   - **Silent**: More than 300 RPM below shift point
   - **Rising Tone**: 300-100 RPM below shift point (pitch increases with RPM)
   - **Beeping Alert**: 100 RPM below shift point until you shift

## Requirements

- Windows (requires .NET 6.0 runtime)
- Assetto Corsa Competizione
- Audio output device

## Installation

1. Download the latest release or build from source
2. Extract to any folder
3. Run `ACCRPMMonitor.exe`

## Usage

### First Time Setup

1. Launch the application
2. Select or create a vehicle configuration:
   - Choose existing vehicle from the list
   - Create new vehicle profile
3. Configure RPM shift points for each gear:
   - Enter optimal shift RPM for gears 1-8
   - Values are saved automatically
4. Launch ACC and start driving

### During Racing

- The monitor connects automatically when ACC is running
- Audio feedback activates only during live sessions (not menus or replays)
- Neutral and reverse gears are ignored (no audio feedback)
- Press **ESC** to exit the application

### Configuration Files

Configuration files are stored in:
```
%LOCALAPPDATA%\ACCRPMMonitor\powercurves\
```

Each vehicle has its own JSON file (e.g., `Porsche_991ii_GT3_R.json`) containing gear-specific RPM thresholds.

#### Example Configuration
```json
{
  "GearRPMThresholds": {
    "1": 6000,
    "2": 6500,
    "3": 7000,
    "4": 7000,
    "5": 7000,
    "6": 7000,
    "7": 7000,
    "8": 7000
  }
}
```

## Audio Feedback Details

### Frequency Mapping
- **Gears 1-2**: Base frequency 500-600 Hz
- **Gear 3+**: Increases by 100 Hz per gear (Gear 3 = 600-700 Hz, Gear 4 = 700-800 Hz, etc.)
- **Rising Phase**: Frequency increases from base to max over 200 RPM range
- **Beeping Phase**: Rapid on/off beeping at max frequency (100ms on, 100ms off)

### Status Indicators

The console displays:
- **ACC Status**: Current game state (OFF, REPLAY, LIVE, PAUSE)
- **Current Gear**: Displayed gear (1-8, or N/R for neutral/reverse)
- **Current RPM**: Real-time engine RPM
- **Threshold**: Configured shift point for current gear
- **Status**: Distance from shift point and current audio state

## Building from Source

Requirements:
- .NET 6.0 SDK or later
- Windows development environment

```bash
git clone https://github.com/yourusername/ACCRPMMonitor.git
cd ACCRPMMonitor
dotnet build
dotnet run
```

## Dependencies

- **NAudio** (2.2.1) - Audio generation and playback
- **SharpDX.DirectInput** (4.2.0) - Potential future controller support

## Technical Details

### Shared Memory Integration

The application reads from ACC's shared memory mapped files:
- `Local\acpmf_physics` - Physics data (gear, RPM, etc.)
- `Local\acpmf_graphics` - Graphics/UI data (session status)

### Audio Engine

Uses NAudio to generate triangle wave audio:
- 44.1 kHz sample rate, mono
- Triangle wave generation for smooth, non-fatiguing audio
- 15% amplitude (0.15) for comfortable listening volume
- Real-time frequency modulation based on RPM

### Performance

- ~20 Hz telemetry update rate (50ms polling interval)
- Minimal CPU usage (~1-2% on modern processors)
- Sub-millisecond audio latency
- Automatic reconnection on ACC restart

## Troubleshooting

### Application won't connect to ACC
- Ensure ACC is fully loaded (not just in the launcher)
- Run the monitor as Administrator if permissions issues occur
- Verify ACC is running (not Assetto Corsa, which uses different memory)

### No audio feedback
- Check that you're in a live session (not menus, replays, or paused)
- Verify you're not in neutral or reverse
- Confirm your RPM is within 300 RPM of the configured threshold
- Check Windows audio output settings

### Configuration not saving
- Ensure the application has write permissions to `%LOCALAPPDATA%`
- Check for file system errors
- Verify JSON syntax if manually editing configuration files

## Future Enhancements

- GUI interface option
- DirectInput button mapping for quick profile switching
- Telemetry data export
- Lap time optimization suggestions
- Multi-monitor support with visual indicators

## License

MIT License - see LICENSE file for details

## Contributing

Contributions welcome! Please open an issue or submit a pull request.

## Acknowledgments

- ACC community for shared memory documentation
- NAudio project for audio synthesis capabilities
