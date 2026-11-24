using System.ComponentModel.DataAnnotations;
using Comisariato.Api.DTOs;
using Comisariato.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Comisariato.Api.Controllers;

[ApiController]
[Route("api/productos")]
[Produces("application/json")]
public sealed class ProductosController : ControllerBase
{
    private readonly IProductoService _productoService;

    public ProductosController(IProductoService productoService)
    {
        _productoService = productoService;
    }

    /// <summary>3. Productos con stock bajo (vw_ProductosStockBajo).</summary>
    [HttpGet("stock-bajo")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductoStockBajoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductoStockBajoResponse>>> ProductosStockBajo(
        CancellationToken cancellationToken)
    {
        return Ok(await _productoService.ProductosStockBajoAsync(cancellationToken));
    }

    /// <summary>5. Movimientos de inventario por producto (tabla MovimientosInventario).</summary>
    [HttpGet("{productoId:int}/movimientos")]
    [ProducesResponseType(typeof(IReadOnlyList<MovimientoInventarioResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MovimientoInventarioResponse>>> MovimientosPorProducto(
        [FromRoute, Range(1, int.MaxValue)] int productoId, CancellationToken cancellationToken)
    {
        return Ok(await _productoService.MovimientosPorProductoAsync(productoId, cancellationToken));
    }
}
