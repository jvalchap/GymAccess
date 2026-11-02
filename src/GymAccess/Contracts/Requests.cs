using System.ComponentModel.DataAnnotations;

namespace GymAccess.Contracts;

public class CreateMemberRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Acepta "12345678" o "12.345.678".</summary>
    [Required]
    public string Dni { get; set; } = string.Empty;
}

public class CreateMembershipRequest
{
    [Required]
    public DateOnly? StartDate { get; set; }

    [Required]
    public DateOnly? EndDate { get; set; }

    /// <summary>Opcional. Si se omite, los ingresos son ilimitados.</summary>
    public int? Credits { get; set; }

    /// <summary>Opcional, formato HH:mm o HH:mm:ss. Si se informa, también hay que informar slotEnd.</summary>
    public string? SlotStart { get; set; }

    /// <summary>Opcional, formato HH:mm o HH:mm:ss.</summary>
    public string? SlotEnd { get; set; }
}

public class CreateDebtRequest
{
    [Required, Range(0.01, 1_000_000_000)]
    public decimal? Amount { get; set; }

    [Required]
    public DateOnly? DueDate { get; set; }
}

public class CheckInRequest
{
    [Required]
    public string Dni { get; set; } = string.Empty;
}
