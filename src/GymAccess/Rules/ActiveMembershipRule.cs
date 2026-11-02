using GymAccess.Domain;

namespace GymAccess.Rules;

/// <summary>Regla 1: la membresía está vigente (fecha local entre inicio y vencimiento, inclusive).</summary>
public sealed class ActiveMembershipRule : ICheckInRule
{
    public DenialReason? Evaluate(CheckInContext context)
    {
        if (context.Membership.IsActiveOn(context.LocalDate))
        {
            return null;
        }

        return DenialReason.NoActiveMembership;
    }
}
