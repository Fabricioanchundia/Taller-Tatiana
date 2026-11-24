using Comisariato.Api.DTOs;
using Microsoft.Data.SqlClient;

namespace Comisariato.Api.Repositories;

public interface IProductoRepository
{
    Task<IReadOnlyList<ProductoStockBajoResponse>> ProductosStockBajoAsync(SqlConnection connection,
        CancellationToken cancellationToken);

    Task<bool> ExisteProductoAsync(SqlConnection connection, int productoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MovimientoInventarioResponse>> MovimientosPorProductoAsync(SqlConnection connection,
        int productoId, CancellationToken cancellationToken);
}
