using GymAccess.Domain;

namespace GymAccess.Rules;

/// <summary>Regla 3: si la membresía tiene franja horaria, la hora local del ingreso debe estar dentro.</summary>
public sealed class TimeSlotRule : ICheckInRule
{
    public DenialReason? Evaluate(CheckInContext context)
    {
        if (context.Membership.IsWithinSlot(context.LocalTime))
        {
            return null;
        }

        return DenialReason.OutsideTimeSlot;
    }
}
