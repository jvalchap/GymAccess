using GymAccess.Contracts;
using GymAccess.Services;
using Microsoft.AspNetCore.Mvc;

namespace GymAccess.Controllers;

[ApiController]
[Route("check-ins")]
public class CheckInsController : ControllerBase
{
    private readonly CheckInService _service;

    public CheckInsController(CheckInService service)
    {
        _service = service;
    }

    /// <summary>
    /// Recibe un DNI y decide el ingreso. Un rechazo es una respuesta válida (200 con allowed=false y el motivo en "reason").
    /// 404 solo si el DNI no corresponde a ningún socio.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CheckInResponse>> Create(CheckInRequest request, CancellationToken ct)
    {
        var dni = DniNormalizer.Normalize(request.Dni);

        var checkIn = await _service.RegisterAsync(dni, ct);
        if (checkIn is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Socio no encontrado",
                detail: $"No existe un socio con DNI {dni}.");
        }

        return Ok(CheckInResponse.From(checkIn));
    }
}
