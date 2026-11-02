using GymAccess.Configuration;
using GymAccess.Domain;
using Microsoft.Extensions.Options;

namespace GymAccess.Rules;

/// <summary>Todo lo que el evaluador necesita saber de un socio para decidir. Sin acceso a datos.</summary>
public sealed record CheckInInput(
    IReadOnlyList<Membership> Memberships,
    IReadOnlyList<Debt> Debts,
    IReadOnlyList<CheckIn> RecentCheckIns,
    DateTime NowUtc);

/// <summary>
/// Decide si un socio puede ingresar. Es una función pura: no consulta la base ni modifica estado
/// (descontar el crédito y persistir el ingreso lo hace <c>CheckInService</c>).
/// </summary>
public sealed class CheckInEvaluator
{
    private readonly IReadOnlyList<ICheckInRule> _rules;
    private readonly GymOptions _options;
    private readonly TimeZoneInfo _timeZone;

    public CheckInEvaluator(IEnumerable<ICheckInRule> rules, IOptions<GymOptions> options)
    {
        _rules = rules.ToList();
        _options = options.Value;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
    }

    public CheckInDecision Evaluate(CheckInInput input)
    {
        var nowUtc = DateTime.SpecifyKind(input.NowUtc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _timeZone);
        var today = DateOnly.FromDateTime(local);
        var time = TimeOnly.FromDateTime(local);

        // Regla 5: solo cuentan los ingresos aprobados que descontaron crédito dentro de la ventana.
        // Los reingresos no la renuevan (si no, se podría encadenar reingresos sin pagar nunca otro crédito).
        var windowStart = nowUtc.AddHours(-_options.ReentryWindowHours);
        var creditCheckIns = input.RecentCheckIns
            .Where(c => c.Result == CheckInResult.Approved && c.CreditConsumed && c.OccurredAtUtc >= windowStart)
            .ToList();

        // Si el socio tiene varias membresías, se prueban en este orden y gana la primera que lo deja pasar:
        // vigentes primero, ilimitadas antes que con créditos (para no gastar créditos), luego la que vence antes.
        var candidates = input.Memberships
            .OrderByDescending(m => m.IsActiveOn(today))
            .ThenBy(m => m.UsesCredits)
            .ThenBy(m => m.EndDate)
            .ThenBy(m => m.Id)
            .ToList();

        if (candidates.Count == 0)
        {
            return CheckInDecision.Denied(DenialReason.NoActiveMembership);
        }

        DenialReason? firstDenial = null;

        foreach (var membership in candidates)
        {
            var isReentry = creditCheckIns.Any(c => c.MembershipId == membership.Id);

            var context = new CheckInContext
            {
                Membership = membership,
                Debts = input.Debts,
                LocalDate = today,
                LocalTime = time,
                IsReentry = isReentry,
                Options = _options
            };

            var denial = FirstDenial(context);

            if (denial is null)
            {
                return CheckInDecision.Approved(membership, creditConsumed: membership.UsesCredits && !isReentry);
            }

            // El motivo que se informa es el de la mejor candidata (la primera en el orden de arriba).
            firstDenial ??= denial;
        }

        return CheckInDecision.Denied(firstDenial!.Value);
    }

    private DenialReason? FirstDenial(CheckInContext context)
    {
        foreach (var rule in _rules)
        {
            var denial = rule.Evaluate(context);
            if (denial is not null)
            {
                return denial;
            }
        }

        return null;
    }
}
