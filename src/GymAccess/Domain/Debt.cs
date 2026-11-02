namespace GymAccess.Domain;

public class Debt
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public DebtStatus Status { get; set; } = DebtStatus.Pending;
    public DateTime? PaidAtUtc { get; set; }

    /// <summary>Pendiente y con fecha de vencimiento anterior a <paramref name="today"/>.</summary>
    public bool IsOverdueOn(DateOnly today) => Status == DebtStatus.Pending && DueDate < today;
}
