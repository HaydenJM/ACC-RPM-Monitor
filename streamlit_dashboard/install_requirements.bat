@echo off
echo Installing Streamlit dashboard requirements...
echo.

REM Check if Python is available
python --version >nul 2>&1
if errorlevel 1 (
    echo Error: Python is not installed or not in PATH
    echo Please install Python from https://www.python.org/downloads/
    echo Make sure to check "Add Python to PATH" during installation
    pause
    exit /b 1
)

REM Install requirements
echo Installing Python packages...
python -m pip install --upgrade pip
python -m pip install -r requirements.txt

if errorlevel 1 (
    echo.
    echo Error: Failed to install requirements
    pause
    exit /b 1
)

echo.
echo ========================================
echo Installation complete!
echo ========================================
echo.
echo The Streamlit dashboard will auto-launch when you run:
echo   ACCRPMMonitor.exe -s -t
echo.
echo Or you can manually start it with:
echo   streamlit run streamlit_dashboard.py
echo.
pause
