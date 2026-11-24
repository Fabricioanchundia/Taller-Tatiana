namespace Comisariato.Api.DTOs;

/// <summary>Fila de dbo.MovimientosInventario.</summary>
public sealed record MovimientoInventarioResponse(
    long MovimientoId,
    int ProductoId,
    string TipoMovimiento,
    int Cantidad,
    int StockAnterior,
    int StockNuevo,
    string? ReferenciaTipo,
    int? ReferenciaId,
    DateTime FechaMovimiento,
    int? EmpleadoId);
