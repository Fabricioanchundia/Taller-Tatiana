using System.Data;
using System.Text.Json;
using Comisariato.Api.DTOs;
using Comisariato.Api.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Comisariato.Api.Repositories;

public sealed class VentaRepository : IVentaRepository
{
    private const string SqlVentaPorId = """
        SELECT v.VentaID, v.NumeroFactura, v.ClienteID, v.EmpleadoID, v.FechaVenta,
               v.Subtotal, v.IVA, v.Total, v.FormaPago, v.Estado
        FROM dbo.Ventas AS v
        WHERE v.VentaID = @VentaID;
        """;

    private const string SqlDetallePorVenta = """
        SELECT d.ProductoID, p.Nombre AS Producto, d.Cantidad,
               d.PrecioUnitario, d.Descuento, d.SubtotalLinea
        FROM dbo.DetalleVenta AS d
        INNER JOIN dbo.Productos AS p ON p.ProductoID = d.ProductoID
        WHERE d.VentaID = @VentaID
        ORDER BY d.DetalleVentaID;
        """;

    private const string SqlExisteEmpleado =
        "SELECT COUNT(1) FROM dbo.Empleados WHERE EmpleadoID = @EmpleadoID;";

    private const string SqlVentasPorEmpleado = """
        SELECT VentaID, NumeroFactura, FechaVenta, ClienteID, Cliente,
               Subtotal, IVA, Total, FormaPago, Estado, NumeroItems
        FROM dbo.fn_VentasPorEmpleado(@EmpleadoID)
        ORDER BY FechaVenta DESC, VentaID DESC;
        """;

    public async Task<int> RegistrarVentaAsync(SqlConnection connection, SqlTransaction transaction,
        VentaRequest request, CancellationToken cancellationToken)
    {
        // Formato que espera el SP: [{"productoId":1,"cantidad":2,"descuento":0}]
        var detallesJson = JsonSerializer.Serialize(request.Detalles!.Select(d => new
        {
            productoId = d.ProductoId,
            cantidad = d.Cantidad,
            descuento = d.Descuento ?? 0m
        }));

        await using var command = new SqlCommand("dbo.usp_RegistrarVenta", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@NumeroFactura", SqlDbType.VarChar, 20).Value = request.NumeroFactura!.Trim();
        command.Parameters.Add("@ClienteID", SqlDbType.Int).Value = (object?)request.ClienteId ?? DBNull.Value;
        command.Parameters.Add("@EmpleadoID", SqlDbType.Int).Value = request.EmpleadoId!.Value;
        command.Parameters.Add("@FormaPago", SqlDbType.NVarChar, 30).Value = request.FormaPago!;
        command.Parameters.Add("@Detalles", SqlDbType.NVarChar, -1).Value = detallesJson;
        var ventaIdParam = command.Parameters.Add("@VentaID", SqlDbType.Int);
        ventaIdParam.Direction = ParameterDirection.Output;

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (SqlErrorMapper.TryMap(ex, out var mapped))
        {
            throw mapped;
        }

        return ventaIdParam.Value is int ventaId && ventaId > 0
            ? ventaId
            : throw new InvalidOperationException("usp_RegistrarVenta no devolvió un VentaID válido.");
    }

    public async Task AnularVentaAsync(SqlConnection connection, SqlTransaction transaction,
        int ventaId, int? empleadoId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("dbo.usp_AnularVenta", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@VentaID", SqlDbType.Int).Value = ventaId;
        command.Parameters.Add("@EmpleadoID", SqlDbType.Int).Value = (object?)empleadoId ?? DBNull.Value;

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (SqlErrorMapper.TryMap(ex, out var mapped))
        {
            throw mapped;
        }
    }

    public async Task<VentaResponse?> ObtenerPorIdAsync(SqlConnection connection, SqlTransaction? transaction,
        int ventaId, CancellationToken cancellationToken)
    {
        var detalles = new List<DetalleVentaResponse>();

        await using (var command = new SqlCommand(SqlDetallePorVenta, connection, transaction))
        {
            command.Parameters.Add("@VentaID", SqlDbType.Int).Value = ventaId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                detalles.Add(new DetalleVentaResponse(
                    reader.GetInt32(reader.GetOrdinal("ProductoID")),
                    reader.GetString(reader.GetOrdinal("Producto")),
                    reader.GetInt32(reader.GetOrdinal("Cantidad")),
                    reader.GetDecimal(reader.GetOrdinal("PrecioUnitario")),
                    reader.GetDecimal(reader.GetOrdinal("Descuento")),
                    reader.GetDecimal(reader.GetOrdinal("SubtotalLinea"))));
            }
        }

        await using (var command = new SqlCommand(SqlVentaPorId, connection, transaction))
        {
            command.Parameters.Add("@VentaID", SqlDbType.Int).Value = ventaId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new VentaResponse(
                reader.GetInt32(reader.GetOrdinal("VentaID")),
                reader.GetString(reader.GetOrdinal("NumeroFactura")),
                GetNullableInt(reader, "ClienteID"),
                reader.GetInt32(reader.GetOrdinal("EmpleadoID")),
                reader.GetDateTime(reader.GetOrdinal("FechaVenta")),
                reader.GetDecimal(reader.GetOrdinal("Subtotal")),
                reader.GetDecimal(reader.GetOrdinal("IVA")),
                reader.GetDecimal(reader.GetOrdinal("Total")),
                reader.GetString(reader.GetOrdinal("FormaPago")),
                reader.GetString(reader.GetOrdinal("Estado")),
                detalles);
        }
    }

    public async Task<bool> ExisteEmpleadoAsync(SqlConnection connection, int empleadoId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SqlExisteEmpleado, connection);
        command.Parameters.Add("@EmpleadoID", SqlDbType.Int).Value = empleadoId;
        var total = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return total > 0;
    }

    public async Task<IReadOnlyList<VentaEmpleadoResponse>> VentasPorEmpleadoAsync(SqlConnection connection,
        int empleadoId, CancellationToken cancellationToken)
    {
        var ventas = new List<VentaEmpleadoResponse>();

        await using var command = new SqlCommand(SqlVentasPorEmpleado, connection);
        command.Parameters.Add("@EmpleadoID", SqlDbType.Int).Value = empleadoId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ventas.Add(new VentaEmpleadoResponse(
                reader.GetInt32(reader.GetOrdinal("VentaID")),
                reader.GetString(reader.GetOrdinal("NumeroFactura")),
                reader.GetDateTime(reader.GetOrdinal("FechaVenta")),
                GetNullableInt(reader, "ClienteID"),
                reader.GetString(reader.GetOrdinal("Cliente")),
                reader.GetDecimal(reader.GetOrdinal("Subtotal")),
                reader.GetDecimal(reader.GetOrdinal("IVA")),
                reader.GetDecimal(reader.GetOrdinal("Total")),
                reader.GetString(reader.GetOrdinal("FormaPago")),
                reader.GetString(reader.GetOrdinal("Estado")),
                reader.GetInt32(reader.GetOrdinal("NumeroItems"))));
        }

        return ventas;
    }

    private static int? GetNullableInt(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }
}
