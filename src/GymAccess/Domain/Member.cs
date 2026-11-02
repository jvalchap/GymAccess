namespace GymAccess.Domain;

public class Member
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>DNI normalizado (solo dígitos). Único.</summary>
    public string Dni { get; set; } = string.Empty;

    public List<Membership> Memberships { get; set; } = new();
    public List<Debt> Debts { get; set; } = new();
}
