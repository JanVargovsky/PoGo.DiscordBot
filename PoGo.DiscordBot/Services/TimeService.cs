using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace PoGo.DiscordBot.Services;

public class TimeService
{
    public const string TimeFormat = "H:mm";
    public const string DateTimeFormat = "d.M.yyyy H:mm";

    readonly TimeZoneInfo _timeZoneInfo;

    public TimeService()
    {
        _timeZoneInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague") :
            TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
    }

    public DateTime? ParseTime(string time) => ParseTime(time, DateTime.UtcNow.Date);

    public DateTime? ParseTime(string time, DateTime date)
    {
        var pieces = time.Split(' ', '.', ',', ':', ';', '\'');

        if (pieces.Length != 2 || !int.TryParse(pieces[0], out int hours) || !int.TryParse(pieces[1], out int minutes))
            return null;

        var dt = new DateTime(date.Year, date.Month, date.Day, hours, minutes, 0);
        dt = TimeZoneInfo.ConvertTimeToUtc(dt, _timeZoneInfo);
        return dt;
    }

    public DateTime? ParseDateTime(string dateTime)
    {
        try
        {
            var tokens = dateTime.Split(new[] { ' ', '.', ',', ':', ';', '\'', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 5)
                throw new Exception($"Invalid date '{dateTime}'");
            var intTokens = tokens.Select(int.Parse).ToArray();

            var dt = new DateTime(intTokens[2], intTokens[1], intTokens[0], intTokens[3], intTokens[4], 0);
            dt = TimeZoneInfo.ConvertTimeToUtc(dt, _timeZoneInfo);
            return dt;

        }
        catch
        {
            return null;
        }
    }

    public DateTime ConvertToLocal(DateTime dt) => TimeZoneInfo.ConvertTime(dt, _timeZoneInfo);

    public DateTime EnsureUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Unspecified => TimeZoneInfo.ConvertTimeToUtc(dt, _timeZoneInfo),
        DateTimeKind.Local => TimeZoneInfo.ConvertTimeToUtc(dt),
        _ => dt
    };

    public string ConvertToLocalString(DateTime dt, string format) => ConvertToLocal(dt).ToString(format);

    public bool IsToday(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
            dt = ConvertToLocal(dt);
        var today = ConvertToLocal(DateTime.UtcNow);
        return dt.Day == today.Day && dt.Month == today.Month && dt.Year == today.Year;
    }
}
