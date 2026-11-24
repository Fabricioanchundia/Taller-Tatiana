using System.ComponentModel.DataAnnotations;
using Comisariato.Api.DTOs;
using Comisariato.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Comisariato.Api.Controllers;

[ApiController]
[Route("api/ventas")]
[Produces("application/json")]
public sealed class VentasController : ControllerBase
{
    private readonly IVentaService _ventaService;

    public VentasController(IVentaService ventaService)
    {
        _ventaService = ventaService;
    }

    /// <summary>1. Registrar venta (usp_RegistrarVenta). El Total lo calcula el SP.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(VentaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VentaResponse>> RegistrarVenta(
        [FromBody] VentaRequest request, CancellationToken cancellationToken)
    {
        var venta = await _ventaService.RegistrarVentaAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObtenerVenta), new { ventaId = venta.VentaId }, venta);
    }

    /// <summary>2. Anular venta (usp_AnularVenta). empleadoId (opcional) indica quién anula.</summary>
    [HttpPost("{ventaId:int}/anular")]
    [ProducesResponseType(typeof(VentaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VentaResponse>> AnularVenta(
        [FromRoute, Range(1, int.MaxValue)] int ventaId,
        [FromQuery, Range(1, int.MaxValue)] int? empleadoId,
        CancellationToken cancellationToken)
    {
        var venta = await _ventaService.AnularVentaAsync(ventaId, empleadoId, cancellationToken);
        return Ok(venta);
    }

    /// <summary>Consultar una venta por id (destino del header Location).</summary>
    [HttpGet("{ventaId:int}")]
    [ProducesResponseType(typeof(VentaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VentaResponse>> ObtenerVenta(
        [FromRoute, Range(1, int.MaxValue)] int ventaId, CancellationToken cancellationToken)
    {
        return Ok(await _ventaService.ObtenerVentaAsync(ventaId, cancellationToken));
    }
}
