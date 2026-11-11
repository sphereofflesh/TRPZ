using Labka.Contracts;
using Labka.Models;

namespace Labka.Services;

public class InMemoryEnergyStore : IEnergyStore
{
    private readonly object _lock = new();
    private readonly List<Meter> _meters = new();
    private readonly List<MeterReading> _readings = new();

    public static readonly Guid DefaultMeterId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public InMemoryEnergyStore()
    {
        SeedInitialData();
    }

    private void SeedInitialData()
    {
        _meters.Add(new Meter
        {
            Id = DefaultMeterId,
            SerialNumber = "EL-2026-001",
            Type = "Електроенергія",
            Location = "вул. Шевченка, 10, кв. 1",
            Status = MeterStatus.Active,
            InstalledAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Coefficient = 1.0m
        });
    }

    public IReadOnlyList<Meter> GetAllMeters()
    {
        lock (_lock)
        {
            return _meters.ToList();
        }
    }

    public Meter? GetMeter(Guid id)
    {
        lock (_lock)
        {
            return _meters.FirstOrDefault(m => m.Id == id);
        }
    }

    public Meter AddMeter(Meter meter)
    {
        lock (_lock)
        {
            _meters.Add(meter);
            return meter;
        }
    }

    public bool UpdateMeterStatus(Guid id, MeterStatus status)
    {
        lock (_lock)
        {
            var meter = _meters.FirstOrDefault(m => m.Id == id);
            if (meter is null) return false;
            meter.Status = status;
            return true;
        }
    }

    public IReadOnlyList<MeterReading> GetReadings(Guid meterId, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        lock (_lock)
        {
            var query = _readings.Where(r => r.MeterId == meterId);
            if (from.HasValue)
                query = query.Where(r => r.RecordedAt >= from.Value.ToUniversalTime());
            if (to.HasValue)
                query = query.Where(r => r.RecordedAt <= to.Value.ToUniversalTime());

            return query.OrderBy(r => r.RecordedAt).ToList();
        }
    }

    public MeterReading? GetLastReading(Guid meterId)
    {
        lock (_lock)
        {
            return _readings
                .Where(r => r.MeterId == meterId)
                .OrderByDescending(r => r.RecordedAt)
                .FirstOrDefault();
        }
    }

    public AcceptReadingResult AddReading(Guid meterId, DateTimeOffset recordedAt, decimal value)
    {
        lock (_lock)
        {
            if (!ReadingRules.ValidValue(value))
            {
                return new AcceptReadingResult(false, "invalid_value", "Неприпустиме значення або точність показника.", null);
            }

            var meter = _meters.FirstOrDefault(m => m.Id == meterId);
            if (meter is null)
            {
                return new AcceptReadingResult(false, "meter_not_found", "Лічильник не знайдено.", null);
            }

            if (meter.Status == MeterStatus.Decommissioned)
            {
                return new AcceptReadingResult(false, "meter_decommissioned", "Лічильник виведено з експлуатації. Внесення показників заблоковано.", null);
            }

            var utcAt = recordedAt.ToUniversalTime();

            var previous = _readings
                .Where(r => r.MeterId == meterId)
                .OrderByDescending(r => r.RecordedAt)
                .FirstOrDefault();

            if (previous is not null)
            {
                if (utcAt <= previous.RecordedAt)
                {
                    return new AcceptReadingResult(false, "invalid_date", "Час нового показника повинен бути пізнішим за попередній.", null);
                }

                if (value < previous.Value)
                {
                    return new AcceptReadingResult(false, "lower_value", "Нове значення показника не може бути меншим за попереднє.", null);
                }
            }

            var reading = new MeterReading
            {
                Id = Guid.NewGuid(),
                MeterId = meterId,
                RecordedAt = utcAt,
                Value = value
            };

            _readings.Add(reading);

            var dto = new MeterReadingDto(reading.Id, reading.MeterId, reading.RecordedAt, reading.Value);
            return new AcceptReadingResult(true, null, null, dto);
        }
    }

    public (bool Success, string? ErrorCode, string? Message, ConsumptionDto? Result) CalculateConsumption(
        Guid meterId, DateTimeOffset from, DateTimeOffset to)
    {
        lock (_lock)
        {
            var meter = _meters.FirstOrDefault(m => m.Id == meterId);
            if (meter is null)
            {
                return (false, "meter_not_found", "Лічильник не знайдено.", null);
            }

            var utcFrom = from.ToUniversalTime();
            var utcTo = to.ToUniversalTime();

            if (utcFrom >= utcTo)
            {
                return (false, "invalid_period", "Початкова дата періоду повинна бути ранішою за кінцеву.", null);
            }

            var periodReadings = _readings
                .Where(r => r.MeterId == meterId && r.RecordedAt >= utcFrom && r.RecordedAt <= utcTo)
                .OrderBy(r => r.RecordedAt)
                .ToList();

            if (periodReadings.Count < 2)
            {
                return (false, "boundary_reading_not_found", "Для обраного періоду недостатньо показників для розрахунку (потрібно щонайменше 2 показники).", null);
            }

            var startReading = periodReadings.First();
            var endReading = periodReadings.Last();

            var rawDiff = endReading.Value - startReading.Value;
            var consumption = rawDiff * meter.Coefficient;

            var dto = new ConsumptionDto(
                meter.Id,
                meter.SerialNumber,
                startReading.RecordedAt,
                endReading.RecordedAt,
                startReading.Value,
                endReading.Value,
                consumption,
                meter.Coefficient
            );

            return (true, null, null, dto);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _meters.Clear();
            _readings.Clear();
            SeedInitialData();
        }
    }
}
