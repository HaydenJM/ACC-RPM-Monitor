# Telemetry Test Server

Standalone telemetry server with mock ACC data. No game required.

## What It Does

Runs an independent HTTP server that simulates ACC telemetry:
- HTTP server on port 8080 (configurable)
- Mock data: RPM, gears, speed, tire pressure/temp, fuel
- Updates at 10Hz (same as real monitoring)
- Serves JSON at `/telemetry` endpoint

## Quick Start

```bash
cd telemetry_test
dotnet run
```

Custom port:
```bash
dotnet run 8081
```

Test the endpoint:
```bash
curl http://localhost:8080/telemetry
```

Use with Streamlit:
```bash
# Terminal 1
cd telemetry_test
dotnet run

# Terminal 2
cd streamlit_dashboard
streamlit run streamlit_dashboard.py
```

## Architecture

Two threads run simultaneously:
1. HTTP Server Thread - listens for requests, serves `/telemetry`
2. Data Generator Thread - updates mock telemetry every 100ms

Data flow:
```
Generator creates snapshot → Thread-safe storage → HTTP handler reads → JSON response
```

## Running Both Test Server AND ACCRPMMonitor

They conflict on the same port. Choose one:

**Option 1: Different ports**
```bash
# Test server on 8080
cd telemetry_test
dotnet run 8080

# Main app on 8081
dotnet run -- -s --telemetry --port 8081
```

**Option 2: One at a time**
- Test server: for dashboard development/testing
- Main app: for real ACC monitoring

## Mock Data Behavior

- RPM: 2000-7500 with automatic gear shifts
- Speed: 50-250 km/h
- Tire pressure: 27-30 PSI (increases with speed)
- Tire temperature: 60-100°C (increases with speed)
- Fuel: slowly decreases
- Realistic noise and wheel-to-wheel variation

## JSON Response

```json
{
  "timestamp": "2025-10-31T14:32:15.123Z",
  "rpm": 5400,
  "gear": 3,
  "speedKmh": 120.5,
  "fuel": 79.8,
  "tirePressureFL": 28.2,
  "tirePressureFR": 28.4,
  "tirePressureRL": 28.6,
  "tirePressureRR": 28.7,
  "tirePressureAvg": 28.5,
  "tireTempFL": 82.3,
  "tireTempFR": 81.5,
  "tireTempRL": 78.2,
  "tireTempRR": 77.8,
  "tireTempAvg": 80.0
}
```

## Use Cases

1. Test dashboard without ACC running
2. Verify HTTP server and JSON serialization
3. Development and demos
4. CI/CD automated testing

## Troubleshooting

**Port already in use**
- Solution: Use different port with `dotnet run 8081`

**Streamlit not connecting**
- Verify server is running (check console output)
- Test endpoint: `curl http://localhost:8080/telemetry`
- Check port matches in both server and dashboard

## Files

- Program.cs - Server and mock data generator
- TelemetryTestServer.csproj - Project file
- README.md - This file

## Test vs Production

| Feature | Test Server | ACCRPMMonitor --telemetry |
|---------|-------------|---------------------------|
| Requires ACC | No | Yes |
| Data source | Mock | Real shared memory |
| Port | 8080 | 8080 |
| Update rate | 10Hz | 10Hz |
| Dependencies | None | ACC |
