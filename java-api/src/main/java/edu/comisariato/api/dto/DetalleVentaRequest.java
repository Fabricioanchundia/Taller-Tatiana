package edu.comisariato.api.dto;

import java.math.BigDecimal;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.PositiveOrZero;

/** Línea de una venta. El precio unitario lo toma el SP de Productos.PrecioVenta. */
public record DetalleVentaRequest(

        @Schema(example = "1")
        @NotNull(message = "productoId es obligatorio.")
        @Positive(message = "productoId debe ser mayor que cero.")
        Integer productoId,

        @Schema(example = "2")
        @NotNull(message = "cantidad es obligatoria.")
        @Positive(message = "La cantidad debe ser mayor que cero.")
        Integer cantidad,

        @Schema(description = "Descuento en dólares para la línea (opcional).", example = "0.00")
        @PositiveOrZero(message = "El descuento no puede ser negativo.")
        BigDecimal descuento
) {
}
