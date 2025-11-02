using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ACCRPMMonitor;

/// <summary>
/// WPF window for displaying real-time ACC telemetry data
/// </summary>
public partial class TelemetryWindow : Window
{
    public TelemetryWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Allows dragging the window by clicking and dragging anywhere on the border
    /// </summary>
    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        catch
        {
            // Ignore any errors during drag (e.g., if called when not in drag state)
        }
    }

    /// <summary>
    /// Updates the telemetry display with new data
    /// Must be called from UI thread
    /// </summary>
    public void UpdateTelemetry(TelemetrySnapshot snapshot)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateTelemetry(snapshot));
            return;
        }

        // Update vehicle info
        SpeedText.Text = $"{snapshot.SpeedKmh:F1} km/h";
        RpmText.Text = snapshot.RPM.ToString();
        GearText.Text = snapshot.Gear == 0 ? "N" : snapshot.Gear.ToString();
        FuelText.Text = $"{snapshot.Fuel:F1} L";

        // Update tire pressures
        PressureFLText.Text = $"{snapshot.TirePressureFL:F1}";
        PressureFRText.Text = $"{snapshot.TirePressureFR:F1}";
        PressureRLText.Text = $"{snapshot.TirePressureRL:F1}";
        PressureRRText.Text = $"{snapshot.TirePressureRR:F1}";
        PressureAvgText.Text = $"{snapshot.TirePressureAvg:F1}";

        // Update tire temperatures
        TempFLText.Text = $"{snapshot.TireTempFL:F1}";
        TempFRText.Text = $"{snapshot.TireTempFR:F1}";
        TempRLText.Text = $"{snapshot.TireTempRL:F1}";
        TempRRText.Text = $"{snapshot.TireTempRR:F1}";
        TempAvgText.Text = $"{snapshot.TireTempAvg:F1}";

        // Color-code tire temperatures (optimal range ~80-90°C)
        UpdateTemperatureColor(TempFLText, snapshot.TireTempFL);
        UpdateTemperatureColor(TempFRText, snapshot.TireTempFR);
        UpdateTemperatureColor(TempRLText, snapshot.TireTempRL);
        UpdateTemperatureColor(TempRRText, snapshot.TireTempRR);

        // Color-code tire pressures (typical range ~27-29 PSI)
        UpdatePressureColor(PressureFLText, snapshot.TirePressureFL);
        UpdatePressureColor(PressureFRText, snapshot.TirePressureFR);
        UpdatePressureColor(PressureRLText, snapshot.TirePressureRL);
        UpdatePressureColor(PressureRRText, snapshot.TirePressureRR);
    }

    /// <summary>
    /// Color-codes temperature based on optimal range
    /// </summary>
    private void UpdateTemperatureColor(System.Windows.Controls.TextBlock textBlock, float temp)
    {
        if (temp < 70)
        {
            // Too cold - Blue
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 255));
        }
        else if (temp >= 70 && temp < 80)
        {
            // Getting warm - Cyan
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 255));
        }
        else if (temp >= 80 && temp <= 95)
        {
            // Optimal - Green
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 255, 100));
        }
        else if (temp > 95 && temp <= 105)
        {
            // Getting hot - Yellow
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 100));
        }
        else
        {
            // Too hot - Red
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
        }
    }

    /// <summary>
    /// Color-codes pressure based on typical range
    /// </summary>
    private void UpdatePressureColor(System.Windows.Controls.TextBlock textBlock, float pressure)
    {
        if (pressure < 26)
        {
            // Too low - Red
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
        }
        else if (pressure >= 26 && pressure < 27)
        {
            // Low - Yellow
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 100));
        }
        else if (pressure >= 27 && pressure <= 29)
        {
            // Optimal - Green
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 255, 100));
        }
        else if (pressure > 29 && pressure <= 30)
        {
            // High - Yellow
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 100));
        }
        else
        {
            // Too high - Red
            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
        }
    }

    /// <summary>
    /// Shows "No data" state
    /// </summary>
    public void ShowNoData()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(ShowNoData);
            return;
        }

        // Reset all displays to default values
        SpeedText.Text = "0";
        RpmText.Text = "0";
        GearText.Text = "N";
        FuelText.Text = "0";

        PressureFLText.Text = "0.0";
        PressureFRText.Text = "0.0";
        PressureRLText.Text = "0.0";
        PressureRRText.Text = "0.0";
        PressureAvgText.Text = "0.0";

        TempFLText.Text = "0.0";
        TempFRText.Text = "0.0";
        TempRLText.Text = "0.0";
        TempRRText.Text = "0.0";
        TempAvgText.Text = "0.0";
    }
}
