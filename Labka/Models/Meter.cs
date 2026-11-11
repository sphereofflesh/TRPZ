namespace Labka.Models;

public sealed class Meter
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string SerialNumber { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public MeterStatus Status { get; set; } = MeterStatus.Active;
    public DateTimeOffset InstalledAt { get; init; } = DateTimeOffset.UtcNow;
    public decimal Coefficient { get; init; } = 1.0m;
}
