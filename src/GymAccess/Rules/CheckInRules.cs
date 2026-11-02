namespace GymAccess.Rules;

/// <summary>
/// Lista de reglas de ingreso. El ORDEN importa: si más de una regla falla, se informa el motivo de la primera.
/// Para agregar una regla: crear la clase y sumarla acá (ver DECISIONS.md).
/// </summary>
public static class CheckInRules
{
    public static IReadOnlyList<ICheckInRule> CreateDefault() => new ICheckInRule[]
    {
        new ActiveMembershipRule(),  // 1. membresía vigente
        new OverdueDebtRule(),       // 2. deuda vencida dentro de la tolerancia
        new TimeSlotRule(),          // 3. franja horaria
        new CreditsRule()            // 4. créditos (con exención por reingreso, regla 5)
    };
}
