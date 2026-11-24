package edu.comisariato.api.dto;

import java.math.BigDecimal;

public record DetalleVentaResponse(
        int productoId,
        String producto,
        int cantidad,
        BigDecimal precioUnitario,
        BigDecimal descuento,
        BigDecimal subtotalLinea
) {
}
