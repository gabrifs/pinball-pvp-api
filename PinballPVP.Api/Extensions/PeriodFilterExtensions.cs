using PinballPVP.Api.Models;

namespace PinballPVP.Api.Extensions;

public static class PeriodFilterExtensions
{
    public static bool IsValidPeriod(this string? period) =>
        string.IsNullOrEmpty(period) || period.ToLower() is "week" or "month" or "year";

    public static IQueryable<SoloMatch> ApplyPeriodFilter(this IQueryable<SoloMatch> query, string? period)
    {
        if (string.IsNullOrEmpty(period)) return query;
        var (start, end) = GetPeriodRange(period);
        return end is { } e
            ? query.Where(m => m.PlayedAt >= start && m.PlayedAt < e)
            : query.Where(m => m.PlayedAt >= start);
    }

    public static IQueryable<VersusMatch> ApplyPeriodFilter(this IQueryable<VersusMatch> query, string? period)
    {
        if (string.IsNullOrEmpty(period)) return query;
        var (start, end) = GetPeriodRange(period);
        return end is { } e
            ? query.Where(m => m.PlayedAt >= start && m.PlayedAt < e)
            : query.Where(m => m.PlayedAt >= start);
    }

    private static (DateTime Start, DateTime? End) GetPeriodRange(string period)
    {
        var now = DateTime.UtcNow.Date;
        return period.ToLower() switch
        {
            "week"  => (now.AddDays(-(int)now.DayOfWeek), null),
            "month" => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1)),
            "year"  => (new DateTime(now.Year, 1, 1),         new DateTime(now.Year + 1, 1, 1)),
            _       => (DateTime.MinValue, null)
        };
    }
}
