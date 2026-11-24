using System.Data;
using Comisariato.Api.DTOs;
using Comisariato.Api.Exceptions;
using Comisariato.Api.Infrastructure;
using Comisariato.Api.Repositories;
using Microsoft.Data.SqlClient;
using Polly.Retry;

namespace Comisariato.Api.Services;

/// <summary>
/// Capa de servicio: aquí se abre la transacción (SqlTransaction).
/// Flujo de cada operación que modifica datos:
///   1. Polly envuelve toda la unidad de trabajo (reintento ante deadlock 1205).
///   2. Se abre la conexión y BEGIN TRANSACTION.
///   3. El repositorio ejecuta el SP con esa transacción.
///   4. Commit si todo salió bien; Rollback en el catch ante cualquier error.
///   5. using / await using cierra conexión y transacción siempre.
/// </summary>
public sealed class VentaService : IVentaService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IVentaRepository _ventaRepository;
    private readonly ILogger<VentaService> _logger;
    private readonly AsyncRetryPolicy _deadlockRetry;

    public VentaService(
        ISqlConnectionFactory connectionFactory,
        IVentaRepository ventaRepository,
        ILogger<VentaService> logger)
    {
        _connectionFactory = connectionFactory;
        _ventaRepository = ventaRepository;
        _logger = logger;
        _deadlockRetry = PollyPolicies.DeadlockRetry(logger);
    }

    public Task<VentaResponse> RegistrarVentaAsync(VentaRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Registrando venta factura={Factura} empleadoId={EmpleadoId} clienteId={ClienteId} lineas={Lineas}",
            request.NumeroFactura, request.EmpleadoId, request.ClienteId, request.Detalles?.Count ?? 0);

        return _deadlockRetry.ExecuteAsync(ct => EjecutarEnTransaccionAsync(
            operacion: "RegistrarVenta",
            contexto: $"factura={request.NumeroFactura} empleadoId={request.EmpleadoId}",
            trabajo: async (connection, transaction) =>
            {
                var ventaId = await _ventaRepository.RegistrarVentaAsync(connection, transaction, request, ct);
                var venta = await _ventaRepository.ObtenerPorIdAsync(connection, transaction, ventaId, ct)
                    ?? throw new InvalidOperationException($"La venta {ventaId} no se encontró después de registrarla.");
                return venta;
            },
            cancellationToken: ct), cancellationToken);
    }

    public Task<VentaResponse> AnularVentaAsync(int ventaId, int? empleadoId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Anulando venta ventaId={VentaId} empleadoId={EmpleadoId}", ventaId, empleadoId);

        return _deadlockRetry.ExecuteAsync(ct => EjecutarEnTransaccionAsync(
            operacion: "AnularVenta",
            contexto: $"ventaId={ventaId} empleadoId={empleadoId}",
            trabajo: async (connection, transaction) =>
            {
                await _ventaRepository.AnularVentaAsync(connection, transaction, ventaId, empleadoId, ct);
                var venta = await _ventaRepository.ObtenerPorIdAsync(connection, transaction, ventaId, ct)
                    ?? throw new NotFoundException($"La venta {ventaId} no existe.");
                return venta;
            },
            cancellationToken: ct), cancellationToken);
    }

    public async Task<VentaResponse> ObtenerVentaAsync(int ventaId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await _ventaRepository.ObtenerPorIdAsync(connection, null, ventaId, cancellationToken)
            ?? throw new NotFoundException($"La venta {ventaId} no existe.");
    }

    public async Task<IReadOnlyList<VentaEmpleadoResponse>> VentasPorEmpleadoAsync(int empleadoId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        if (!await _ventaRepository.ExisteEmpleadoAsync(connection, empleadoId, cancellationToken))
        {
            throw new NotFoundException($"El empleado {empleadoId} no existe.");
        }

        var ventas = await _ventaRepository.VentasPorEmpleadoAsync(connection, empleadoId, cancellationToken);
        _logger.LogInformation("Consulta de ventas empleadoId={EmpleadoId} resultados={Total}",
            empleadoId, ventas.Count);
        return ventas;
    }

    /// <summary>
    /// Unidad de trabajo transaccional: BEGIN → trabajo → COMMIT, o ROLLBACK si algo falla.
    /// </summary>
    private async Task<T> EjecutarEnTransaccionAsync<T>(
        string operacion,
        string contexto,
        Func<SqlConnection, SqlTransaction, Task<T>> trabajo,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var resultado = await trabajo(connection, transaction);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("{Operacion} confirmada (COMMIT). {Contexto}", operacion, contexto);
            return resultado;
        }
        catch (Exception ex)
        {
            await RollbackSeguroAsync(transaction, operacion);

            if (ex is DeadlockException)
            {
                _logger.LogWarning("{Operacion} revertida (ROLLBACK) por deadlock 1205. {Contexto}",
                    operacion, contexto);
            }
            else
            {
                _logger.LogError(ex, "{Operacion} revertida (ROLLBACK). {Contexto}. Error: {Mensaje}",
                    operacion, contexto, ex.Message);
            }

            throw;
        }
    }

    /// <summary>
    /// Con SET XACT_ABORT ON, SQL Server puede haber revertido ya la transacción;
    /// en ese caso Rollback lanza InvalidOperationException y no debe ocultar el error original.
    /// </summary>
    private async Task RollbackSeguroAsync(SqlTransaction transaction, string operacion)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch (Exception rollbackEx)
        {
            _logger.LogDebug(rollbackEx,
                "{Operacion}: la transacción ya había sido revertida por SQL Server.", operacion);
        }
    }
}
