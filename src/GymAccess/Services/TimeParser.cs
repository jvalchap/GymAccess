using System.Globalization;

namespace GymAccess.Services;

public static class TimeParser
{
    private static readonly string[] Formats = { "HH:mm", "HH:mm:ss" };

    public static bool TryParse(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
