# Command-Line Usage

ACCRPMMonitor supports command-line arguments for quick access to commonly used features.

## Options

```
-t, --telemetry       Enable telemetry window (displays real-time tire temps/pressures)
-e, --endurance       Use endurance audio profile (low-fatigue tones)
-h, --help            Show help message
```

## Examples

### Basic Usage
```bash
# Interactive menu (default)
ACCRPMMonitor

# With telemetry window
ACCRPMMonitor -t

# With endurance audio profile
ACCRPMMonitor -e

# With both telemetry and endurance audio
ACCRPMMonitor -t -e
```

## Audio Profiles

**Normal Profile** (default):
- Standard frequencies (500-1000 Hz)
- Fast beeping progression (500ms → 50ms)
- Solid tone at shift threshold
- Audio in all gears

**Endurance Profile** (`-e` flag):
- Lower frequencies (300-800 Hz for Standard mode, descending 800→400 Hz)
- Slower beeping (700ms → 150ms)
- Brief chirps instead of solid tones
- Audio only in gears 1-5 (silent in 6th gear+)
- Designed for long racing sessions to reduce fatigue

## Telemetry Window

When enabled with `-t`, displays a real-time WPF window showing:
- **Vehicle Info**: Speed, RPM, Gear, Fuel
- **Tire Pressures**: All 4 corners with color-coded indicators
  - Green: Optimal (27-29 PSI)
  - Yellow: Acceptable (26-27 or 29-30 PSI)
  - Red: Out of range (<26 or >30 PSI)
- **Tire Temperatures**: All 4 corners with color-coded indicators
  - Blue: Too cold (<70°C)
  - Cyan: Warming up (70-80°C)
  - Green: Optimal (80-95°C)
  - Yellow: Getting hot (95-105°C)
  - Red: Too hot (>105°C)

The telemetry window automatically launches when monitoring starts and updates in real-time throughout your session.
