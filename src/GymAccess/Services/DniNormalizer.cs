namespace GymAccess.Services;

public static class DniNormalizer
{
    /// <summary>Quita puntos, espacios y guiones: "12.345.678" -> "12345678".</summary>
    public static string Normalize(string? raw) =>
        (raw ?? string.Empty).Trim().Replace(".", "").Replace(" ", "").Replace("-", "");

    /// <summary>Entre 6 y 9 dígitos.</summary>
    public static bool IsValid(string normalized) =>
        normalized.Length is >= 6 and <= 9 && normalized.All(c => c is >= '0' and <= '9');
}
