namespace GymAccess.Domain;

/// <summary>Registro de un intento de ingreso (aprobado o rechazado).</summary>
public class CheckIn
{
    public int Id { get; set; }

    /// <summary>Socio al que pertenece el intento. Null si el DNI presentado no corresponde a ningún socio.</summary>
    public int? MemberId { get; set; }

    /// <summary>DNI tal como se presentó (normalizado). Se guarda siempre, para auditar también los intentos de DNI desconocidos.</summary>
    public string PresentedDni { get; set; } = string.Empty;

    /// <summary>Membresía con la que se aprobó el ingreso. Null si fue rechazado.</summary>
    public int? MembershipId { get; set; }

    public DateTime OccurredAtUtc { get; set; }
    public CheckInResult Result { get; set; }
    public DenialReason? DenialReason { get; set; }

    /// <summary>True si este ingreso descontó un crédito (false en reingresos y membresías ilimitadas).</summary>
    public bool CreditConsumed { get; set; }

    /// <summary>Saldo de créditos luego del ingreso. Null si no aplica.</summary>
    public int? CreditsRemainingAfter { get; set; }
}
