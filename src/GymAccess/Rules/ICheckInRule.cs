using GymAccess.Domain;

namespace GymAccess.Rules;

/// <summary>
/// Una regla de ingreso. Es pura: mira el contexto y devuelve el motivo de rechazo,
/// o null si la regla se cumple. No toca la base de datos ni modifica nada.
/// </summary>
public interface ICheckInRule
{
    DenialReason? Evaluate(CheckInContext context);
}
