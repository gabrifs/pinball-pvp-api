using PinballPVP.Api.Extensions;

namespace PinballPVP.Tests.Unit;

public class PeriodFilterExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("week")]
    [InlineData("WEEK")]
    [InlineData("Week")]
    [InlineData("month")]
    [InlineData("MONTH")]
    [InlineData("year")]
    [InlineData("YEAR")]
    public void IsValidPeriod_ValidInputs_ReturnsTrue(string? period)
    {
        Assert.True(period.IsValidPeriod());
    }

    [Theory]
    [InlineData("day")]
    [InlineData("quarterly")]
    [InlineData("w")]
    [InlineData("all-time")]
    [InlineData("weekly")]
    [InlineData("monthly")]
    [InlineData(" ")]
    [InlineData("wEeK ")]
    public void IsValidPeriod_InvalidInputs_ReturnsFalse(string period)
    {
        Assert.False(period.IsValidPeriod());
    }

    [Theory]
    [InlineData(2024)]
    [InlineData(2025)]
    public void GetYearRange_ReturnsJanFirstToJanFirstNextYear_Utc(int year)
    {
        var (start, end) = PeriodFilterExtensions.GetYearRange(year);

        Assert.Equal(new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(DateTimeKind.Utc, start.Kind);

        Assert.Equal(new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc), end);
        Assert.Equal(DateTimeKind.Utc, end.Kind);
    }
}
