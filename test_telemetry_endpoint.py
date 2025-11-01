"""
Quick test script to verify telemetry endpoint is working
Run this while ACCRPMMonitor is running with telemetry enabled
"""

import requests
import json
import time

TELEMETRY_URL = "http://localhost:8080/telemetry"

print("Testing telemetry endpoint...")
print(f"URL: {TELEMETRY_URL}\n")

for i in range(10):
    try:
        response = requests.get(TELEMETRY_URL, timeout=2)
        print(f"[{i+1}] Status: {response.status_code}")

        if response.status_code == 200:
            data = response.json()
            print(f"    Data received: {json.dumps(data, indent=2)}")
            print(f"    Tire Pressure FL: {data.get('tirePressureFL', 'N/A')}")
            print(f"    Tire Temp FL: {data.get('tireTempFL', 'N/A')}")
        elif response.status_code == 503:
            print("    No telemetry data available yet")
            print(f"    Response: {response.text}")
        else:
            print(f"    Unexpected status: {response.text}")

    except requests.exceptions.ConnectionError:
        print(f"[{i+1}] Connection failed - is telemetry server running?")
    except requests.exceptions.Timeout:
        print(f"[{i+1}] Request timed out")
    except Exception as e:
        print(f"[{i+1}] Error: {e}")

    print()
    time.sleep(1)

print("Test complete")
