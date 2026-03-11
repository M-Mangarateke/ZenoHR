// REQ-OPS-003: EMP201 deadline calculation helpers.
// EMP201 is due by the 7th of each month for the previous month's PAYE/UIF/SDL.

using System.Globalization;

namespace ZenoHR.Api.BackgroundServices;

/// <summary>
/// Pure helper methods for EMP201 filing deadline calculations.
/// Stateless and deterministic — designed for easy unit testing.
/// </summary>
public static class Emp201DeadlineHelper
{
    private const int DeadlineDay = 7;

    /// <summary>
    /// Returns the next EMP201 deadline on or after <paramref name="today"/>.
    /// If today is on or before the 7th, returns the 7th of the current month.
    /// If today is after the 7th, returns the 7th of the next month.
    /// </summary>
    public static DateOnly GetNextDeadline(DateOnly today)
    {
        if (today.Day <= DeadlineDay)
        {
            return new DateOnly(today.Year, today.Month, DeadlineDay);
        }

        // Move to next month
        var nextMonth = today.AddMonths(1);
        return new DateOnly(nextMonth.Year, nextMonth.Month, DeadlineDay);
    }

    /// <summary>
    /// Returns the filing period (month, year) that a given deadline covers.
    /// EMP201 due on the 7th covers the previous month.
    /// For example, 7 January 2026 covers December 2025.
    /// </summary>
    public static (int Month, int Year) GetFilingPeriod(DateOnly deadline)
    {
        var previousMonth = deadline.AddMonths(-1);
        return (previousMonth.Month, previousMonth.Year);
    }

    /// <summary>
    /// Returns the number of days from <paramref name="today"/> until the next deadline.
    /// Returns 0 if today is the deadline. Returns negative if the deadline has passed
    /// (i.e., today is after the 7th — overdue for the current period).
    /// </summary>
    public static int GetDaysUntilDeadline(DateOnly today)
    {
        var deadline = GetNextDeadline(today);
        return deadline.DayNumber - today.DayNumber;
    }

    /// <summary>
    /// Formats a filing period tuple as "MM/yyyy" using invariant culture.
    /// </summary>
    public static string FormatFilingPeriod(int month, int year) =>
        string.Format(CultureInfo.InvariantCulture, "{0:D2}/{1}", month, year);
}
