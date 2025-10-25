# ACC RPM Monitor

A real-time audio feedback system for Assetto Corsa Competizione that helps you optimize gear shifts through intelligent shift point detection and dynamic audio alerts.

## Overview

ACC RPM Monitor reads telemetry data directly from ACC's shared memory to provide auditory shift indicators customized for each vehicle. The application features a menu-driven interface with dedicated workflows for creating auto configurations through data collection or manually defining shift points.

## Features

### Audio Feedback System
- **Dynamic Audio Alert**: Smooth volume ramping that adapts based on how fast your RPMs are climbing
  - Volume gradually increases from quiet to full as RPM approaches shift point
  - Prevents audio at low RPMs (hard-coded 6000 RPM minimum threshold)
  - Prevents late warnings in lower gears where RPMs climb quickly
- **Gear-Based Frequency**: Each gear has unique audio pitch for better situational awareness
  - Gear 1: 500 Hz
  - Gear 2: 600 Hz
  - Gear 3: 700 Hz
  - Gear 4+: Increases by 100 Hz per gear
- **Smooth and Natural**: Steady continuous tone with volume control for less distracting feedback

### Main Menu System
- **Menu-Driven Interface**: Clear workflow options for all tasks
  - Create Auto Configuration (Data Collection)
  - Create/Edit Manual Configuration
  - Select & Use Configuration (Start Monitoring)
  - Change Vehicle
  - Open Config Folder (View reports and power curves)
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
  - Focuses on gears 1-5 for comprehensive analysis
  - Real-time per-gear data point tracking
  - Immediate feedback on data quality and success
  - Option to retry if data insufficient
- **Comprehensive Session Reports**:
  - JSON and human-readable text reports for every collection session
  - Per-gear analysis with confidence scores and explanations
  - RPM distribution histograms
  - Specific recommendations for improvement
  - Reports saved to `%LocalAppData%/ACCRPMMonitor/reports/`
- **Power Curve Graphs**: Automatically generated PNG visualizations after successful auto-configuration
  - 1920x1080 professional-quality graphs
  - Shows acceleration curves for gears 1-5 with color-coded lines
  - Displays calculated gear ratios
  - Marks optimal shift points with diamond markers
- **Enhanced Confidence Scoring**:
  - Transparent validation showing exactly what the app looks for
  - Minimum 30 data points per gear (60% confidence)
  - Detailed breakdown: 30-59 pts (60%), 60-119 pts (80%), 120+ pts (100%)
  - Requires all gears 1-5 to pass for successful auto-config
- **Per-Vehicle Configuration**: Separate configs for each car, both manual and auto-generated
- **Per-Gear Customization**: Configure different shift points for each gear (1-8)

### Telemetry & Performance
- **Real-Time Telemetry**: Direct shared memory integration with ACC for instant feedback
- **RPM Rate Tracking**: Monitors RPM acceleration over 200ms window
- **Speed and Throttle Monitoring**: Displays real-time speed (km/h) and throttle percentage
- **Live Status Display**: Shows RPM rate, beeping distance, and current gear
- **Interactive Setup**: Easy-to-use console menus for vehicle and mode selection

## How It Works

### Creating Auto Configuration
1. **Select Workflow**: Choose "Create Auto Configuration" from main menu
2. **Follow Instructions**: Load Monza or Paul Ricard and start practice session
3. **Collect Data**: Press F1 to start, accelerate through gears 1-5 at high throttle, press F1 to stop
4. **Analyze Results**: App immediately analyzes data and shows per-gear confidence scores
5. **Review Reports**: Detailed reports and power curve graphs saved showing exactly how shift points were determined
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
5. **Alert**: Provides smooth volume-ramping audio feedback
6. **Exit**: Press ESC to return to main menu

## Requirements

- Windows (requires .NET 6.0 runtime)
- Assetto Corsa Competizione
- Audio output device

## Installation

1. Download the latest release
2. Extract to any folder
3. Run `ACCRPMMonitor.exe`

## Usage

### First Run
The application will automatically detect your vehicle when you run it with ACC open. A default configuration will be created if none exists.

### Configuration Storage
- Configurations stored in: `%LocalAppData%\ACCRPMMonitor\powercurves\`
- Reports and graphs stored in: `%LocalAppData%\ACCRPMMonitor\reports\`
- Use the "Open Config Folder" menu option to quickly access these files

### Recommended Workflow
1. **Auto-Config for New Vehicles**: Use "Create Auto Configuration" with a hotlap session for quick, optimal setup
2. **Fine-Tune if Needed**: Switch to manual mode and adjust individual gears
3. **Use Adaptive Mode**: Enable continuous learning in monitoring mode (F2 to save)
4. **Monitor**: Use your configuration during races and practice sessions

## Troubleshooting

### No vehicle detected
- Make sure ACC is running before starting the monitor
- Check that you're in a session (menu won't trigger detection)
- The application will automatically detect vehicles on subsequent runs

### Data collection not working
- Ensure you're on track with ACC (not in menus)
- Hold at least 85% throttle for valid data
- Travel at more than 5 km/h
- Need to collect data for gears 1-5 sequentially

### Audio not playing
- Check Windows volume and application volume
- Verify audio device is connected and working
- The application will only play audio when RPM is above 6000

## Building from Source

```bash
dotnet build -c Release
```

Requires:
- .NET 6.0 SDK
- Dependencies managed via NuGet

## License

ACC RPM Monitor is provided as-is for personal use.

## Support

For issues or feature requests, please refer to the project repository.

---

**Current Version**: v2.1.0
**Last Updated**: October 2025
