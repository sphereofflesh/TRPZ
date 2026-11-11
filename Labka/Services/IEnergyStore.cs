using Labka.Contracts;
using Labka.Models;

namespace Labka.Services;

public interface IEnergyStore
{
    IReadOnlyList<Meter> GetAllMeters();
    Meter? GetMeter(Guid id);
    Meter AddMeter(Meter meter);
    bool UpdateMeterStatus(Guid id, MeterStatus status);

    IReadOnlyList<MeterReading> GetReadings(Guid meterId, DateTimeOffset? from = null, DateTimeOffset? to = null);
    MeterReading? GetLastReading(Guid meterId);
    AcceptReadingResult AddReading(Guid meterId, DateTimeOffset recordedAt, decimal value);

    (bool Success, string? ErrorCode, string? Message, ConsumptionDto? Result) CalculateConsumption(
        Guid meterId, DateTimeOffset from, DateTimeOffset to);

    void Reset();
}
