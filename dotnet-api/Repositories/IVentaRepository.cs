using Comisariato.Api.DTOs;
using Microsoft.Data.SqlClient;

namespace Comisariato.Api.Repositories;

/// <summary>
/// Acceso a datos de ventas. La conexión y la transacción las crea el servicio
/// y se reciben como parámetro, así el repositorio participa de la misma unidad de trabajo.
/// </summary>
public interface IVentaRepository
{
    Task<int> RegistrarVentaAsync(SqlConnection connection, SqlTransaction transaction,
        VentaRequest request, CancellationToken cancellationToken);

    Task AnularVentaAsync(SqlConnection connection, SqlTransaction transaction,
        int ventaId, int? empleadoId, CancellationToken cancellationToken);

    Task<VentaResponse?> ObtenerPorIdAsync(SqlConnection connection, SqlTransaction? transaction,
        int ventaId, CancellationToken cancellationToken);

    Task<bool> ExisteEmpleadoAsync(SqlConnection connection, int empleadoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VentaEmpleadoResponse>> VentasPorEmpleadoAsync(SqlConnection connection,
        int empleadoId, CancellationToken cancellationToken);
}
