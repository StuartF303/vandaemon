namespace VanDaemon.Plugins.Ui.Api;

/// <summary>
/// Read model for the subset of the existing <c>GET api/tanks</c> payload the reference tile needs.
/// </summary>
public sealed class TankDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double CurrentLevel { get; set; }
    public double Capacity { get; set; }
}
