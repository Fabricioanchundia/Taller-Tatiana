namespace Comisariato.Api.DTOs;

/// <summary>Respuesta de registrar / anular / consultar una venta.</summary>
public sealed record VentaResponse(
    int VentaId,
    string NumeroFactura,
    int? ClienteId,
    int EmpleadoId,
    DateTime FechaVenta,
    decimal Subtotal,
    decimal Iva,
    decimal Total,
    string FormaPago,
    string Estado,
    IReadOnlyList<DetalleVentaResponse> Detalles);

public sealed record DetalleVentaResponse(
    int ProductoId,
    string Producto,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal SubtotalLinea);
