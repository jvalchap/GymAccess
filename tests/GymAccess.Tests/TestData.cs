using GymAccess.Configuration;
using GymAccess.Domain;
using GymAccess.Rules;
using Microsoft.Extensions.Options;

namespace GymAccess.Tests;

/// <summary>Builders y helpers compartidos por los tests. La zona horaria de los tests es UTC para que la hora sea obvia.</summary>
internal static class TestData
{
    /// <summary>Lunes 2 de noviembre de 2026, 15:00 UTC.</summary>
    public static readonly DateTime Now = new(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc);

    public static readonly DateOnly Today = new(2026, 11, 2);

    public static GymOptions DefaultOptions(decimal debtTolerance = 1000m) => new()
    {
        DebtTolerance = debtTolerance,
        ReentryWindowHours = 2,
        TimeZoneId = "UTC"
    };

    public static CheckInEvaluator NewEvaluator(GymOptions? options = null) =>
        new(CheckInRules.CreateDefault(), Options.Create(options ?? DefaultOptions()));

    public static CheckInDecision Evaluate(
        IEnumerable<Membership> memberships,
        IEnumerable<Debt>? debts = null,
        IEnumerable<CheckIn>? checkIns = null,
        DateTime? now = null,
        GymOptions? options = null)
    {
        var input = new CheckInInput(
            memberships.ToList(),
            (debts ?? Array.Empty<Debt>()).ToList(),
            (checkIns ?? Array.Empty<CheckIn>()).ToList(),
            now ?? Now);

        return NewEvaluator(options).Evaluate(input);
    }

    /// <summary>Membresía vigente hoy, ilimitada y sin franja, salvo que se indique otra cosa.</summary>
    public static Membership NewMembership(
        int id = 1,
        DateOnly? start = null,
        DateOnly? end = null,
        int? creditsRemaining = null,
        TimeOnly? slotStart = null,
        TimeOnly? slotEnd = null) => new()
    {
        Id = id,
        MemberId = 1,
        StartDate = start ?? Today.AddDays(-30),
        EndDate = end ?? Today.AddDays(30),
        Credits = creditsRemaining,
        CreditsRemaining = creditsRemaining,
        SlotStart = slotStart,
        SlotEnd = slotEnd
    };

    public static Debt NewDebt(decimal amount, DateOnly dueDate, DebtStatus status = DebtStatus.Pending) => new()
    {
        MemberId = 1,
        Amount = amount,
        DueDate = dueDate,
        Status = status
    };

    /// <summary>Ingreso aprobado que descontó un crédito.</summary>
    public static CheckIn NewCreditCheckIn(int membershipId, DateTime at) => new()
    {
        MemberId = 1,
        MembershipId = membershipId,
        OccurredAtUtc = at,
        Result = CheckInResult.Approved,
        CreditConsumed = true
    };
}
