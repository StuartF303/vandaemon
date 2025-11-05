using VanDaemon.Core.Enums;

namespace VanDaemon.Core.Entities;

/// <summary>
/// Represents a system alert or notification
/// </summary>
public class Alert
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Source { get; set; } = string.Empty; // Tank ID, Control ID, or system component
    public string Message { get; set; } = string.Empty;
    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
