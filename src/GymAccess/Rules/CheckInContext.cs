using GymAccess.Configuration;
using GymAccess.Domain;

namespace GymAccess.Rules;

/// <summary>Datos que ven las reglas al evaluar UNA membresía candidata.</summary>
public sealed class CheckInContext
{
    public required Membership Membership { get; init; }

    /// <summary>Deudas del socio (las reglas deciden cuáles cuentan).</summary>
    public required IReadOnlyList<Debt> Debts { get; init; }

    /// <summary>Fecha actual en la zona horaria del gimnasio.</summary>
    public required DateOnly LocalDate { get; init; }

    /// <summary>Hora actual en la zona horaria del gimnasio.</summary>
    public required TimeOnly LocalTime { get; init; }

    /// <summary>True si el socio ya ingresó (descontando crédito) con esta membresía dentro de la ventana de reingreso.</summary>
    public required bool IsReentry { get; init; }

    public required GymOptions Options { get; init; }
}
