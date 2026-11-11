namespace Labka.Models;

public sealed class MeterReading
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MeterId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public decimal Value { get; init; }
}
