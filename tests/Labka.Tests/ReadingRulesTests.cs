using Labka.Services;
using Xunit;

namespace Labka.Tests;

public class ReadingRulesTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1200.5, true)]
    [InlineData(999999.9999, true)]
    [InlineData(-1, false)]
    [InlineData(1000000001, false)]
    [InlineData(12.34567, false)] // exceeds 4 decimal places
    public void ValidValue_ValidatesCorrectly(decimal value, bool expected)
    {
        Assert.Equal(expected, ReadingRules.ValidValue(value));
    }

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(1.5, true)]
    [InlineData(0, false)]
    [InlineData(-0.5, false)]
    [InlineData(1000001, false)]
    public void ValidCoefficient_ValidatesCorrectly(decimal coef, bool expected)
    {
        Assert.Equal(expected, ReadingRules.ValidCoefficient(coef));
    }

    [Fact]
    public void CanAccept_ReturnsTrue_WhenTimeIsLaterAndValueIsGreaterOrEqual()
    {
        var prevAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var prevVal = 1200m;

        var newAt = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var newVal = 1260m;

        Assert.True(ReadingRules.CanAccept(newAt, newVal, prevAt, prevVal));
    }

    [Fact]
    public void CanAccept_ReturnsFalse_WhenTimeIsEarlierOrEqual()
    {
        var prevAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var prevVal = 1200m;

        var newAt = DateTimeOffset.Parse("2026-05-31T00:00:00Z");
        var newVal = 1260m;

        Assert.False(ReadingRules.CanAccept(newAt, newVal, prevAt, prevVal));
    }

    [Fact]
    public void CanAccept_ReturnsFalse_WhenValueIsLower()
    {
        var prevAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var prevVal = 1200m;

        var newAt = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var newVal = 1199m;

        Assert.False(ReadingRules.CanAccept(newAt, newVal, prevAt, prevVal));
    }
}
