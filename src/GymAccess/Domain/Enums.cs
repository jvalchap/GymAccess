namespace GymAccess.Domain;

public enum DebtStatus
{
    Pending,
    Paid
}

public enum CheckInResult
{
    Approved,
    Denied
}

/// <summary>Motivo de rechazo de un ingreso. Uno por regla de ingreso.</summary>
public enum DenialReason
{
    /// <summary>Regla 1: el socio no tiene una membresía vigente.</summary>
    NoActiveMembership,

    /// <summary>Regla 2: las deudas pendientes vencidas superan la tolerancia.</summary>
    OverdueDebt,

    /// <summary>Regla 3: el ingreso ocurre fuera de la franja horaria de la membresía.</summary>
    OutsideTimeSlot,

    /// <summary>Regla 4: la membresía usa créditos y no quedan.</summary>
    NoCredits,

    /// <summary>El DNI presentado no corresponde a ningún socio.</summary>
    MemberNotFound
}
