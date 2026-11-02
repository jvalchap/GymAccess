using GymAccess.Contracts;
using GymAccess.Data;
using GymAccess.Domain;
using GymAccess.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymAccess.Controllers;

[ApiController]
[Route("debts")]
public class DebtsController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;

    public DebtsController(GymDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Marca una deuda como pagada. Pagar una deuda ya pagada devuelve 409.</summary>
    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<DebtResponse>> Pay(int id, CancellationToken ct)
    {
        var debt = await _db.Debts.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (debt is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Deuda no encontrada",
                detail: $"No existe una deuda con id {id}.");
        }

        if (debt.Status == DebtStatus.Paid)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Deuda ya pagada",
                detail: $"La deuda {id} ya estaba marcada como pagada.");
        }

        debt.Status = DebtStatus.Paid;
        debt.PaidAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(DebtResponse.From(debt));
    }
}
