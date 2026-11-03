using GymAccess.Configuration;
using GymAccess.Data;
using GymAccess.Domain;
using GymAccess.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymAccess.Services;

/// <summary>
/// Orquesta un ingreso: carga los datos del socio, le pide la decisión al <see cref="CheckInEvaluator"/>,
/// descuenta el crédito si corresponde y registra el intento (todo en un único SaveChanges, o sea atómico).
/// </summary>
public sealed class CheckInService
{
    // Serializa los ingresos dentro del proceso para que dos pasadas simultáneas del mismo socio
    // no descuenten dos veces ni se salteen el reingreso. Suficiente para una instancia con SQLite.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly GymDbContext _db;
    private readonly CheckInEvaluator _evaluator;
    private readonly IClock _clock;
    private readonly GymOptions _options;

    public CheckInService(GymDbContext db, CheckInEvaluator evaluator, IClock clock, IOptions<GymOptions> options)
    {
        _db = db;
        _evaluator = evaluator;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>
    /// Registra el intento de ingreso y devuelve el resultado. Siempre registra: si el DNI no corresponde
    /// a ningún socio, el intento queda rechazado con <see cref="DenialReason.MemberNotFound"/>.
    /// </summary>
    public async Task<CheckIn> RegisterAsync(string dni, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Dni == dni, ct);
            var now = _clock.UtcNow;

            if (member is null)
            {
                var unknown = new CheckIn
                {
                    MemberId = null,
                    PresentedDni = dni,
                    OccurredAtUtc = now,
                    Result = CheckInResult.Denied,
                    DenialReason = DenialReason.MemberNotFound
                };

                _db.CheckIns.Add(unknown);
                await _db.SaveChangesAsync(ct);
                return unknown;
            }

            var windowStart = now.AddHours(-_options.ReentryWindowHours);

            // Memberships queda "tracked": si hay que descontar un crédito, el SaveChanges de abajo lo persiste.
            var memberships = await _db.Memberships.Where(m => m.MemberId == member.Id).ToListAsync(ct);
            var debts = await _db.Debts.AsNoTracking()
                .Where(d => d.MemberId == member.Id && d.Status == DebtStatus.Pending)
                .ToListAsync(ct);
            var recentCheckIns = await _db.CheckIns.AsNoTracking()
                .Where(c => c.MemberId == member.Id && c.OccurredAtUtc >= windowStart)
                .ToListAsync(ct);

            var decision = _evaluator.Evaluate(new CheckInInput(memberships, debts, recentCheckIns, now));

            if (decision.CreditConsumed)
            {
                decision.Membership!.CreditsRemaining -= 1;
            }

            var checkIn = new CheckIn
            {
                MemberId = member.Id,
                PresentedDni = dni,
                MembershipId = decision.Membership?.Id,
                OccurredAtUtc = now,
                Result = decision.Allowed ? CheckInResult.Approved : CheckInResult.Denied,
                DenialReason = decision.Reason,
                CreditConsumed = decision.CreditConsumed,
                CreditsRemainingAfter = decision.Membership?.CreditsRemaining
            };

            _db.CheckIns.Add(checkIn);
            await _db.SaveChangesAsync(ct);

            return checkIn;
        }
        finally
        {
            Gate.Release();
        }
    }
}
