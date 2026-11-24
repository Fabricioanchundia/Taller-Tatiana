package edu.comisariato.api.repository;

import java.sql.CallableStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Types;
import java.time.LocalDateTime;
import java.util.List;
import java.util.Optional;

import org.springframework.dao.DataAccessException;
import org.springframework.jdbc.core.CallableStatementCallback;
import org.springframework.jdbc.core.CallableStatementCreator;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;

import edu.comisariato.api.dto.DetalleVentaResponse;
import edu.comisariato.api.dto.VentaEmpleadoResponse;
import edu.comisariato.api.dto.VentaRequest;
import edu.comisariato.api.dto.VentaResponse;

/**
 * Acceso a datos de ventas con SQL explícito (JdbcTemplate).
 * JdbcTemplate abre/cierra statements y result sets automáticamente
 * y usa la conexión de la transacción abierta por el servicio.
 */
@Repository
public class VentaRepository {

    private static final String CALL_REGISTRAR_VENTA =
            "{call dbo.usp_RegistrarVenta(?, ?, ?, ?, ?, ?)}";

    private static final String CALL_ANULAR_VENTA =
            "{call dbo.usp_AnularVenta(?, ?)}";

    private static final String SQL_VENTA_POR_ID = """
            SELECT v.VentaID, v.NumeroFactura, v.ClienteID, v.EmpleadoID, v.FechaVenta,
                   v.Subtotal, v.IVA, v.Total, v.FormaPago, v.Estado
            FROM dbo.Ventas AS v
            WHERE v.VentaID = ?
            """;

    private static final String SQL_DETALLE_POR_VENTA = """
            SELECT d.ProductoID, p.Nombre AS Producto, d.Cantidad,
                   d.PrecioUnitario, d.Descuento, d.SubtotalLinea
            FROM dbo.DetalleVenta AS d
            INNER JOIN dbo.Productos AS p ON p.ProductoID = d.ProductoID
            WHERE d.VentaID = ?
            ORDER BY d.DetalleVentaID
            """;

    private static final String SQL_EXISTE_EMPLEADO =
            "SELECT COUNT(1) FROM dbo.Empleados WHERE EmpleadoID = ?";

    private static final String SQL_VENTAS_POR_EMPLEADO = """
            SELECT VentaID, NumeroFactura, FechaVenta, ClienteID, Cliente,
                   Subtotal, IVA, Total, FormaPago, Estado, NumeroItems
            FROM dbo.fn_VentasPorEmpleado(?)
            ORDER BY FechaVenta DESC, VentaID DESC
            """;

    private final JdbcTemplate jdbcTemplate;
    private final ObjectMapper objectMapper;

    public VentaRepository(JdbcTemplate jdbcTemplate, ObjectMapper objectMapper) {
        this.jdbcTemplate = jdbcTemplate;
        this.objectMapper = objectMapper;
    }

    /** Ejecuta usp_RegistrarVenta y devuelve el VentaID generado. */
    public int registrarVenta(VentaRequest request) {
        String detallesJson = toJson(request);

        CallableStatementCreator creator = connection -> {
            CallableStatement cs = connection.prepareCall(CALL_REGISTRAR_VENTA);
            cs.setString(1, request.numeroFactura().trim());
            if (request.clienteId() == null) {
                cs.setNull(2, Types.INTEGER);
            } else {
                cs.setInt(2, request.clienteId());
            }
            cs.setInt(3, request.empleadoId());
            cs.setNString(4, request.formaPago());
            cs.setNString(5, detallesJson);
            cs.registerOutParameter(6, Types.INTEGER);
            return cs;
        };

        CallableStatementCallback<Integer> callback = cs -> {
            cs.execute();
            return cs.getInt(6);
        };

        try {
            Integer ventaId = jdbcTemplate.execute(creator, callback);
            if (ventaId == null || ventaId <= 0) {
                throw new IllegalStateException("usp_RegistrarVenta no devolvió un VentaID válido.");
            }
            return ventaId;
        } catch (DataAccessException ex) {
            throw SqlServerErrorTranslator.translate(ex);
        }
    }

    /** Ejecuta usp_AnularVenta. */
    public void anularVenta(int ventaId, Integer empleadoId) {
        CallableStatementCreator creator = connection -> {
            CallableStatement cs = connection.prepareCall(CALL_ANULAR_VENTA);
            cs.setInt(1, ventaId);
            if (empleadoId == null) {
                cs.setNull(2, Types.INTEGER);
            } else {
                cs.setInt(2, empleadoId);
            }
            return cs;
        };

        CallableStatementCallback<Boolean> callback = cs -> {
            cs.execute();
            return Boolean.TRUE;
        };

        try {
            jdbcTemplate.execute(creator, callback);
        } catch (DataAccessException ex) {
            throw SqlServerErrorTranslator.translate(ex);
        }
    }

    public Optional<VentaResponse> buscarPorId(int ventaId) {
        try {
            List<DetalleVentaResponse> detalles =
                    jdbcTemplate.query(SQL_DETALLE_POR_VENTA, VentaRepository::mapDetalle, ventaId);

            List<VentaResponse> ventas = jdbcTemplate.query(SQL_VENTA_POR_ID,
                    (rs, rowNum) -> mapVenta(rs, detalles), ventaId);

            return ventas.stream().findFirst();
        } catch (DataAccessException ex) {
            throw SqlServerErrorTranslator.translate(ex);
        }
    }

    public boolean existeEmpleado(int empleadoId) {
        Integer total = jdbcTemplate.queryForObject(SQL_EXISTE_EMPLEADO, Integer.class, empleadoId);
        return total != null && total > 0;
    }

    public List<VentaEmpleadoResponse> ventasPorEmpleado(int empleadoId) {
        return jdbcTemplate.query(SQL_VENTAS_POR_EMPLEADO, (rs, rowNum) -> new VentaEmpleadoResponse(
                rs.getInt("VentaID"),
                rs.getString("NumeroFactura"),
                rs.getObject("FechaVenta", LocalDateTime.class),
                rs.getObject("ClienteID", Integer.class),
                rs.getString("Cliente"),
                rs.getBigDecimal("Subtotal"),
                rs.getBigDecimal("IVA"),
                rs.getBigDecimal("Total"),
                rs.getString("FormaPago"),
                rs.getString("Estado"),
                rs.getInt("NumeroItems")), empleadoId);
    }

    // ------------------------------------------------------------------
    // Mapeos
    // ------------------------------------------------------------------

    private static VentaResponse mapVenta(ResultSet rs, List<DetalleVentaResponse> detalles)
            throws SQLException {
        return new VentaResponse(
                rs.getInt("VentaID"),
                rs.getString("NumeroFactura"),
                rs.getObject("ClienteID", Integer.class),
                rs.getInt("EmpleadoID"),
                rs.getObject("FechaVenta", LocalDateTime.class),
                rs.getBigDecimal("Subtotal"),
                rs.getBigDecimal("IVA"),
                rs.getBigDecimal("Total"),
                rs.getString("FormaPago"),
                rs.getString("Estado"),
                detalles);
    }

    private static DetalleVentaResponse mapDetalle(ResultSet rs, int rowNum) throws SQLException {
        return new DetalleVentaResponse(
                rs.getInt("ProductoID"),
                rs.getString("Producto"),
                rs.getInt("Cantidad"),
                rs.getBigDecimal("PrecioUnitario"),
                rs.getBigDecimal("Descuento"),
                rs.getBigDecimal("SubtotalLinea"));
    }

    /** Serializa las líneas al formato JSON que espera el SP. */
    private String toJson(VentaRequest request) {
        try {
            return objectMapper.writeValueAsString(request.detalles());
        } catch (JsonProcessingException ex) {
            throw new IllegalStateException("No se pudo serializar el detalle de la venta.", ex);
        }
    }
}
