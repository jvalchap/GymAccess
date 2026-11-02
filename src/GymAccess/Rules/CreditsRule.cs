using GymAccess.Domain;

namespace GymAccess.Rules;

/// <summary>
/// Regla 4: si la membresía usa créditos, queda al menos uno.
/// Un reingreso dentro de la ventana (regla 5) no necesita crédito, aunque el saldo sea 0.
/// </summary>
public sealed class CreditsRule : ICheckInRule
{
    public DenialReason? Evaluate(CheckInContext context)
    {
        var membership = context.Membership;

        if (!membership.UsesCredits || context.IsReentry)
        {
            return null;
        }

        if (membership.CreditsRemaining > 0)
        {
            return null;
        }

        return DenialReason.NoCredits;
    }
}
