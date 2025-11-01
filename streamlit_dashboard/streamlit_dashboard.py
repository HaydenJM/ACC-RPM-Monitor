"""
ACC Tire Telemetry Dashboard - Fixed Version
Real-time visualization of tire pressure and temperature data from ACC
Streams data from the C# telemetry server over HTTP
"""

import streamlit as st
import requests
import pandas as pd
import plotly.graph_objects as go
from plotly.subplots import make_subplots
import time
from datetime import datetime

# Configuration
TELEMETRY_URL = "http://localhost:8080/telemetry"

# Page config
st.set_page_config(
    page_title="ACC Tire Telemetry",
    page_icon="🏎️",
    layout="wide",
    initial_sidebar_state="collapsed"
)

# Custom CSS
st.markdown("""
    <style>
    .big-font {
        font-size:50px !important;
        font-weight: bold;
    }
    .metric-label {
        font-size:20px !important;
        color: #888;
    }
    </style>
    """, unsafe_allow_html=True)

# Title
st.title("🏎️ ACC Tire Telemetry Dashboard")

# Initialize session state for data history
if 'pressure_history' not in st.session_state:
    st.session_state.pressure_history = {
        'time': [],
        'FL': [], 'FR': [], 'RL': [], 'RR': []
    }
if 'temp_history' not in st.session_state:
    st.session_state.temp_history = {
        'time': [],
        'FL': [], 'FR': [], 'RL': [], 'RR': []
    }

MAX_HISTORY_POINTS = 300  # Keep last 30 seconds at 10Hz


def fetch_telemetry():
    """Fetch telemetry data from the C# server"""
    try:
        response = requests.get(TELEMETRY_URL, timeout=1)
        if response.status_code == 200:
            return response.json()
        return None
    except requests.exceptions.RequestException:
        return None


def update_history(data):
    """Update the rolling history of telemetry data"""
    if data is None:
        return

    timestamp = datetime.fromisoformat(data['timestamp'].replace('Z', '+00:00'))

    # Update pressure history
    st.session_state.pressure_history['time'].append(timestamp)
    st.session_state.pressure_history['FL'].append(data['tirePressureFL'])
    st.session_state.pressure_history['FR'].append(data['tirePressureFR'])
    st.session_state.pressure_history['RL'].append(data['tirePressureRL'])
    st.session_state.pressure_history['RR'].append(data['tirePressureRR'])

    # Update temperature history
    st.session_state.temp_history['time'].append(timestamp)
    st.session_state.temp_history['FL'].append(data['tireTempFL'])
    st.session_state.temp_history['FR'].append(data['tireTempFR'])
    st.session_state.temp_history['RL'].append(data['tireTempRL'])
    st.session_state.temp_history['RR'].append(data['tireTempRR'])

    # Trim history to max length
    if len(st.session_state.pressure_history['time']) > MAX_HISTORY_POINTS:
        for key in st.session_state.pressure_history:
            st.session_state.pressure_history[key] = st.session_state.pressure_history[key][-MAX_HISTORY_POINTS:]
        for key in st.session_state.temp_history:
            st.session_state.temp_history[key] = st.session_state.temp_history[key][-MAX_HISTORY_POINTS:]


def create_pressure_chart():
    """Create tire pressure chart"""
    fig = make_subplots(
        rows=1, cols=1,
        subplot_titles=['Tire Pressure (PSI)']
    )

    colors = {'FL': '#FF6B6B', 'FR': '#4ECDC4', 'RL': '#FFE66D', 'RR': '#95E1D3'}

    for wheel in ['FL', 'FR', 'RL', 'RR']:
        fig.add_trace(
            go.Scatter(
                x=st.session_state.pressure_history['time'],
                y=st.session_state.pressure_history[wheel],
                name=wheel,
                mode='lines',
                line=dict(color=colors[wheel], width=2)
            )
        )

    fig.update_layout(
        height=400,
        showlegend=True,
        hovermode='x unified',
        xaxis_title='Time',
        yaxis_title='Pressure (PSI)',
        yaxis=dict(range=[15, 40])
    )

    return fig


def create_temp_chart():
    """Create tire temperature chart"""
    fig = make_subplots(
        rows=1, cols=1,
        subplot_titles=['Tire Temperature (°C)']
    )

    colors = {'FL': '#FF6B6B', 'FR': '#4ECDC4', 'RL': '#FFE66D', 'RR': '#95E1D3'}

    for wheel in ['FL', 'FR', 'RL', 'RR']:
        fig.add_trace(
            go.Scatter(
                x=st.session_state.temp_history['time'],
                y=st.session_state.temp_history[wheel],
                name=wheel,
                mode='lines',
                line=dict(color=colors[wheel], width=2)
            )
        )

    fig.update_layout(
        height=400,
        showlegend=True,
        hovermode='x unified',
        xaxis_title='Time',
        yaxis_title='Temperature (°C)',
        yaxis=dict(range=[40, 120])
    )

    return fig


# Fetch latest telemetry
data = fetch_telemetry()

# Update history
if data:
    update_history(data)

# Display header metrics
if data:
    col1, col2, col3, col4 = st.columns([1, 1, 1, 1])
    with col1:
        st.metric("RPM", f"{data['rpm']}")
    with col2:
        st.metric("Gear", f"{data['gear']}")
    with col3:
        st.metric("Speed", f"{data['speedKmh']:.1f} km/h")
    with col4:
        st.metric("Fuel", f"{data['fuel']:.1f} L")
else:
    st.error("⚠️ No telemetry data available. Make sure ACC is running and telemetry server is started.")

# Display charts
if data and len(st.session_state.pressure_history['time']) > 0:
    st.plotly_chart(create_pressure_chart(), use_container_width=True)
    st.plotly_chart(create_temp_chart(), use_container_width=True)

# Display detailed tire metrics
if data:
    st.subheader("Current Tire Telemetry")

    col1, col2 = st.columns(2)

    with col1:
        st.markdown("### 📊 Tire Pressures (PSI)")
        pressure_data = {
            'Wheel': ['Front Left', 'Front Right', 'Rear Left', 'Rear Right', 'Average'],
            'Pressure': [
                f"{data['tirePressureFL']:.2f}",
                f"{data['tirePressureFR']:.2f}",
                f"{data['tirePressureRL']:.2f}",
                f"{data['tirePressureRR']:.2f}",
                f"{data['tirePressureAvg']:.2f}"
            ]
        }
        st.dataframe(pressure_data, use_container_width=True, hide_index=True)

    with col2:
        st.markdown("### 🌡️ Tire Temperatures (°C)")
        temp_data = {
            'Wheel': ['Front Left', 'Front Right', 'Rear Left', 'Rear Right', 'Average'],
            'Temperature': [
                f"{data['tireTempFL']:.1f}",
                f"{data['tireTempFR']:.1f}",
                f"{data['tireTempRL']:.1f}",
                f"{data['tireTempRR']:.1f}",
                f"{data['tireTempAvg']:.1f}"
            ]
        }
        st.dataframe(temp_data, use_container_width=True, hide_index=True)

# Auto-refresh every 1 second
time.sleep(1)
st.rerun()
