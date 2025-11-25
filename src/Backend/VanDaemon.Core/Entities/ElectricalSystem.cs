namespace VanDaemon.Core.Entities;

/// <summary>
/// Represents the electrical system state (battery, solar, AC power, etc.)
/// Primarily sourced from Victron Cerbo GX or similar battery management systems
/// </summary>
public class ElectricalSystem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Main Battery";

    // Battery DC values
    public double Voltage { get; set; } // Volts (V)
    public double Current { get; set; } // Amperes (A) - positive = charging, negative = discharging
    public double Power { get; set; } // Watts (W)
    public double StateOfCharge { get; set; } // Percentage (0-100)
    public double Temperature { get; set; } // Celsius (°C)

    // Battery capacity and consumption
    public double ConsumedAmpHours { get; set; } // Amp-hours consumed (Ah)
    public int TimeToGo { get; set; } // Seconds until battery empty (at current discharge rate)

    // Solar charging (if available)
    public double SolarPower { get; set; } // Watts (W)
    public double SolarVoltage { get; set; } // Volts (V)
    public double SolarCurrent { get; set; } // Amperes (A)

    // AC Input/Output (if inverter/charger present)
    public double AcInputPower { get; set; } // Watts (W)
    public double AcOutputPower { get; set; } // Watts (W)

    // Sensor configuration
    public string SensorPlugin { get; set; } = string.Empty;
    public Dictionary<string, object> SensorConfiguration { get; set; } = new();

    // Metadata
    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;
}
