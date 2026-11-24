package edu.comisariato.api.repository;

import java.time.LocalDateTime;
import java.util.List;

import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

import edu.comisariato.api.dto.MovimientoInventarioResponse;
import edu.comisariato.api.dto.ProductoStockBajoResponse;

@Repository
public class ProductoRepository {

    private static final String SQL_STOCK_BAJO = """
            SELECT ProductoID, CodigoBarras, Nombre, Categoria, Proveedor,
                   Stock, StockMinimo, UnidadesFaltantes
            FROM dbo.vw_ProductosStockBajo
            ORDER BY UnidadesFaltantes DESC, Nombre
            """;

    private static final String SQL_EXISTE_PRODUCTO =
            "SELECT COUNT(1) FROM dbo.Productos WHERE ProductoID = ?";

    private static final String SQL_MOVIMIENTOS = """
            SELECT MovimientoID, ProductoID, TipoMovimiento, Cantidad,
                   StockAnterior, StockNuevo, ReferenciaTipo, ReferenciaID,
                   FechaMovimiento, EmpleadoID
            FROM dbo.MovimientosInventario
            WHERE ProductoID = ?
            ORDER BY FechaMovimiento DESC, MovimientoID DESC
            """;

    private final JdbcTemplate jdbcTemplate;

    public ProductoRepository(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    public List<ProductoStockBajoResponse> productosStockBajo() {
        return jdbcTemplate.query(SQL_STOCK_BAJO, (rs, rowNum) -> new ProductoStockBajoResponse(
                rs.getInt("ProductoID"),
                rs.getString("CodigoBarras"),
                rs.getString("Nombre"),
                rs.getString("Categoria"),
                rs.getString("Proveedor"),
                rs.getInt("Stock"),
                rs.getInt("StockMinimo"),
                rs.getInt("UnidadesFaltantes")));
    }

    public boolean existeProducto(int productoId) {
        Integer total = jdbcTemplate.queryForObject(SQL_EXISTE_PRODUCTO, Integer.class, productoId);
        return total != null && total > 0;
    }

    public List<MovimientoInventarioResponse> movimientosPorProducto(int productoId) {
        return jdbcTemplate.query(SQL_MOVIMIENTOS, (rs, rowNum) -> new MovimientoInventarioResponse(
                rs.getLong("MovimientoID"),
                rs.getInt("ProductoID"),
                rs.getString("TipoMovimiento"),
                rs.getInt("Cantidad"),
                rs.getInt("StockAnterior"),
                rs.getInt("StockNuevo"),
                rs.getString("ReferenciaTipo"),
                rs.getObject("ReferenciaID", Integer.class),
                rs.getObject("FechaMovimiento", LocalDateTime.class),
                rs.getObject("EmpleadoID", Integer.class)), productoId);
    }
}
