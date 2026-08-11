namespace NileChain.Domain.Common;

/// <summary>
/// Delivery dates are Egypt calendar dates (the day the factory intends), not absolute UTC instants.
/// Stored as that calendar date at 12:00:00 UTC so Egypt (UTC+2/+3) never shifts the displayed date.
/// Validation compares calendar dates in UTC (grace = 0 days): DeliveryDate.Date &gt;= UtcNow.Date.
/// </summary>
public static class DeliveryDatePolicy
{
    /// <summary>
    /// Normalize an incoming date (or DateTime) to noon UTC on the same calendar day.
    /// </summary>
    public static DateTime ToUtcStorage(DateTime deliveryDate)
    {
        var d = DateOnly.FromDateTime(deliveryDate);
        return new DateTime(d.Year, d.Month, d.Day, 12, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime ToUtcStorage(DateOnly deliveryDate) =>
        new(deliveryDate.Year, deliveryDate.Month, deliveryDate.Day, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// True when the delivery calendar date is today (UTC) or later. Grace = 0 days.
    /// Assumption: "today" is <see cref="DateTime.UtcNow"/>.Date (not Egypt-local midnight).
    /// </summary>
    public static bool IsNotInPast(DateTime deliveryDate, DateTime? utcNow = null)
    {
        var today = DateOnly.FromDateTime(utcNow ?? DateTime.UtcNow);
        var delivery = DateOnly.FromDateTime(deliveryDate);
        return delivery >= today;
    }

    public static bool IsNotInPast(DateOnly deliveryDate, DateTime? utcNow = null)
    {
        var today = DateOnly.FromDateTime(utcNow ?? DateTime.UtcNow);
        return deliveryDate >= today;
    }
}
