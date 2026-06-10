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

    // Returns the [Start, End) UTC range covering the given calendar year.
    public static (DateTime Start, DateTime End) GetYearRange(int year) =>
        (new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
         new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    private static (DateTime Start, DateTime? End) GetPeriodRange(string period)
    {
        var now = DateTime.UtcNow;
        // Use explicit DateTimeKind.Utc — DateTime.Date strips the Kind to Unspecified,
        // which Npgsql rejects when writing to a 'timestamp with time zone' column.
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        switch (period.ToLower())
        {
            case "week":
                return (today.AddDays(-(int)today.DayOfWeek), null);
            case "month":
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                return (monthStart, monthStart.AddMonths(1));
            case "year":
                var (yearStart, yearEnd) = GetYearRange(now.Year);
                return (yearStart, yearEnd);
            default:
                return (DateTime.MinValue, null);
        }
    }
}
