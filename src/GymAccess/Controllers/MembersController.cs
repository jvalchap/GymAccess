using GymAccess.Contracts;
using GymAccess.Data;
using GymAccess.Domain;
using GymAccess.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymAccess.Controllers;

[ApiController]
[Route("members")]
public class MembersController : ControllerBase
{
    private readonly GymDbContext _db;

    public MembersController(GymDbContext db)
    {
        _db = db;
    }

    /// <summary>Crea un socio. El DNI es único.</summary>
    [HttpPost]
    public async Task<ActionResult<MemberResponse>> Create(CreateMemberRequest request, CancellationToken ct)
    {
        var dni = DniNormalizer.Normalize(request.Dni);
        if (!DniNormalizer.IsValid(dni))
        {
            ModelState.AddModelError(nameof(request.Dni), "El DNI debe tener entre 6 y 9 dígitos.");
            return ValidationProblem(ModelState);
        }

        if (await _db.Members.AnyAsync(m => m.Dni == dni, ct))
        {
            return DniConflict(dni);
        }

        var member = new Member { Name = request.Name.Trim(), Dni = dni };
        _db.Members.Add(member);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Carrera: otro request creó el mismo DNI entre el AnyAsync y el SaveChanges (índice único).
            return DniConflict(dni);
        }

        return CreatedAtAction(nameof(GetById), new { id = member.Id }, MemberResponse.From(member));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MemberResponse>> GetById(int id, CancellationToken ct)
    {
        var member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member is null)
        {
            return MemberNotFound(id);
        }

        return Ok(MemberResponse.From(member));
    }

    /// <summary>Asigna una membresía. Sin "credits" los ingresos son ilimitados; sin franja, a cualquier hora.</summary>
    [HttpPost("{id:int}/memberships")]
    public async Task<ActionResult<MembershipResponse>> AddMembership(
        int id, CreateMembershipRequest request, CancellationToken ct)
    {
        if (!await _db.Members.AnyAsync(m => m.Id == id, ct))
        {
            return MemberNotFound(id);
        }

        var start = request.StartDate!.Value;
        var end = request.EndDate!.Value;

        if (end < start)
        {
            ModelState.AddModelError(nameof(request.EndDate), "La fecha de vencimiento no puede ser anterior a la de inicio.");
        }

        if (request.Credits is < 0)
        {
            ModelState.AddModelError(nameof(request.Credits), "Los créditos no pueden ser negativos.");
        }

        TimeOnly? slotStart = null;
        TimeOnly? slotEnd = null;

        if (request.SlotStart is not null || request.SlotEnd is not null)
        {
            if (request.SlotStart is null || request.SlotEnd is null)
            {
                ModelState.AddModelError("Slot", "La franja horaria requiere slotStart y slotEnd.");
            }
            else
            {
                if (TimeParser.TryParse(request.SlotStart, out var parsedStart))
                {
                    slotStart = parsedStart;
                }
                else
                {
                    ModelState.AddModelError(nameof(request.SlotStart), "Formato inválido, use HH:mm.");
                }

                if (TimeParser.TryParse(request.SlotEnd, out var parsedEnd))
                {
                    slotEnd = parsedEnd;
                }
                else
                {
                    ModelState.AddModelError(nameof(request.SlotEnd), "Formato inválido, use HH:mm.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var membership = new Membership
        {
            MemberId = id,
            StartDate = start,
            EndDate = end,
            Credits = request.Credits,
            CreditsRemaining = request.Credits,
            SlotStart = slotStart,
            SlotEnd = slotEnd
        };

        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, MembershipResponse.From(membership));
    }

    /// <summary>Registra una deuda pendiente.</summary>
    [HttpPost("{id:int}/debts")]
    public async Task<ActionResult<DebtResponse>> AddDebt(int id, CreateDebtRequest request, CancellationToken ct)
    {
        if (!await _db.Members.AnyAsync(m => m.Id == id, ct))
        {
            return MemberNotFound(id);
        }

        var debt = new Debt
        {
            MemberId = id,
            Amount = request.Amount!.Value,
            DueDate = request.DueDate!.Value,
            Status = DebtStatus.Pending
        };

        _db.Debts.Add(debt);
        await _db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, DebtResponse.From(debt));
    }

    /// <summary>Historial de ingresos del socio, del más reciente al más antiguo.</summary>
    [HttpGet("{id:int}/check-ins")]
    public async Task<ActionResult<IReadOnlyList<CheckInResponse>>> GetCheckIns(int id, CancellationToken ct)
    {
        if (!await _db.Members.AnyAsync(m => m.Id == id, ct))
        {
            return MemberNotFound(id);
        }

        var checkIns = await _db.CheckIns.AsNoTracking()
            .Where(c => c.MemberId == id)
            .OrderByDescending(c => c.OccurredAtUtc)
            .ThenByDescending(c => c.Id)
            .ToListAsync(ct);

        return Ok(checkIns.Select(CheckInResponse.From).ToList());
    }

    private ActionResult MemberNotFound(int id) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Socio no encontrado", detail: $"No existe un socio con id {id}.");

    private ActionResult DniConflict(string dni) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "DNI duplicado", detail: $"Ya existe un socio con DNI {dni}.");
}
