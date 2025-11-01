using System;
using System.Collections.Generic;

namespace ACCRPMMonitor;

/// <summary>
/// Command-line argument parser for ACCRPMMonitor
/// </summary>
public class CommandLineArgs
{
    public bool EnableTelemetry { get; set; } = false;
    public bool EnduranceSound { get; set; } = false;
    public bool ShowHelp { get; set; } = false;

    /// <summary>
    /// Parse command-line arguments
    /// </summary>
    public static CommandLineArgs Parse(string[] args)
    {
        var result = new CommandLineArgs();

        foreach (string arg in args)
        {
            switch (arg.ToLower())
            {
                case "-h":
                case "--help":
                    result.ShowHelp = true;
                    return result;

                case "-t":
                case "--telemetry":
                    result.EnableTelemetry = true;
                    break;

                case "-e":
                case "--endurance":
                    result.EnduranceSound = true;
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Display help information
    /// </summary>
    public static void ShowHelpText()
    {
        Console.WriteLine(@"
ACC RPM Monitor - Command Line Options
======================================

Usage: ACCRPMMonitor [options]

Options:
  -t, --telemetry             Enable telemetry window (tire temps/pressures)
  -e, --endurance             Use endurance audio profile (low-fatigue)
  -h, --help                  Show this help message

Examples:
  ACCRPMMonitor               # Interactive menu (default)
  ACCRPMMonitor -t            # With telemetry window
  ACCRPMMonitor -e            # With endurance audio
  ACCRPMMonitor -t -e         # With both telemetry and endurance audio

");
    }
}
