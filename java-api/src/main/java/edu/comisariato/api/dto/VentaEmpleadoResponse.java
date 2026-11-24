package edu.comisariato.api.dto;

import java.math.BigDecimal;
import java.time.LocalDateTime;

/** Fila devuelta por dbo.fn_VentasPorEmpleado. */
public record VentaEmpleadoResponse(
        int ventaId,
        String numeroFactura,
        LocalDateTime fechaVenta,
        Integer clienteId,
        String cliente,
        BigDecimal subtotal,
        BigDecimal iva,
        BigDecimal total,
        String formaPago,
        String estado,
        int numeroItems
) {
}
