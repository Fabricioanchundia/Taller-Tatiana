namespace Comisariato.Api.DTOs;

/// <summary>Fila devuelta por dbo.fn_VentasPorEmpleado.</summary>
public sealed record VentaEmpleadoResponse(
    int VentaId,
    string NumeroFactura,
    DateTime FechaVenta,
    int? ClienteId,
    string Cliente,
    decimal Subtotal,
    decimal Iva,
    decimal Total,
    string FormaPago,
    string Estado,
    int NumeroItems);
