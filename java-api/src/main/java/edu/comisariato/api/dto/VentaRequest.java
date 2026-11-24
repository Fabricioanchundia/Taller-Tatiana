package edu.comisariato.api.dto;

import java.util.List;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;

/**
 * Cuerpo de POST /api/ventas.
 * No incluye Subtotal, IVA ni Total: los calcula usp_RegistrarVenta.
 * Si el cliente envía "total", Jackson lo ignora.
 */
public record VentaRequest(

        @Schema(example = "001-001-000000101")
        @NotBlank(message = "El número de factura es obligatorio.")
        @Size(max = 20, message = "El número de factura admite máximo 20 caracteres.")
        String numeroFactura,

        @Schema(description = "Opcional. Null = consumidor final.", example = "1", nullable = true)
        @Positive(message = "clienteId debe ser mayor que cero.")
        Integer clienteId,

        @Schema(example = "1")
        @NotNull(message = "El empleado es obligatorio.")
        @Positive(message = "empleadoId debe ser mayor que cero.")
        Integer empleadoId,

        @Schema(example = "Efectivo", allowableValues = {"Efectivo", "Tarjeta", "Transferencia"})
        @NotBlank(message = "La forma de pago es obligatoria.")
        @Pattern(regexp = "Efectivo|Tarjeta|Transferencia",
                 message = "Forma de pago inválida. Valores permitidos: Efectivo, Tarjeta, Transferencia.")
        String formaPago,

        @NotEmpty(message = "La venta debe tener al menos un producto.")
        @Valid
        List<DetalleVentaRequest> detalles
) {
}
