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
    /// Recibe un DNI y decide el ingreso. Un rechazo es una respuesta válida (200 con allowed=false y el motivo en "reason"),
    /// incluido el caso de un DNI que no corresponde a ningún socio (reason = MemberNotFound). Todo intento queda registrado.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CheckInResponse>> Create(CheckInRequest request, CancellationToken ct)
    {
        var dni = DniNormalizer.Normalize(request.Dni);

        var checkIn = await _service.RegisterAsync(dni, ct);

        return Ok(CheckInResponse.From(checkIn));
    }
}
