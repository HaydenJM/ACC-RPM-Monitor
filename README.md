# ACC RPM Monitor

A real-time audio feedback system for Assetto Corsa Competizione that helps you optimize gear shifts through intelligent shift point detection and dynamic audio alerts.

## Overview

ACC RPM Monitor reads telemetry data directly from ACC's shared memory to provide auditory shift indicators customized for each vehicle. The application features a menu-driven interface with dedicated workflows for creating auto configurations through data collection or manually defining shift points.

## Features

### Audio Feedback System
- **Dynamic Beeping Alert**: Urgent beeping that adapts timing based on how fast your RPMs are climbing
  - Very fast RPM increase (>1500 RPM/sec): Beeps 400 RPM early
  - Fast increase (>1000 RPM/sec): Beeps 300 RPM early
  - Moderate increase (>300 RPM/sec): Beeps 200 RPM early
  - Slow increase (>50 RPM/sec): Beeps 100 RPM early
  - Very slow/stable: Beeps 50 RPM early
- **Gear-Based Frequency**: Each gear has unique audio pitch for better situational awareness
  - Gear 1: 500 Hz
  - Gear 2: 600 Hz
  - Gear 3: 700 Hz
  - Gear 4+: Increases by 100 Hz per gear
- **Simple and Effective**: No rising tone, just adaptive beeping that starts at the right time

### Main Menu System
- **Menu-Driven Interface**: Clear workflow options for all tasks
  - Create Auto Configuration (Data Collection)
  - Create/Edit Manual Configuration
  - Select & Use Configuration (Start Monitoring)
  - Change Vehicle
  - Exit application
- **Returns to Menu**: All workflows return to main menu when complete

### Intelligent Shift Detection
- **Automatic Vehicle Detection**: Identifies your current car from ACC and loads the right config
- **Dual Configuration Modes**:
  - **Manual Mode**: Define your own custom RPM shift points (fully editable)
  - **Auto Mode**: Uses learned optimal shift points (read-only)
- **Dedicated Data Collection Workflow**:
  - Step-by-step instructions for hotlap sessions (Monza/Paul Ricard recommended)
  - F1 key controls data collection start/stop
  - Focuses on gears 1-6 for comprehensive analysis
  - Immediate feedback on data quality and success
  - Option to retry if data insufficient
- **Comprehensive Session Reports**:
  - JSON and human-readable text reports for every collection session
  - Per-gear analysis with confidence scores and explanations
  - RPM distribution histograms
  - Specific recommendations for improvement
  - Reports saved to `%LocalAppData%/ACCRPMMonitor/reports/`
- **Enhanced Confidence Scoring**:
  - Transparent validation showing exactly what the app looks for
  - Minimum 50 data points per gear (0.50 confidence)
  - Detailed breakdown: < 50 pts (0.0), 50-99 pts (0.5), 100-199 pts (0.75), 200+ pts (1.0)
  - Requires all gears 1-6 to pass for successful auto-config
- **Per-Vehicle Configuration**: Separate configs for each car, both manual and auto-generated
- **Per-Gear Customization**: Configure different shift points for each gear (1-8)

### Telemetry & Performance
- **Real-Time Telemetry**: Direct shared memory integration with ACC for instant feedback
- **RPM Rate Tracking**: Monitors RPM acceleration over 200ms window
- **Live Status Display**: Shows RPM rate, beeping distance, and current gear
- **Interactive Setup**: Easy-to-use console menus for vehicle and mode selection

## How It Works

### Creating Auto Configuration
1. **Select Workflow**: Choose "Create Auto Configuration" from main menu
2. **Follow Instructions**: Load Monza or Paul Ricard and start practice session
3. **Collect Data**: Press F1 to start, do hotlap redlining gears 1-6, press F1 to stop
4. **Analyze Results**: App immediately analyzes data and shows per-gear confidence scores
5. **Review Reports**: Detailed reports saved showing exactly how shift points were determined
6. **Retry or Continue**: If data insufficient, retry collection; if successful, config is saved

### Creating Manual Configuration
1. **Select Workflow**: Choose "Create/Edit Manual Configuration" from main menu
2. **Edit Values**: Set custom shift RPM for each gear (1-8)
3. **Save**: Configuration saved automatically

### Using Configuration (Monitoring Mode)
1. **Select Workflow**: Choose "Select & Use Configuration" from main menu
2. **Choose Mode**: Pick Manual or Auto configuration
3. **Start ACC**: Launch ACC and join a session
4. **Monitor**: Reads current gear and RPM data in real-time (~20Hz update rate)
5. **Alert**: Provides adaptive beeping feedback based on RPM acceleration rate
6. **Exit**: Press ESC to return to main menu

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

1. **Launch**: Run `ACCRPMMonitor.exe`
2. **Vehicle Detection**: Automatic detection runs (or select manually from menu)
3. **Main Menu**: Choose your workflow:
   - **Option 1**: Create auto configuration via data collection
   - **Option 2**: Create/edit manual configuration
   - **Option 3**: Select and use an existing configuration
   - **Option 4**: Change vehicle
   - **Option 5**: Exit

### Creating Your First Auto Configuration

1. Select option **1** from main menu
2. Follow on-screen instructions:
   - Load Monza or Paul Ricard in ACC
   - Start a practice session
3. Press **F1** to start data collection
4. Do a hotlap, redlining gears 1-6 under full throttle
5. Press **F1** to stop collection
6. Review results:
   - Success: Config saved, detailed reports generated
   - Incomplete: Follow recommendations and retry
7. Return to main menu

### During Monitoring (Racing)

- The monitor connects automatically when ACC is running
- Audio feedback activates only during live sessions (not menus or replays)
- Neutral and reverse gears are ignored (no audio feedback)
- **Press ESC** to exit monitoring and return to main menu

### In Data Collection Mode

- **Press F1** to start/stop data collection
- **Press ESC** to abort and return to main menu
- Focus on redlining gears 1-6 for best results

### Configuration Files

Configuration files are stored in:
```
%LOCALAPPDATA%\ACCRPMMonitor\powercurves\
```

Data collection reports are stored in:
```
%LOCALAPPDATA%\ACCRPMMonitor\reports\
```

Each vehicle has two JSON files:
- `{vehicle}.json` - Manual configuration (user-defined)
- `{vehicle}_auto.json` - Auto-generated optimal configuration

Each data collection session generates two report files:
- `DataCollectionReport_{vehicle}_{timestamp}.json` - Machine-readable data
- `DataCollectionReport_{vehicle}_{timestamp}.txt` - Human-readable analysis

#### Manual Configuration Example
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
  },
  "IsAutoGenerated": false
}
```

#### Auto-Generated Configuration Example
```json
{
  "GearRPMThresholds": {
    "1": 5850,
    "2": 6420,
    "3": 6890,
    "4": 7150,
    "5": 7200,
    "6": 7250,
    "7": 7300,
    "8": 7350
  },
  "IsAutoGenerated": true,
  "LastUpdated": "2025-10-08T14:32:15.123Z",
  "TotalDataPoints": 1500,
  "DataConfidence": {
    "1": 1.0,
    "2": 1.0,
    "3": 0.75,
    "4": 0.5
  }
}
```

## Audio Feedback Details

### Dynamic Beeping System
The application uses a single-stage beeping alert system with adaptive timing:

- **Very Fast RPM Climb (>1500 RPM/sec)**: Beeping starts 400 RPM early
- **Fast RPM Climb (>1000 RPM/sec)**: Beeping starts 300 RPM early
- **Moderate-Fast Climb (>600 RPM/sec)**: Beeping starts 250 RPM early
- **Moderate Climb (>300 RPM/sec)**: Beeping starts 200 RPM early
- **Slow-Moderate Climb (>150 RPM/sec)**: Beeping starts 150 RPM early
- **Slow Climb (>50 RPM/sec)**: Beeping starts 100 RPM early
- **Very Slow/Stable**: Beeping starts 50 RPM early

### Frequency Mapping
Each gear has a distinct frequency for better awareness:
- **Gear 1**: 500 Hz
- **Gear 2**: 600 Hz
- **Gear 3**: 700 Hz
- **Gear 4**: 800 Hz
- **Gear 5+**: Continues increasing by 100 Hz per gear

### Beeping Pattern
- **Pattern**: Rapid on/off beeping (100ms on, 100ms off)
- **Volume**: 15% amplitude for comfortable listening
- **Audio Type**: Triangle wave for smooth, non-fatiguing sound

### Status Indicators

#### In Monitoring Mode
The console displays:
- **ACC Status**: Current game state (OFF, REPLAY, LIVE, PAUSE)
- **Current Gear**: Displayed gear (1-8, or N/R for neutral/reverse)
- **Current RPM**: Real-time engine RPM
- **Threshold**: Configured shift point for current gear
- **RPM Rate**: Current RPM acceleration (RPM/sec)
- **Beep Dist**: Dynamic beeping distance based on RPM rate
- **Status**: Distance from shift point and current audio state (Normal, BEEPING, SHIFT UP!)

#### In Data Collection Mode
The console displays:
- **ACC Status**: Current game state
- **Data Collection**: ACTIVE or STOPPED with total data points collected
- **Current Gear**: Active gear
- **Current RPM**: Real-time engine RPM
- **Status**: Collection status and instructions

## Building from Source

Requirements:
- .NET 6.0 SDK or later
- Windows development environment

```bash
git clone https://github.com/HaydenJM/ACC-RPM-Monitor.git
cd ACC-RPM-Monitor
dotnet build
dotnet run
```

## Dependencies

- **NAudio** (2.2.1) - Audio generation and playback
- **SharpDX.DirectInput** (4.2.0) - Potential future controller support

## Technical Details

### Shared Memory Integration

The application reads from ACC's shared memory mapped files:
- `Local\acpmf_physics` - Physics data (gear, RPM, speed, throttle)
- `Local\acpmf_graphics` - Graphics/UI data (session status)
- `Local\acpmf_static` - Static data (vehicle model, track name)

### Audio Engine

Dynamic Audio Engine with RPM rate tracking:
- 44.1 kHz sample rate, mono
- Triangle wave generation for smooth, non-fatigating audio
- 15% amplitude (0.15) for comfortable listening volume
- Real-time frequency modulation based on RPM
- 200ms RPM history window for rate calculation
- Adaptive warning distance calculation

### Optimal Shift Detection Algorithm

1. **Data Collection**: Records RPM, speed, throttle, and gear during driving (F1 to start/stop)
2. **Filtering**: Only uses full-throttle data (>95% throttle) for analysis
3. **Peak Detection**: Finds maximum speed achieved in each gear
4. **Optimal Point**: Identifies lowest RPM achieving ≥99% of max speed
5. **Confidence Scoring**: Calculates reliability based on data quantity
   - < 50 points: 0.0 (Insufficient)
   - 50-99 points: 0.5 (Low confidence)
   - 100-199 points: 0.75 (Medium confidence)
   - 200+ points: 1.0 (High confidence)
6. **Report Generation**: Creates detailed JSON and text reports for each session
7. **Validation**: Requires all gears 1-6 to pass confidence threshold
8. **Save**: Only saves configuration if all required gears pass validation

### Performance

- ~20 Hz telemetry update rate (50ms polling interval)
- Minimal CPU usage (~1-2% on modern processors)
- Sub-millisecond audio latency
- Automatic reconnection on ACC restart
- Menu-driven architecture for clean separation of workflows

## Troubleshooting

### Application won't connect to ACC
- Ensure ACC is fully loaded (not just in the launcher)
- Run the monitor as Administrator if permissions issues occur
- Verify ACC is running (not Assetto Corsa, which uses different memory)

### No audio feedback
- Check that you're in monitoring mode (option 3 from main menu)
- Verify you're in a live session (not menus, replays, or paused)
- Confirm you're not in neutral or reverse
- Check your RPM is within beeping distance of the configured threshold
- Verify Windows audio output settings

### Auto config not being created
- Make sure you selected "Create Auto Configuration" from main menu (option 1)
- Press F1 to START data collection (it won't collect automatically)
- Drive full-throttle acceleration runs redlining gears 1-6
- Press F1 to STOP and analyze
- Need at least 50 data points per gear for minimum confidence
- Check the generated reports in `%LocalAppData%/ACCRPMMonitor/reports/` for details

### Configuration not saving
- Ensure the application has write permissions to `%LOCALAPPDATA%`
- Check for file system errors
- Verify JSON syntax if manually editing configuration files

## Tips for Best Results

### Creating Auto Configurations
- **Track Selection**: Use Monza or Paul Ricard for long straights to redline each gear
- **Session Type**: Practice mode works best (no traffic, no pressure)
- **Driving Style**: Full throttle acceleration, try to redline gears 1-6
- **Data Quality**: Aim for 200+ points per gear for high confidence (1.0 score)
- **Review Reports**: Check the text reports to understand what the app detected
- **Multiple Runs**: If first attempt fails, do another hotlap and press F1 again to collect more data

### Manual Mode
- Start conservative with shift points and adjust based on feel
- Use lower RPMs for better fuel efficiency
- Set higher RPMs for qualifying laps
- Test different values per track/conditions

### Using Monitoring Mode
- Works best when you have consistent throttle application
- Lower gears benefit most from dynamic beeping (faster RPM climb)
- The beeping distance shown on screen helps you understand when audio will trigger
- Press ESC to return to menu if you want to try different config

## Future Enhancements

- GUI interface option
- DirectInput button mapping for quick profile switching
- Visual indicators for multi-monitor setups
- Telemetry data logging and export
- Lap time analysis and optimization
- Downshift detection and warnings
- Optimal shift point suggestions based on power curves
- Track-specific configurations

## License

MIT License - see LICENSE file for details

## Contributing

Contributions welcome! Please open an issue or submit a pull request.

## Acknowledgments

- ACC community for shared memory documentation
- NAudio project for audio synthesis capabilities
