# Folder Structure

## Data Organization

All application data is stored under the `data/` directory with the following structure:

```
data/
├── {car_name}/
│   ├── {track_name}/
│   │   ├── {car_name}_config.json        # Manual configuration
│   │   ├── {car_name}_auto.json          # Auto-generated configuration
│   │   ├── {car_name}_PowerCurve_*.png   # Power curve graphs
│   │   ├── shift_analysis_*.txt          # Shift pattern reports (text)
│   │   └── shift_analysis_*.json         # Shift pattern reports (JSON)
│   └── {another_track}/
│       └── ...
└── {another_car}/
    └── ...
```

## Examples

```
data/
├── porsche_991ii_gt3_r/
│   ├── monza/
│   │   ├── porsche_991ii_gt3_r_config.json
│   │   ├── porsche_991ii_gt3_r_auto.json
│   │   ├── porsche_991ii_gt3_r_PowerCurve_2025-10-31_14-32-15.png
│   │   ├── shift_analysis_20251031_143230.txt
│   │   └── shift_analysis_20251031_143230.json
│   ├── spa/
│   │   └── ...
│   └── brands_hatch/
│       └── ...
├── ferrari_488_gt3_evo/
│   ├── monza/
│   │   └── ...
│   └── imola/
│       └── ...
```

## File Descriptions

### Configuration Files

**{car}_config.json** - Manual configuration
- User-defined shift points for each gear
- Custom audio settings
- Created via interactive configuration menu

**{car}_auto.json** - Auto-generated configuration
- Physics-based optimal shift points
- Calculated from acceleration curves
- Includes gear ratios and performance data
- Created via Auto Configuration flow

### Report Files

**shift_analysis_{timestamp}.txt** - Human-readable report
- Session summary (laps, shifts, times)
- Per-gear shift point analysis
- Performance comparison (physics vs. actual)
- Recommendations for improvement

**shift_analysis_{timestamp}.json** - Machine-readable report
- Same data as text report
- Structured JSON format
- For programmatic analysis or visualization

### Graph Files

**{car}_PowerCurve_{timestamp}.png** - Visual power curve analysis
- Acceleration curves for each gear
- Gear ratios displayed
- Optimal shift points marked (diamond markers)
- User's actual average shift points (circle markers)
- Generated after auto-config or monitoring sessions

## Benefits of Car-First Structure

1. **Intuitive Organization**: All data for a specific car is in one place
2. **Easy Comparison**: Compare performance across different tracks for same car
3. **Track-Specific Tuning**: Each track can have different optimal shift points
4. **Simple Backup**: Backup entire car folder to preserve all configurations
5. **Clean Deletion**: Remove a car folder to clean up all related data
6. **Migration Friendly**: Easy to share car setups (just copy the folder)

## File Naming Conventions

- **Car names**: Sanitized, lowercase, underscores instead of spaces
  - Example: "Porsche 991 II GT3 R" → `porsche_991ii_gt3_r`
- **Track names**: Sanitized, lowercase, underscores instead of spaces
  - Example: "Brands Hatch" → `brands_hatch`
- **Timestamps**: ISO-like format `yyyyMMdd_HHmmss`
  - Example: `20251031_143215`

## ConfigMan Methods

- `GetVehicleDataDirectory()` - Returns `data/{car}/{track}/`
- `GetConfigPath()` - Returns path to config file
- `GetAvailableVehicles()` - Lists all cars with data
- `GetAvailableTracks()` - Lists all tracks for current car
- `SetVehicleAndTrack()` - Changes current car/track context

## Migration from Old Structure

Old structure (before this change):
```
data/
├── {track}/
│   ├── {car}.json
│   └── {car}_auto.json
```

New structure:
```
data/
├── {car}/
│   └── {track}/
│       ├── {car}_config.json
│       └── {car}_auto.json
```

If you have existing data in the old structure, you'll need to manually reorganize it or the application will create new configurations.
