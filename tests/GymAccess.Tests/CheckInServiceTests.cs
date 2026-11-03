using GymAccess.Data;
using GymAccess.Domain;
using GymAccess.Rules;
using GymAccess.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymAccess.Tests;

/// <summary>Prueban la orquestación contra SQLite en memoria: descuento de créditos, reingreso y registro de intentos.</summary>
public sealed class CheckInServiceTests : IDisposable
{
    private const string Dni = "30111222";

    private readonly SqliteConnection _connection;
    private readonly GymDbContext _db;
    private readonly FakeClock _clock = new(TestData.Now);

    public CheckInServiceTests()
    {
        // La base en memoria vive mientras la conexión esté abierta.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GymDbContext>().UseSqlite(_connection).Options;
        _db = new GymDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private CheckInService NewService()
    {
        var gymOptions = Options.Create(TestData.DefaultOptions());
        var evaluator = new CheckInEvaluator(CheckInRules.CreateDefault(), gymOptions);
        return new CheckInService(_db, evaluator, _clock, gymOptions);
    }

    private async Task<Membership> SeedMemberWithMembershipAsync(int? credits)
    {
        var member = new Member { Name = "Ana Pérez", Dni = Dni };
        var membership = new Membership
        {
            StartDate = TestData.Today.AddDays(-30),
            EndDate = TestData.Today.AddDays(30),
            Credits = credits,
            CreditsRemaining = credits
        };
        member.Memberships.Add(membership);

        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        return membership;
    }

    private async Task<int?> StoredCreditsAsync() =>
        (await _db.Memberships.AsNoTracking().SingleAsync()).CreditsRemaining;

    [Fact]
    public async Task Unknown_dni_is_denied_and_the_attempt_is_recorded()
    {
        var result = await NewService().RegisterAsync("99999999");

        Assert.Equal(CheckInResult.Denied, result.Result);
        Assert.Equal(DenialReason.MemberNotFound, result.DenialReason);
        Assert.Null(result.MemberId);
        Assert.False(result.CreditConsumed);

        var stored = await _db.CheckIns.AsNoTracking().SingleAsync();
        Assert.Equal("99999999", stored.PresentedDni);
        Assert.Null(stored.MemberId);
        Assert.Equal(DenialReason.MemberNotFound, stored.DenialReason);
    }

    [Fact]
    public async Task Approved_check_in_with_credits_consumes_one_and_is_persisted()
    {
        var membership = await SeedMemberWithMembershipAsync(credits: 2);

        var checkIn = await NewService().RegisterAsync(Dni);

        Assert.NotNull(checkIn);
        Assert.Equal(CheckInResult.Approved, checkIn!.Result);
        Assert.True(checkIn.CreditConsumed);
        Assert.Equal(1, checkIn.CreditsRemainingAfter);
        Assert.Equal(membership.Id, checkIn.MembershipId);
        Assert.Equal(1, await StoredCreditsAsync());
        Assert.Single(await _db.CheckIns.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Second_check_in_within_two_hours_does_not_consume_another_credit()
    {
        await SeedMemberWithMembershipAsync(credits: 2);
        var service = NewService();

        await service.RegisterAsync(Dni);
        _clock.UtcNow = TestData.Now.AddMinutes(45);
        var reentry = await service.RegisterAsync(Dni);

        Assert.Equal(CheckInResult.Approved, reentry!.Result);
        Assert.False(reentry.CreditConsumed);
        Assert.Equal(1, reentry.CreditsRemainingAfter);
        Assert.Equal(1, await StoredCreditsAsync());
    }

    [Fact]
    public async Task Last_credit_can_be_reentered_and_then_is_exhausted_after_the_window()
    {
        await SeedMemberWithMembershipAsync(credits: 1);
        var service = NewService();

        var first = await service.RegisterAsync(Dni);
        _clock.UtcNow = TestData.Now.AddMinutes(30);
        var reentry = await service.RegisterAsync(Dni);
        _clock.UtcNow = TestData.Now.AddHours(3);
        var afterWindow = await service.RegisterAsync(Dni);

        Assert.True(first!.CreditConsumed);
        Assert.Equal(0, first.CreditsRemainingAfter);

        Assert.Equal(CheckInResult.Approved, reentry!.Result);
        Assert.False(reentry.CreditConsumed);

        Assert.Equal(CheckInResult.Denied, afterWindow!.Result);
        Assert.Equal(DenialReason.NoCredits, afterWindow.DenialReason);
        Assert.Equal(0, await StoredCreditsAsync());
    }

    [Fact]
    public async Task Unlimited_membership_is_approved_every_time_without_touching_credits()
    {
        await SeedMemberWithMembershipAsync(credits: null);
        var service = NewService();

        var first = await service.RegisterAsync(Dni);
        _clock.UtcNow = TestData.Now.AddHours(5);
        var second = await service.RegisterAsync(Dni);

        Assert.Equal(CheckInResult.Approved, first!.Result);
        Assert.Equal(CheckInResult.Approved, second!.Result);
        Assert.False(first.CreditConsumed);
        Assert.False(second.CreditConsumed);
        Assert.Null(await StoredCreditsAsync());
    }

    [Fact]
    public async Task Denied_attempts_are_recorded_with_their_reason_and_consume_nothing()
    {
        await SeedMemberWithMembershipAsync(credits: 3);
        var member = await _db.Members.SingleAsync();
        _db.Debts.Add(new Debt { MemberId = member.Id, Amount = 5000m, DueDate = TestData.Today.AddDays(-1) });
        await _db.SaveChangesAsync();

        var checkIn = await NewService().RegisterAsync(Dni);

        Assert.Equal(CheckInResult.Denied, checkIn!.Result);
        Assert.Equal(DenialReason.OverdueDebt, checkIn.DenialReason);
        Assert.False(checkIn.CreditConsumed);
        Assert.Null(checkIn.MembershipId);
        Assert.Equal(3, await StoredCreditsAsync());

        var stored = await _db.CheckIns.AsNoTracking().SingleAsync();
        Assert.Equal(CheckInResult.Denied, stored.Result);
        Assert.Equal(DenialReason.OverdueDebt, stored.DenialReason);
    }

    [Fact]
    public async Task Paying_the_debt_lets_the_member_in_again()
    {
        await SeedMemberWithMembershipAsync(credits: null);
        var member = await _db.Members.SingleAsync();
        var debt = new Debt { MemberId = member.Id, Amount = 5000m, DueDate = TestData.Today.AddDays(-1) };
        _db.Debts.Add(debt);
        await _db.SaveChangesAsync();
        var service = NewService();

        var before = await service.RegisterAsync(Dni);
        debt.Status = DebtStatus.Paid;
        await _db.SaveChangesAsync();
        var after = await service.RegisterAsync(Dni);

        Assert.Equal(CheckInResult.Denied, before!.Result);
        Assert.Equal(CheckInResult.Approved, after!.Result);
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime now) => UtcNow = now;

        public DateTime UtcNow { get; set; }
    }
}
