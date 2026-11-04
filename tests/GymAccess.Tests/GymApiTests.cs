using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GymAccess.Tests;

public class GymApiTests : IClassFixture<GymApiFactory>
{
    private static int _dniSeed = 30_000_000;

    private readonly HttpClient _client;

    [Fact]
    public async Task Create_member_returns_201_with_location_and_normalized_dni()
    {
        var response = await _client.PostAsJsonAsync("/members", new { name = "  Ana PŠrez ", dni = "40.111.222" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadAsync(response);
        Assert.Equal("Ana PŠrez", body.GetProperty("name").GetString());
        Assert.Equal("40111222", body.GetProperty("dni").GetString());
    }

    [Fact]
    public async Task Create_member_with_duplicate_dni_returns_409()
    {
        var (_, dni) = await CreateMemberAsync();

        var response = await _client.PostAsJsonAsync("/members", new { name = "Otra persona", dni });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

    [Fact]
    public async Task Check_in_is_approved_for_an_active_unlimited_membership()
    {
        var (id, dni) = await CreateMemberAsync();
        await AddMembershipAsync(id, new { startDate = Day(-30), endDate = Day(30) });

        var body = await CheckInAsync(dni);

        Assert.True(body.GetProperty("allowed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("reason").ValueKind);
        Assert.False(body.GetProperty("creditConsumed").GetBoolean());
    }

    [Fact]
    public async Task Check_in_is_denied_with_NoActiveMembership_when_the_membership_expired()
    {
        var (id, dni) = await CreateMemberAsync();
        await AddMembershipAsync(id, new { startDate = Day(-60), endDate = Day(-1) });

        AssertDenied(await CheckInAsync(dni), "NoActiveMembership");
    }

    [Fact]
    public async Task Check_in_is_denied_with_OverdueDebt_and_allowed_again_after_paying()
    {
        var (id, dni) = await CreateMemberAsync();
        await AddMembershipAsync(id, new { startDate = Day(-30), endDate = Day(30) });
        var debtId = await AddDebtAsync(id, amount: 5000, dueDate: Day(-1));

        AssertDenied(await CheckInAsync(dni), "OverdueDebt");

        var pay = await _client.PostAsync($"/debts/{debtId}/pay", content: null);
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        Assert.Equal("Paid", (await ReadAsync(pay)).GetProperty("status").GetString());

        Assert.True((await CheckInAsync(dni)).GetProperty("allowed").GetBoolean());
    }


    [Fact]
    public async Task Overdue_debt_within_the_tolerance_does_not_block_the_check_in()
    {
        var (id, dni) = await CreateMemberAsync();
        await AddMembershipAsync(id, new { startDate = Day(-30), endDate = Day(30) });
        await AddDebtAsync(id, amount: 500, dueDate: Day(-1)); // tolerancia por defecto: 1000

        Assert.True((await CheckInAsync(dni)).GetProperty("allowed").GetBoolean());
    }

    [Fact]
    public async Task Check_in_is_denied_with_OutsideTimeSlot_and_approved_inside_the_slot()
    {
        // Hora local del reloj de test: 12:00.
        var (outsideId, outsideDni) = await CreateMemberAsync();
        await AddMembershipAsync(outsideId, new { startDate = Day(-30), endDate = Day(30), slotStart = "08:00", slotEnd = "10:00" });

        var (insideId, insideDni) = await CreateMemberAsync();
        await AddMembershipAsync(insideId, new { startDate = Day(-30), endDate = Day(30), slotStart = "11:00", slotEnd = "13:00" });

        AssertDenied(await CheckInAsync(outsideDni), "OutsideTimeSlot");
        Assert.True((await CheckInAsync(insideDni)).GetProperty("allowed").GetBoolean());
    }

    [Fact]
    [Fact]
    public async Task Credit_is_consumed_on_the_first_check_in_and_not_on_the_reentry()
    {
        var (id, dni) = await CreateMemberAsync();
        await AddMembershipAsync(id, new { startDate = Day(-30), endDate = Day(30), credits = 2 });

        var first = await CheckInAsync(dni);
        var reentry = await CheckInAsync(dni);

        Assert.True(first.GetProperty("creditConsumed").GetBoolean());
        Assert.Equal(1, first.GetProperty("creditsRemaining").GetInt32());

        Assert.True(reentry.GetProperty("allowed").GetBoolean());
        Assert.False(reentry.GetProperty("creditConsumed").GetBoolean());
        Assert.Equal(1, reentry.GetProperty("creditsRemaining").GetInt32());
    }


    [Fact]
    public async Task Check_in_history_lists_every_attempt_newest_first()
    {
        var (id, dni) = await CreateMemberAsync();

        AssertDenied(await CheckInAsync(dni), "NoActiveMembership");               // 1Âº: todavÃ­a sin miembrosÃ­a
        await AddMembershipAsync(id, new { startDate = Day(-30), endDate = Day(30) });
        Assert.True((await CheckInAsync(dni)).GetProperty("allowed").GetBoolean()); // 2Âº: aprobado

        var response = await _client.GetAsync($"/members/{id}/check-ins");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await ReadAsync(response);
        Assert.Equal(2, history.GetArrayLength());
        Assert.True(history[0].GetProperty("allowed").GetBoolean());
        Assert.False(history[1].GetProperty("allowed").GetBoolean());
        Assert.Equal("NoActiveMembership", history[1].GetProperty("reason").GetString());
    }

    // ---------- Validaciones y 404 ----------

    [Fact]
    public async Task Membership_ending_before_it_starts_returns_400()
    {
        var (id, _) = await CreateMemberAsync();

        var response = await _client.PostAsJsonAsync($"/members/{id}/memberships", new { startDate = Day(30), endDate = Day(-30) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Membership_with_only_one_end_of_the_time_slot_returns_400()
    {
        var (id, _) = await CreateMemberAsync();

        var response = await _client.PostAsJsonAsync(
            $"/members/{id}/memberships",
            new { startDate = Day(-30), endDate = Day(30), slotStart = "08:00" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Debt_with_a_negative_amount_returns_400()
    {
        var (id, _) = await CreateMemberAsync();

        var response = await _client.PostAsJsonAsync($"/members/{id}/debts", new { amount = -10, dueDate = Day(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Paying_the_same_debt_twice_returns_409()
    {
        var (id, _) = await CreateMemberAsync();
        var debtId = await AddDebtAsync(id, amount: 100, dueDate: Day(-1));

        var first = await _client.PostAsync($"/debts/{debtId}/pay", content: null);
        var second = await _client.PostAsync($"/debts/{debtId}/pay", content: null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Unknown_ids_return_404()
    {
        var membership = await _client.PostAsJsonAsync("/members/999999/memberships", new { startDate = Day(-1), endDate = Day(1) });
        var debt = await _client.PostAsJsonAsync("/members/999999/debts", new { amount = 100, dueDate = Day(-1) });
        var pay = await _client.PostAsync("/debts/999999/pay", content: null);
        var history = await _client.GetAsync("/members/999999/check-ins");

        Assert.Equal(HttpStatusCode.NotFound, membership.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, debt.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, pay.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
    }

    // ---------- Helpers ----------


    [Fact]
    public async Task Check_in_is_denied_with_NoCredits_when_the_membership_has_no_credits()
    {
        var (id, dni) = await CreateMemberAsync();
        await AddMembershipAsync(id, new { startDate = Day(-30), endDate = Day(30), credits = 0 });

        AssertDenied(await CheckInAsync(dni), "NoCredits");
    }

    [Fact]
    public async Task Check_in_with_an_unknown_dni_is_denied_with_MemberNotFound_and_still_returns_200()
    {
        var body = await CheckInAsync("00000001");

        AssertDenied(body, "MemberNotFound");
        Assert.Equal(JsonValueKind.Null, body.GetProperty("memberId").ValueKind);
    }

    // ---------- CrÃ©ditos y reingreso ----------

    public async Task Create_member_with_invalid_dni_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/members", new { name = "Ana", dni = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Ingresos: aprobado y un caso por motivo de rechazo ----------

    public GymApiTests(GymApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ---------- Socios ----------
}


    private static string Day(int offsetDays) => TestData.Today.AddDays(offsetDays).ToString("yyyy-MM-dd");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(int Id, string Dni)> CreateMemberAsync()
    {
        var dni = Interlocked.Increment(ref _dniSeed).ToString();

        var response = await _client.PostAsJsonAsync("/members", new { name = "Socio de prueba", dni });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ReadAsync(response);
        return (body.GetProperty("id").GetInt32(), dni);
    }

    private async Task AddMembershipAsync(int memberId, object request)
    {
        var response = await _client.PostAsJsonAsync($"/members/{memberId}/memberships", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<int> AddDebtAsync(int memberId, decimal amount, string dueDate)
    {
        var response = await _client.PostAsJsonAsync($"/members/{memberId}/debts", new { amount, dueDate });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await ReadAsync(response)).GetProperty("id").GetInt32();
    }

    /// <summary>Un rechazo es una respuesta vŠlida: siempre 200, con allowed=false y el motivo como texto.</summary>
    private async Task<JsonElement> CheckInAsync(string dni)
    {
        var response = await _client.PostAsJsonAsync("/check-ins", new { dni });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAsync(response);
    }

    private static void AssertDenied(JsonElement body, string reason)
    {
        Assert.False(body.GetProperty("allowed").GetBoolean());
        Assert.Equal(reason, body.GetProperty("reason").GetString());
    }
}

    }
