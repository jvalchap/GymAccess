using GymAccess.Domain;

namespace GymAccess.Contracts;

public record MemberResponse(int Id, string Name, string Dni)
{
    public static MemberResponse From(Member m) => new(m.Id, m.Name, m.Dni);
}

public record MembershipResponse(
    int Id,
    int MemberId,
    DateOnly StartDate,
    DateOnly EndDate,
    int? Credits,
    int? CreditsRemaining,
    TimeOnly? SlotStart,
    TimeOnly? SlotEnd)
{
    public static MembershipResponse From(Membership m) =>
        new(m.Id, m.MemberId, m.StartDate, m.EndDate, m.Credits, m.CreditsRemaining, m.SlotStart, m.SlotEnd);
}

public record DebtResponse(int Id, int MemberId, decimal Amount, DateOnly DueDate, DebtStatus Status, DateTime? PaidAt)
{
    public static DebtResponse From(Debt d) => new(
        d.Id,
        d.MemberId,
        d.Amount,
        d.DueDate,
        d.Status,
        d.PaidAtUtc.HasValue ? (DateTime?)DateTime.SpecifyKind(d.PaidAtUtc.Value, DateTimeKind.Utc) : null);
}

public record CheckInResponse(
    int Id,
    int? MemberId,
    bool Allowed,
    DenialReason? Reason,
    string Message,
    int? MembershipId,
    bool CreditConsumed,
    int? CreditsRemaining,
    DateTime OccurredAt)
{
    public static CheckInResponse From(CheckIn c) => new(
        c.Id,
        c.MemberId,
        c.Result == CheckInResult.Approved,
        c.DenialReason,
        CheckInMessages.For(c),
        c.MembershipId,
        c.CreditConsumed,
        c.CreditsRemainingAfter,
        DateTime.SpecifyKind(c.OccurredAtUtc, DateTimeKind.Utc));
}

/// <summary>Mensajes legibles para mostrar en el molinete / recepción.</summary>
public static class CheckInMessages
{
    public static string For(CheckIn c)
    {
        if (c.Result == CheckInResult.Approved)
        {
            return c.CreditConsumed
                ? "Ingreso permitido. Se descontó un crédito."
                : "Ingreso permitido.";
        }

        return c.DenialReason switch
        {
            DenialReason.NoActiveMembership => "El socio no tiene una membresía vigente.",
            DenialReason.OverdueDebt => "El socio tiene deudas vencidas por encima del monto de tolerancia.",
            DenialReason.OutsideTimeSlot => "El ingreso está fuera de la franja horaria de la membresía.",
            DenialReason.NoCredits => "La membresía no tiene créditos disponibles.",
            DenialReason.MemberNotFound => "No existe un socio con ese DNI.",
            _ => "Ingreso rechazado."
        };
    }
}
