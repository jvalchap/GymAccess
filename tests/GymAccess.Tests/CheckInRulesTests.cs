using GymAccess.Domain;
using static GymAccess.Tests.TestData;

namespace GymAccess.Tests;

public class CheckInRulesTests
{
    // ---------- Aprobado ----------

    [Fact]
    public void Approves_member_with_active_unlimited_membership()
    {
        var decision = Evaluate(new[] { NewMembership() });

        Assert.True(decision.Allowed);
        Assert.Null(decision.Reason);
        Assert.False(decision.CreditConsumed);
    }

    // ---------- Regla 1: membresía vigente ----------

    [Fact]
    public void Denies_when_member_has_no_memberships()
    {
        var decision = Evaluate(Array.Empty<Membership>());

        Assert.False(decision.Allowed);
        Assert.Equal(DenialReason.NoActiveMembership, decision.Reason);
    }

    [Fact]
    public void Denies_when_membership_expired_yesterday()
    {
        var decision = Evaluate(new[] { NewMembership(start: Today.AddDays(-60), end: Today.AddDays(-1)) });

        Assert.Equal(DenialReason.NoActiveMembership, decision.Reason);
    }

    [Fact]
    public void Denies_when_membership_starts_tomorrow()
    {
        var decision = Evaluate(new[] { NewMembership(start: Today.AddDays(1), end: Today.AddDays(30)) });

        Assert.Equal(DenialReason.NoActiveMembership, decision.Reason);
    }

    [Fact]
    public void Approves_on_first_and_last_day_of_membership()
    {
        var startsToday = Evaluate(new[] { NewMembership(start: Today, end: Today.AddDays(30)) });
        var endsToday = Evaluate(new[] { NewMembership(start: Today.AddDays(-30), end: Today) });

        Assert.True(startsToday.Allowed);
        Assert.True(endsToday.Allowed);
    }

    [Fact]
    public void Uses_gym_local_date_not_utc_date_to_decide_vigencia()
    {
        // 01:00 UTC del 3/11 son las 22:00 del 2/11 en Buenos Aires: la membresía que vence el 2/11 todavía sirve.
        var options = DefaultOptions();
        options.TimeZoneId = "America/Argentina/Buenos_Aires";
        var nowUtc = new DateTime(2026, 11, 3, 1, 0, 0, DateTimeKind.Utc);

        var decision = Evaluate(
            new[] { NewMembership(start: Today.AddDays(-30), end: Today) },
            now: nowUtc,
            options: options);

        Assert.True(decision.Allowed);
    }

    // ---------- Regla 2: deuda vencida ----------

    [Fact]
    public void Denies_when_overdue_pending_debt_exceeds_tolerance()
    {
        var decision = Evaluate(
            new[] { NewMembership() },
            debts: new[] { NewDebt(1500m, Today.AddDays(-1)) });

        Assert.False(decision.Allowed);
        Assert.Equal(DenialReason.OverdueDebt, decision.Reason);
    }

    [Fact]
    public void Approves_when_overdue_debt_is_exactly_the_tolerance()
    {
        var decision = Evaluate(
            new[] { NewMembership() },
            debts: new[] { NewDebt(1000m, Today.AddDays(-1)) });

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Approves_when_overdue_debt_is_below_tolerance()
    {
        var decision = Evaluate(
            new[] { NewMembership() },
            debts: new[] { NewDebt(500m, Today.AddDays(-10)) });

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Sums_all_overdue_debts_against_the_tolerance()
    {
        var decision = Evaluate(
            new[] { NewMembership() },
            debts: new[] { NewDebt(600m, Today.AddDays(-5)), NewDebt(600m, Today.AddDays(-1)) });

        Assert.Equal(DenialReason.OverdueDebt, decision.Reason);
    }

    [Fact]
    public void Ignores_paid_debts()
    {
        var decision = Evaluate(
            new[] { NewMembership() },
            debts: new[] { NewDebt(50_000m, Today.AddDays(-30), DebtStatus.Paid) });

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Ignores_debts_that_are_not_overdue_yet()
    {
        var decision = Evaluate(
            new[] { NewMembership() },
            debts: new[] { NewDebt(50_000m, Today), NewDebt(50_000m, Today.AddDays(10)) });

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Tolerance_is_configurable()
    {
        var debts = new[] { NewDebt(1500m, Today.AddDays(-1)) };

        var strict = Evaluate(new[] { NewMembership() }, debts, options: DefaultOptions(debtTolerance: 0m));
        var lenient = Evaluate(new[] { NewMembership() }, debts, options: DefaultOptions(debtTolerance: 2000m));

        Assert.Equal(DenialReason.OverdueDebt, strict.Reason);
        Assert.True(lenient.Allowed);
    }

    // ---------- Regla 3: franja horaria ----------

    [Fact]
    public void Approves_inside_time_slot()
    {
        var decision = Evaluate(new[]
        {
            NewMembership(slotStart: new TimeOnly(14, 0), slotEnd: new TimeOnly(16, 0))
        });

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Denies_outside_time_slot()
    {
        var decision = Evaluate(new[]
        {
            NewMembership(slotStart: new TimeOnly(8, 0), slotEnd: new TimeOnly(12, 0))
        });

        Assert.False(decision.Allowed);
        Assert.Equal(DenialReason.OutsideTimeSlot, decision.Reason);
    }

    [Fact]
    public void Time_slot_bounds_are_inclusive()
    {
        var atStart = Evaluate(new[] { NewMembership(slotStart: new TimeOnly(15, 0), slotEnd: new TimeOnly(18, 0)) });
        var atEnd = Evaluate(new[] { NewMembership(slotStart: new TimeOnly(12, 0), slotEnd: new TimeOnly(15, 0)) });

        Assert.True(atStart.Allowed);
        Assert.True(atEnd.Allowed);
    }

    [Fact]
    public void Time_slot_can_cross_midnight()
    {
        var slot = (start: new TimeOnly(22, 0), end: new TimeOnly(2, 0));

        var at2330 = Evaluate(
            new[] { NewMembership(slotStart: slot.start, slotEnd: slot.end) },
            now: new DateTime(2026, 11, 2, 23, 30, 0, DateTimeKind.Utc));
        var at0100 = Evaluate(
            new[] { NewMembership(slotStart: slot.start, slotEnd: slot.end) },
            now: new DateTime(2026, 11, 3, 1, 0, 0, DateTimeKind.Utc));
        var at1200 = Evaluate(
            new[] { NewMembership(slotStart: slot.start, slotEnd: slot.end) },
            now: new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(at2330.Allowed);
        Assert.True(at0100.Allowed);
        Assert.Equal(DenialReason.OutsideTimeSlot, at1200.Reason);
    }

    // ---------- Regla 4: créditos ----------

    [Fact]
    public void Approves_and_consumes_a_credit_when_membership_has_credits()
    {
        var decision = Evaluate(new[] { NewMembership(creditsRemaining: 3) });

        Assert.True(decision.Allowed);
        Assert.True(decision.CreditConsumed);
    }

    [Fact]
    public void Denies_when_no_credits_left()
    {
        var decision = Evaluate(new[] { NewMembership(creditsRemaining: 0) });

        Assert.False(decision.Allowed);
        Assert.Equal(DenialReason.NoCredits, decision.Reason);
    }

    [Fact]
    public void Unlimited_membership_never_consumes_credits()
    {
        var decision = Evaluate(new[] { NewMembership(creditsRemaining: null) });

        Assert.True(decision.Allowed);
        Assert.False(decision.CreditConsumed);
    }

    // ---------- Regla 5: reingreso dentro de las 2 horas ----------

    [Fact]
    public void Reentry_within_two_hours_does_not_consume_another_credit()
    {
        var decision = Evaluate(
            new[] { NewMembership(id: 7, creditsRemaining: 5) },
            checkIns: new[] { NewCreditCheckIn(7, Now.AddMinutes(-90)) });

        Assert.True(decision.Allowed);
        Assert.False(decision.CreditConsumed);
    }

    [Fact]
    public void Reentry_is_allowed_even_when_the_last_credit_was_just_used()
    {
        var decision = Evaluate(
            new[] { NewMembership(id: 7, creditsRemaining: 0) },
            checkIns: new[] { NewCreditCheckIn(7, Now.AddMinutes(-30)) });

        Assert.True(decision.Allowed);
        Assert.False(decision.CreditConsumed);
    }

    [Fact]
    public void Reentry_window_includes_exactly_two_hours()
    {
        var decision = Evaluate(
            new[] { NewMembership(id: 7, creditsRemaining: 0) },
            checkIns: new[] { NewCreditCheckIn(7, Now.AddHours(-2)) });

        Assert.True(decision.Allowed);
        Assert.False(decision.CreditConsumed);
    }

    [Fact]
    public void After_two_hours_a_new_credit_is_required()
    {
        var withCredit = Evaluate(
            new[] { NewMembership(id: 7, creditsRemaining: 4) },
            checkIns: new[] { NewCreditCheckIn(7, Now.AddHours(-3)) });
        var withoutCredit = Evaluate(
            new[] { NewMembership(id: 7, creditsRemaining: 0) },
            checkIns: new[] { NewCreditCheckIn(7, Now.AddHours(-3)) });

        Assert.True(withCredit.Allowed);
        Assert.True(withCredit.CreditConsumed);
        Assert.Equal(DenialReason.NoCredits, withoutCredit.Reason);
    }

    [Fact]
    public void Reentries_do_not_extend_the_window()
    {
        // Crédito usado hace 3 h; reingreso (sin crédito) hace 1 h. La ventana se mide desde el crédito, no desde el reingreso.
        var reentry = new CheckIn
        {
            MemberId = 1,
            MembershipId = 7,
            OccurredAtUtc = Now.AddHours(-1),
            Result = CheckInResult.Approved,
            CreditConsumed = false
        };

        var decision = Evaluate(
            new[] { NewMembership(id: 7, creditsRemaining: 0) },
            checkIns: new[] { NewCreditCheckIn(7, Now.AddHours(-3)), reentry });

        Assert.Equal(DenialReason.NoCredits, decision.Reason);
    }

    [Fact]
    public void Denied_check_ins_do_not_open_a_reentry_window()
    {
        var denied = new CheckIn
        {
            MemberId = 1,
            OccurredAtUtc = Now.AddMinutes(-10),
            Result = CheckInResult.Denied,
            DenialReason = DenialReason.NoCredits,
            CreditConsumed = false
        };

        var decision = Evaluate(new[] { NewMembership(id: 7, creditsRemaining: 0) }, checkIns: new[] { denied });

        Assert.Equal(DenialReason.NoCredits, decision.Reason);
    }

    [Fact]
    public void Reentry_does_not_bypass_overdue_debt()
    {
        var decision = Evaluate(
            new[] { NewMembership(id: 7, creditsRemaining: 0) },
            debts: new[] { NewDebt(5000m, Today.AddDays(-1)) },
            checkIns: new[] { NewCreditCheckIn(7, Now.AddMinutes(-30)) });

        Assert.Equal(DenialReason.OverdueDebt, decision.Reason);
    }

    [Fact]
    public void Reentry_does_not_bypass_time_slot()
    {
        var decision = Evaluate(
            new[]
            {
                NewMembership(
                    id: 7,
                    creditsRemaining: 1,
                    slotStart: new TimeOnly(8, 0),
                    slotEnd: new TimeOnly(14, 30))
            },
            checkIns: new[] { NewCreditCheckIn(7, Now.AddMinutes(-30)) });

        Assert.Equal(DenialReason.OutsideTimeSlot, decision.Reason);
    }

    // ---------- Precedencia y varias membresías ----------

    [Fact]
    public void Reports_the_first_failing_rule_in_priority_order()
    {
        // Falla la deuda (regla 2) y la franja (regla 3): se informa la deuda.
        var decision = Evaluate(
            new[] { NewMembership(slotStart: new TimeOnly(8, 0), slotEnd: new TimeOnly(10, 0)) },
            debts: new[] { NewDebt(5000m, Today.AddDays(-1)) });

        Assert.Equal(DenialReason.OverdueDebt, decision.Reason);
    }

    [Fact]
    public void Expired_membership_takes_precedence_over_overdue_debt()
    {
        var decision = Evaluate(
            new[] { NewMembership(start: Today.AddDays(-60), end: Today.AddDays(-1)) },
            debts: new[] { NewDebt(5000m, Today.AddDays(-1)) });

        Assert.Equal(DenialReason.NoActiveMembership, decision.Reason);
    }

    [Fact]
    public void Prefers_unlimited_membership_over_one_with_credits()
    {
        var decision = Evaluate(new[]
        {
            NewMembership(id: 1, creditsRemaining: 5),
            NewMembership(id: 2, creditsRemaining: null)
        });

        Assert.True(decision.Allowed);
        Assert.Equal(2, decision.Membership!.Id);
        Assert.False(decision.CreditConsumed);
    }

    [Fact]
    public void Falls_back_to_another_membership_when_the_first_is_outside_its_slot()
    {
        var decision = Evaluate(new[]
        {
            NewMembership(id: 1, slotStart: new TimeOnly(6, 0), slotEnd: new TimeOnly(9, 0)),
            NewMembership(id: 2, creditsRemaining: 3)
        });

        Assert.True(decision.Allowed);
        Assert.Equal(2, decision.Membership!.Id);
        Assert.True(decision.CreditConsumed);
    }

    [Fact]
    public void Ignores_expired_memberships_when_another_one_is_active()
    {
        var decision = Evaluate(new[]
        {
            NewMembership(id: 1, start: Today.AddDays(-90), end: Today.AddDays(-30)),
            NewMembership(id: 2)
        });

        Assert.True(decision.Allowed);
        Assert.Equal(2, decision.Membership!.Id);
    }
}
