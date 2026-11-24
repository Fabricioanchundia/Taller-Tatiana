package edu.comisariato.api.dto;

/** Fila devuelta por dbo.vw_ProductosStockBajo. */
public record ProductoStockBajoResponse(
        int productoId,
        String codigoBarras,
        String nombre,
        String categoria,
        String proveedor,
        int stock,
        int stockMinimo,
        int unidadesFaltantes
) {
}
