using Comisariato.Api.DTOs;

namespace Comisariato.Api.Services;

public interface IProductoService
{
    Task<IReadOnlyList<ProductoStockBajoResponse>> ProductosStockBajoAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MovimientoInventarioResponse>> MovimientosPorProductoAsync(int productoId,
        CancellationToken cancellationToken);
}
