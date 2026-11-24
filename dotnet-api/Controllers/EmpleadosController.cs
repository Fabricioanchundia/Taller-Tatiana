using System.ComponentModel.DataAnnotations;
using Comisariato.Api.DTOs;
using Comisariato.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Comisariato.Api.Controllers;

[ApiController]
[Route("api/empleados")]
[Produces("application/json")]
public sealed class EmpleadosController : ControllerBase
{
    private readonly IVentaService _ventaService;

    public EmpleadosController(IVentaService ventaService)
    {
        _ventaService = ventaService;
    }

    /// <summary>4. Ventas por empleado (fn_VentasPorEmpleado). 404 si el empleado no existe.</summary>
    [HttpGet("{empleadoId:int}/ventas")]
    [ProducesResponseType(typeof(IReadOnlyList<VentaEmpleadoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<VentaEmpleadoResponse>>> VentasPorEmpleado(
        [FromRoute, Range(1, int.MaxValue)] int empleadoId, CancellationToken cancellationToken)
    {
        return Ok(await _ventaService.VentasPorEmpleadoAsync(empleadoId, cancellationToken));
    }
}
