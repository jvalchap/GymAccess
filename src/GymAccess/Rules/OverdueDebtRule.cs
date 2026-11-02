using GymAccess.Domain;

namespace GymAccess.Rules;

/// <summary>
/// Regla 2: el total de deudas pendientes vencidas no supera la tolerancia configurada.
/// Una deuda está vencida si su fecha de vencimiento es anterior a hoy. Igual a la tolerancia se permite.
/// </summary>
public sealed class OverdueDebtRule : ICheckInRule
{
    public DenialReason? Evaluate(CheckInContext context)
    {
        var overdueTotal = context.Debts
            .Where(d => d.IsOverdueOn(context.LocalDate))
            .Sum(d => d.Amount);

        if (overdueTotal > context.Options.DebtTolerance)
        {
            return DenialReason.OverdueDebt;
        }

        return null;
    }
}
