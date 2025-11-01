# ACC Tire Telemetry Dashboard - Streamlit

Real-time tire pressure and temperature visualization for Assetto Corsa Competizione.

## Architecture

```
ACC → C# ACCRPMMonitor → HTTP Server (localhost:8080) → Python Streamlit Dashboard (localhost:8501)
```

The C# application reads telemetry from ACC's shared memory and serves it over HTTP. The Python Streamlit dashboard fetches this data and displays real-time charts and metrics.

## Quick Start (Automated)

### Step 1: Install Python Dependencies (One-time setup)

**Windows:** Double-click `install_requirements.bat` in this folder

**Or manually:**
```bash
pip install -r requirements.txt
```

Or install individually:
```bash
pip install streamlit requests pandas plotly
```

### Step 2: Run ACCRPMMonitor with Telemetry

The Streamlit dashboard will **automatically launch** when you enable telemetry:

```bash
ACCRPMMonitor.exe -s -t
```

That's it! The application will:
1. Start the telemetry server on `http://localhost:8080/telemetry`
2. Launch the Streamlit dashboard
3. Open your browser to `http://localhost:8501`

The dashboard will automatically connect and start displaying real-time tire data.

## Manual Dashboard Launch (Optional)

If you prefer to launch the dashboard manually instead of auto-launch:

1. Start ACC and begin a practice/race session
2. Start ACCRPMMonitor with telemetry enabled
3. In a separate terminal, run:

```bash
cd streamlit_dashboard
streamlit run streamlit_dashboard.py
```

4. Navigate to `http://localhost:8501` in your browser

## Disabling Auto-Launch

The dashboard auto-launches by default. To disable this behavior, you would need to modify the C# code or we can add a command-line flag in the future.

## Features

### Real-Time Metrics Display
- RPM, Gear, Speed, Fuel
- Live tire pressures (PSI) for all 4 wheels
- Live tire temperatures (°C) for all 4 wheels

### Historical Charts
- **Tire Pressure Chart**: Shows pressure trends over the last 30 seconds
- **Tire Temperature Chart**: Shows temperature trends over the last 30 seconds
- Color-coded by wheel (FL, FR, RL, RR)

### Data Tables
- Current pressure for each wheel
- Current temperature for each wheel
- Average values calculated in real-time

## API Endpoints

The C# telemetry server exposes:

### `GET /telemetry`
Returns current telemetry snapshot as JSON:

```json
{
  "timestamp": "2025-01-30T12:34:56Z",
  "rpm": 7250,
  "gear": 4,
  "speedKmh": 187.5,
  "fuel": 45.3,
  "tirePressureFL": 27.8,
  "tirePressureFR": 27.9,
  "tirePressureRL": 26.5,
  "tirePressureRR": 26.6,
  "tirePressureAvg": 27.2,
  "tireTempFL": 85.3,
  "tireTempFR": 86.1,
  "tireTempRL": 92.4,
  "tireTempRR": 93.2,
  "tireTempAvg": 89.25
}
```

### `GET /`
Returns server info and available endpoints

## Configuration

### Update Interval
Modify in `streamlit_dashboard.py`:
```python
UPDATE_INTERVAL = 0.1  # seconds (100ms = 10Hz)
```

### History Length
Modify in `streamlit_dashboard.py`:
```python
MAX_HISTORY_POINTS = 300  # 30 seconds at 10Hz
```

### Server Port
Modify in `TelemetryServer.cs`:
```csharp
public int Port { get; set; } = 8080;
```

And update the URL in `streamlit_dashboard.py`:
```python
TELEMETRY_URL = "http://localhost:8080/telemetry"
```

## Troubleshooting

### "No telemetry data available"
- Make sure ACC is running and you're in a session (not menus)
- Verify the C# application is running with telemetry server enabled
- Check the C# console for "Telemetry server started" message

### Dashboard not updating
- Check the telemetry server is reachable: Open `http://localhost:8080/telemetry` in browser
- Verify UPDATE_INTERVAL is not too low (minimum 0.05 seconds recommended)

### Port already in use
- Change the port in both `TelemetryServer.cs` and `streamlit_dashboard.py`
- Or stop any application using port 8080

## Advanced Usage

### Remote Access
To access the dashboard from another device on your network:

1. Modify the HttpListener prefix in `TelemetryServer.cs`:
```csharp
_listener.Prefixes.Add($"http://+:{Port}/");
```

2. Run as administrator (required for non-localhost bindings)

3. Update the dashboard URL to your PC's IP address:
```python
TELEMETRY_URL = "http://192.168.1.100:8080/telemetry"
```

4. Run Streamlit with server address:
```bash
streamlit run streamlit_dashboard.py --server.address 0.0.0.0
```

### Multiple Clients
Multiple Streamlit dashboards can connect to the same telemetry server simultaneously.

### Data Recording
The telemetry server could be extended to log data to files for post-race analysis.

## Performance Notes

- **Update Rate**: 10Hz (100ms intervals) is recommended for smooth updates without overwhelming the server
- **Network Overhead**: ~500 bytes per update = ~5KB/sec bandwidth
- **CPU Usage**: Minimal on both C# and Python sides
- **Browser Performance**: Plotly charts may slow down with >500 data points

## Future Enhancements

- [ ] Lap-by-lap pressure/temperature comparison
- [ ] Configurable alerts (pressure too low, temperature too high)
- [ ] Export data to CSV
- [ ] Brake temperature monitoring
- [ ] Suspension travel visualization
- [ ] Multi-session recording and playback
