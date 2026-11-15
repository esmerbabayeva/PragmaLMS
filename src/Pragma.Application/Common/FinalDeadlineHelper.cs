using System;
using Pragma.Domain.Entities;
using Pragma.Domain.Enums;

namespace Pragma.Application.Common;

/// <summary>
/// Kohort üzrə Final layihənin son təhvil müddətini (Asia/Baku və UTC) idarə edən köməkçi sinif.
/// </summary>
public static class FinalDeadlineHelper
{
    public static readonly TimeZoneInfo BakuTimeZone = ResolveBakuTimeZone();

    private static TimeZoneInfo ResolveBakuTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "Asia/Baku",
                    TimeSpan.FromHours(4),
                    "Azerbaijan Standard Time",
                    "Azerbaijan Standard Time");
            }
        }
    }

    /// <summary>
    /// Kohortun rəsmi Final təhvil müddətini UTC formatında qaytarır.
    /// FinalDeadlineUtc doludursa onu qaytarır; boşdursa və EndDate doludursa EndDate gününün Asia/Baku 23:59:59 vaxtını UTC-yə çevirib qaytarır; hər ikisi boşdursa null.
    /// </summary>
    public static DateTime? GetFinalDeadlineUtc(Cohort cohort)
    {
        if (cohort.FinalDeadlineUtc.HasValue)
        {
            return DateTime.SpecifyKind(cohort.FinalDeadlineUtc.Value, DateTimeKind.Utc);
        }

        if (cohort.EndDate.HasValue)
        {
            var rawEndUtc = DateTime.SpecifyKind(cohort.EndDate.Value, DateTimeKind.Utc);
            var bakuLocal = TimeZoneInfo.ConvertTimeFromUtc(rawEndUtc, BakuTimeZone);
            var endOfDayBaku = new DateTime(bakuLocal.Year, bakuLocal.Month, bakuLocal.Day, 23, 59, 59, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(endOfDayBaku, BakuTimeZone);
        }

        return null;
    }

    /// <summary>
    /// Final layihənin təhvil müddətinin bitib-bitmədiyini yoxlayır.
    /// Status == Finished VƏ YA (deadline != null VƏ nowUtc > deadline).
    /// Deadline null olduqda "keçməyib" qaytarır (əgər Finished deyilsə).
    /// </summary>
    public static bool IsFinalDeadlinePassed(Cohort cohort, DateTime? nowUtc = null, TimeProvider? timeProvider = null)
    {
        if (cohort.Status == CohortStatus.Finished)
        {
            return true;
        }

        var currentNowUtc = nowUtc ?? timeProvider?.GetUtcNow().UtcDateTime ?? DateTime.UtcNow;
        var deadlineUtc = GetFinalDeadlineUtc(cohort);

        if (!deadlineUtc.HasValue)
        {
            return false;
        }

        return currentNowUtc > deadlineUtc.Value;
    }

    /// <summary>
    /// UTC vaxtını Asia/Baku vaxtına çevirir.
    /// </summary>
    public static DateTime ToBakuLocal(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, BakuTimeZone);
    }

    /// <summary>
    /// UTC vaxtını "yyyy-MM-dd HH:mm" formatında Baku vaxtı ilə qaytarır.
    /// </summary>
    public static string? ToBakuLocalString(DateTime? utcDateTime)
    {
        if (!utcDateTime.HasValue) return null;
        var local = ToBakuLocal(utcDateTime.Value);
        return local.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>
    /// Daxil edilmiş tarix (yyyy-MM-dd) və saatı (HH:mm) Asia/Baku vaxtı kimi qəbul edib UTC DateTime-a çevirir.
    /// Time boşdursa 23:59:59 qəbul edilir.
    /// </summary>
    public static DateTime ConvertBakuToUtc(DateOnly date, TimeOnly? time = null)
    {
        var finalTime = time ?? new TimeOnly(23, 59, 59);
        var unspecifiedLocal = new DateTime(date.Year, date.Month, date.Day, finalTime.Hour, finalTime.Minute, finalTime.Second, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, BakuTimeZone);
    }

    /// <summary>
    /// Müddətin günün sonuna (23:59:59 və ya 23:59) təsadüf edib-etmədiyini yoxlayır.
    /// </summary>
    public static bool IsEndOfDay(DateTime? finalDeadlineUtc)
    {
        if (!finalDeadlineUtc.HasValue) return true;
        var local = ToBakuLocal(finalDeadlineUtc.Value);
        return local.Hour == 23 && local.Minute == 59;
    }
}
