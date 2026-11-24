package edu.comisariato.api.dto;

import java.time.LocalDateTime;

/** Fila de dbo.MovimientosInventario. */
public record MovimientoInventarioResponse(
        long movimientoId,
        int productoId,
        String tipoMovimiento,
        int cantidad,
        int stockAnterior,
        int stockNuevo,
        String referenciaTipo,
        Integer referenciaId,
        LocalDateTime fechaMovimiento,
        Integer empleadoId
) {
}
