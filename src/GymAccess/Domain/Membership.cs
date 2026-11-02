namespace GymAccess.Domain;

public class Membership
{
    public int Id { get; set; }
    public int MemberId { get; set; }

    /// <summary>Primer día de vigencia (inclusive).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Último día de vigencia (inclusive).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Créditos asignados. Null = ingresos ilimitados.</summary>
    public int? Credits { get; set; }

    /// <summary>Saldo de créditos. Null = ingresos ilimitados.</summary>
    public int? CreditsRemaining { get; set; }

    /// <summary>Inicio de la franja horaria (hora local del gimnasio, inclusive). Null = sin franja.</summary>
    public TimeOnly? SlotStart { get; set; }

    /// <summary>Fin de la franja horaria (hora local del gimnasio, inclusive). Null = sin franja.</summary>
    public TimeOnly? SlotEnd { get; set; }

    public bool UsesCredits => CreditsRemaining.HasValue;

    public bool IsActiveOn(DateOnly date) => date >= StartDate && date <= EndDate;

    /// <summary>
    /// Sin franja: siempre true. Si la franja cruza la medianoche (inicio &gt; fin, ej. 22:00-02:00)
    /// se interpreta como "desde el inicio hasta el fin del día siguiente".
    /// </summary>
    public bool IsWithinSlot(TimeOnly time)
    {
        if (!SlotStart.HasValue || !SlotEnd.HasValue)
        {
            return true;
        }

        var start = SlotStart.Value;
        var end = SlotEnd.Value;

        return start <= end
            ? time >= start && time <= end
            : time >= start || time <= end;
    }
}
