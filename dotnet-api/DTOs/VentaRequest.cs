using System.ComponentModel.DataAnnotations;

namespace Comisariato.Api.DTOs;

/// <summary>
/// Cuerpo de POST /api/ventas. No incluye Subtotal, IVA ni Total:
/// los calcula usp_RegistrarVenta en el servidor.
/// Las propiedades son anulables para que [Required] detecte los campos faltantes (400).
/// </summary>
public sealed record VentaRequest
{
    /// <example>001-001-000000201</example>
    [Required(ErrorMessage = "El número de factura es obligatorio.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "El número de factura admite entre 1 y 20 caracteres.")]
    public string? NumeroFactura { get; init; }

    /// <summary>Opcional. Null = consumidor final.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "clienteId debe ser mayor que cero.")]
    public int? ClienteId { get; init; }

    [Required(ErrorMessage = "El empleado es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "empleadoId debe ser mayor que cero.")]
    public int? EmpleadoId { get; init; }

    [Required(ErrorMessage = "La forma de pago es obligatoria.")]
    [RegularExpression("^(Efectivo|Tarjeta|Transferencia)$",
        ErrorMessage = "Forma de pago inválida. Valores permitidos: Efectivo, Tarjeta, Transferencia.")]
    public string? FormaPago { get; init; }

    [Required(ErrorMessage = "La venta debe tener al menos un producto.")]
    [MinLength(1, ErrorMessage = "La venta debe tener al menos un producto.")]
    public List<DetalleVentaRequest>? Detalles { get; init; }
}

/// <summary>Línea de la venta. El precio lo toma el SP de Productos.PrecioVenta.</summary>
public sealed record DetalleVentaRequest
{
    [Required(ErrorMessage = "productoId es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "productoId debe ser mayor que cero.")]
    public int? ProductoId { get; init; }

    [Required(ErrorMessage = "cantidad es obligatoria.")]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public int? Cantidad { get; init; }

    [Range(typeof(decimal), "0", "99999999", ErrorMessage = "El descuento no puede ser negativo.")]
    public decimal? Descuento { get; init; }
}
