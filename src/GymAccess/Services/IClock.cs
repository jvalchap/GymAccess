namespace GymAccess.Services;

/// <summary>Abstracción del reloj para poder controlar el tiempo en los tests.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class UtcClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
