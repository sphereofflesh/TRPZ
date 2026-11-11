namespace Labka.Services;

public static class ReadingRules
{
    public static bool ValidValue(decimal value) =>
        value >= 0m && value <= 1_000_000_000m && decimal.Round(value, 4) == value;

    public static bool ValidCoefficient(decimal value) =>
        value > 0m && value <= 1_000_000m && decimal.Round(value, 4) == value;

    public static bool IsTimeLater(DateTimeOffset newAt, DateTimeOffset previousAt) =>
        newAt > previousAt;

    public static bool IsValueNotLower(decimal newValue, decimal previousValue) =>
        newValue >= previousValue;

    public static bool CanAccept(
        DateTimeOffset newAt,
        decimal newValue,
        DateTimeOffset previousAt,
        decimal previousValue) =>
        IsTimeLater(newAt, previousAt) && IsValueNotLower(newValue, previousValue);
}
