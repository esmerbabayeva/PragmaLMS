namespace Pragma.Application.Common;

public static class TimeProviderExtensions
{
    private static readonly TimeZoneInfo BakuTimeZone = ResolveBakuTimeZone();

    private static TimeZoneInfo ResolveBakuTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time"); }
        catch { }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku"); }
        catch { }
        return TimeZoneInfo.CreateCustomTimeZone("PragmaBakuTime", TimeSpan.FromHours(4), "Baku Time", "Baku Time");
    }

    public static DateTime GetBakuNow(this TimeProvider timeProvider)
    {
        var utcNow = timeProvider.GetUtcNow();
        return TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, BakuTimeZone);
    }

    public static DateOnly GetBakuToday(this TimeProvider timeProvider)
    {
        return DateOnly.FromDateTime(timeProvider.GetBakuNow());
    }
}
