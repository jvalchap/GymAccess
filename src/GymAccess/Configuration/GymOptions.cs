namespace GymAccess.Configuration;

/// <summary>Parámetros de negocio, configurables en appsettings.json (sección "Gym").</summary>
public class GymOptions
{
    public const string SectionName = "Gym";

    /// <summary>
    /// Monto de tolerancia: si la suma de deudas pendientes vencidas SUPERA este valor, se rechaza el ingreso.
    /// </summary>
    public decimal DebtTolerance { get; set; } = 0m;

    /// <summary>Ventana (en horas) en la que un socio puede reingresar sin que se le descuente otro crédito.</summary>
    public int ReentryWindowHours { get; set; } = 2;

    /// <summary>
    /// Zona horaria del gimnasio. Define qué es "hoy" y la hora local para vigencia, deudas vencidas y franja horaria.
    /// </summary>
    public string TimeZoneId { get; set; } = "America/Argentina/Buenos_Aires";
}
