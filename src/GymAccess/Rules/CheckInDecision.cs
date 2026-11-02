using GymAccess.Domain;

namespace GymAccess.Rules;

/// <summary>Resultado de evaluar un ingreso. Todavía no persistido ni aplicado.</summary>
public sealed record CheckInDecision(
    bool Allowed,
    DenialReason? Reason,
    Membership? Membership,
    bool CreditConsumed)
{
    public static CheckInDecision Approved(Membership membership, bool creditConsumed) =>
        new(true, null, membership, creditConsumed);

    public static CheckInDecision Denied(DenialReason reason) =>
        new(false, reason, null, false);
}
