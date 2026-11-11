using Labka.Contracts;
using Labka.Models;

namespace Labka.Services;

public class EnergyService
{
    private readonly IEnergyStore _store;

    public EnergyService(IEnergyStore store)
    {
        _store = store;
    }

    public IReadOnlyList<MeterDto> GetAllMeters()
    {
        return _store.GetAllMeters()
            .Select(m => new MeterDto(
                m.Id,
                m.SerialNumber,
                m.Type,
                m.Location,
                m.Status.ToString(),
                m.InstalledAt,
                m.Coefficient
            ))
            .ToList();
    }

    public MeterDto? GetMeter(Guid id)
    {
        var m = _store.GetMeter(id);
        if (m is null) return null;

        return new MeterDto(
            m.Id,
            m.SerialNumber,
            m.Type,
            m.Location,
            m.Status.ToString(),
            m.InstalledAt,
            m.Coefficient
        );
    }

    public (bool Success, string? ErrorCode, string? Message, MeterDto? Meter) RegisterMeter(RegisterMeterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SerialNumber))
            return (false, "invalid_serial_number", "Серійний номер не може бути порожнім.", null);

        if (string.IsNullOrWhiteSpace(request.Type))
            return (false, "invalid_type", "Тип лічильника не може бути порожнім.", null);

        if (string.IsNullOrWhiteSpace(request.Location))
            return (false, "invalid_location", "Місце встановлення не може бути порожнім.", null);

        var coef = request.Coefficient ?? 1.0m;
        if (!ReadingRules.ValidCoefficient(coef))
            return (false, "invalid_coefficient", "Некоректний коефіцієнт лічильника (має бути > 0).", null);

        var meter = new Meter
        {
            Id = Guid.NewGuid(),
            SerialNumber = request.SerialNumber.Trim(),
            Type = request.Type.Trim(),
            Location = request.Location.Trim(),
            Status = MeterStatus.Active,
            InstalledAt = request.InstalledAt?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
            Coefficient = coef
        };

        _store.AddMeter(meter);

        var dto = new MeterDto(
            meter.Id,
            meter.SerialNumber,
            meter.Type,
            meter.Location,
            meter.Status.ToString(),
            meter.InstalledAt,
            meter.Coefficient
        );

        return (true, null, null, dto);
    }

    public (bool Success, string? ErrorCode, string? Message) DecommissionMeter(Guid id)
    {
        var meter = _store.GetMeter(id);
        if (meter is null)
            return (false, "meter_not_found", "Лічильник не знайдено.");

        if (meter.Status == MeterStatus.Decommissioned)
            return (false, "already_decommissioned", "Лічильник уже виведено з експлуатації.");

        _store.UpdateMeterStatus(id, MeterStatus.Decommissioned);
        return (true, null, null);
    }

    public AcceptReadingResult SubmitReading(Guid meterId, SubmitReadingRequest request)
    {
        return _store.AddReading(meterId, request.RecordedAt, request.Value);
    }

    public IReadOnlyList<MeterReadingDto> GetReadings(Guid meterId, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        return _store.GetReadings(meterId, from, to)
            .Select(r => new MeterReadingDto(r.Id, r.MeterId, r.RecordedAt, r.Value))
            .ToList();
    }

    public (bool Success, string? ErrorCode, string? Message, ConsumptionDto? Result) CalculateConsumption(
        Guid meterId, DateTimeOffset from, DateTimeOffset to)
    {
        return _store.CalculateConsumption(meterId, from, to);
    }
}
