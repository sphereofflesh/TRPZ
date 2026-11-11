namespace Labka.Contracts;

public sealed record RegisterMeterRequest(
    string SerialNumber,
    string Type,
    string Location,
    DateTimeOffset? InstalledAt = null,
    decimal? Coefficient = 1.0m
);

public sealed record SubmitReadingRequest(
    DateTimeOffset RecordedAt,
    decimal Value
);

public sealed record MeterDto(
    Guid Id,
    string SerialNumber,
    string Type,
    string Location,
    string Status,
    DateTimeOffset InstalledAt,
    decimal Coefficient
);

public sealed record MeterReadingDto(
    Guid Id,
    Guid MeterId,
    DateTimeOffset RecordedAt,
    decimal Value
);

public sealed record ConsumptionDto(
    Guid MeterId,
    string SerialNumber,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal StartValue,
    decimal EndValue,
    decimal Consumption,
    decimal Coefficient
);

public sealed record ApiError(
    string Code,
    string Message
);

public sealed record AcceptReadingResult(
    bool Accepted,
    string? Code,
    string? Message,
    MeterReadingDto? Reading
);
