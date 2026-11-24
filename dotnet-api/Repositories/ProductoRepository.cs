using System.Data;
using Comisariato.Api.DTOs;
using Microsoft.Data.SqlClient;

namespace Comisariato.Api.Repositories;

public sealed class ProductoRepository : IProductoRepository
{
    private const string SqlStockBajo = """
        SELECT ProductoID, CodigoBarras, Nombre, Categoria, Proveedor,
               Stock, StockMinimo, UnidadesFaltantes
        FROM dbo.vw_ProductosStockBajo
        ORDER BY UnidadesFaltantes DESC, Nombre;
        """;

    private const string SqlExisteProducto =
        "SELECT COUNT(1) FROM dbo.Productos WHERE ProductoID = @ProductoID;";

    private const string SqlMovimientos = """
        SELECT MovimientoID, ProductoID, TipoMovimiento, Cantidad,
               StockAnterior, StockNuevo, ReferenciaTipo, ReferenciaID,
               FechaMovimiento, EmpleadoID
        FROM dbo.MovimientosInventario
        WHERE ProductoID = @ProductoID
        ORDER BY FechaMovimiento DESC, MovimientoID DESC;
        """;

    public async Task<IReadOnlyList<ProductoStockBajoResponse>> ProductosStockBajoAsync(
        SqlConnection connection, CancellationToken cancellationToken)
    {
        var productos = new List<ProductoStockBajoResponse>();

        await using var command = new SqlCommand(SqlStockBajo, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var proveedorOrdinal = reader.GetOrdinal("Proveedor");
            productos.Add(new ProductoStockBajoResponse(
                reader.GetInt32(reader.GetOrdinal("ProductoID")),
                reader.GetString(reader.GetOrdinal("CodigoBarras")),
                reader.GetString(reader.GetOrdinal("Nombre")),
                reader.GetString(reader.GetOrdinal("Categoria")),
                reader.IsDBNull(proveedorOrdinal) ? null : reader.GetString(proveedorOrdinal),
                reader.GetInt32(reader.GetOrdinal("Stock")),
                reader.GetInt32(reader.GetOrdinal("StockMinimo")),
                reader.GetInt32(reader.GetOrdinal("UnidadesFaltantes"))));
        }

        return productos;
    }

    public async Task<bool> ExisteProductoAsync(SqlConnection connection, int productoId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SqlExisteProducto, connection);
        command.Parameters.Add("@ProductoID", SqlDbType.Int).Value = productoId;
        var total = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return total > 0;
    }

    public async Task<IReadOnlyList<MovimientoInventarioResponse>> MovimientosPorProductoAsync(
        SqlConnection connection, int productoId, CancellationToken cancellationToken)
    {
        var movimientos = new List<MovimientoInventarioResponse>();

        await using var command = new SqlCommand(SqlMovimientos, connection);
        command.Parameters.Add("@ProductoID", SqlDbType.Int).Value = productoId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var refTipo = reader.GetOrdinal("ReferenciaTipo");
            var refId = reader.GetOrdinal("ReferenciaID");
            var empleado = reader.GetOrdinal("EmpleadoID");

            movimientos.Add(new MovimientoInventarioResponse(
                reader.GetInt64(reader.GetOrdinal("MovimientoID")),
                reader.GetInt32(reader.GetOrdinal("ProductoID")),
                reader.GetString(reader.GetOrdinal("TipoMovimiento")),
                reader.GetInt32(reader.GetOrdinal("Cantidad")),
                reader.GetInt32(reader.GetOrdinal("StockAnterior")),
                reader.GetInt32(reader.GetOrdinal("StockNuevo")),
                reader.IsDBNull(refTipo) ? null : reader.GetString(refTipo),
                reader.IsDBNull(refId) ? null : reader.GetInt32(refId),
                reader.GetDateTime(reader.GetOrdinal("FechaMovimiento")),
                reader.IsDBNull(empleado) ? null : reader.GetInt32(empleado)));
        }

        return movimientos;
    }
}
