namespace Comisariato.Api.DTOs;

/// <summary>Fila devuelta por dbo.vw_ProductosStockBajo.</summary>
public sealed record ProductoStockBajoResponse(
    int ProductoId,
    string CodigoBarras,
    string Nombre,
    string Categoria,
    string? Proveedor,
    int Stock,
    int StockMinimo,
    int UnidadesFaltantes);
