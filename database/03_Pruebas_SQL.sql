-- ============================================================
-- Script 03: pruebas manuales de los objetos del taller (SSMS / Azure Data Studio)
-- Ejecutar bloque por bloque (seleccionar y F5) DESPUÉS de los scripts 01 y 02.
-- Cada bloque indica el resultado esperado.
-- ============================================================
USE Comisariato;
GO

-- ------------------------------------------------------------
-- A. Venta correcta  -> devuelve el VentaID generado
-- ------------------------------------------------------------
DECLARE @Id INT;
EXEC dbo.usp_RegistrarVenta
     @NumeroFactura = '001-001-000000001',
     @ClienteID     = 1,
     @EmpleadoID    = 1,
     @FormaPago     = N'Efectivo',
     @Detalles      = N'[{"productoId":1,"cantidad":2},{"productoId":3,"cantidad":1,"descuento":0.20}]',
     @VentaID       = @Id OUTPUT;
SELECT @Id AS VentaIDCreada;
SELECT * FROM dbo.Ventas WHERE VentaID = @Id;
SELECT * FROM dbo.DetalleVenta WHERE VentaID = @Id;
GO

-- ------------------------------------------------------------
-- B. Factura duplicada -> Msg 50409 (la API responde 409)
-- ------------------------------------------------------------
DECLARE @Id INT;
EXEC dbo.usp_RegistrarVenta '001-001-000000001', NULL, 1, N'Efectivo',
     N'[{"productoId":1,"cantidad":1}]', @Id OUTPUT;
GO

-- ------------------------------------------------------------
-- C. Stock insuficiente -> Msg 50422 (la API responde 422)
-- ------------------------------------------------------------
DECLARE @Id INT;
EXEC dbo.usp_RegistrarVenta '001-001-000000002', NULL, 1, N'Tarjeta',
     N'[{"productoId":4,"cantidad":9999}]', @Id OUTPUT;
GO

-- ------------------------------------------------------------
-- D. Venta que deja el detergente en stock bajo (30 -> 5, mínimo 10)
-- ------------------------------------------------------------
DECLARE @Id INT;
EXEC dbo.usp_RegistrarVenta '001-001-000000003', 2, 1, N'Transferencia',
     N'[{"productoId":4,"cantidad":25}]', @Id OUTPUT;
SELECT * FROM dbo.vw_ProductosStockBajo;
GO

-- ------------------------------------------------------------
-- E. Anular venta -> el stock regresa y se registran entradas
-- ------------------------------------------------------------
EXEC dbo.usp_AnularVenta @VentaID = 1, @EmpleadoID = 3;
SELECT VentaID, Estado FROM dbo.Ventas WHERE VentaID = 1;
GO

-- F. Anular otra vez -> Msg 50409 (409)
EXEC dbo.usp_AnularVenta @VentaID = 1;
GO

-- G. Anular venta inexistente -> Msg 50404 (404)
EXEC dbo.usp_AnularVenta @VentaID = 99999;
GO

-- ------------------------------------------------------------
-- H. Consultas
-- ------------------------------------------------------------
SELECT * FROM dbo.fn_VentasPorEmpleado(1) ORDER BY FechaVenta DESC;
SELECT * FROM dbo.fn_VentasPorEmpleado(2);   -- existe, sin ventas -> 0 filas

SELECT *
FROM dbo.MovimientosInventario
WHERE ProductoID = 1
ORDER BY FechaMovimiento DESC, MovimientoID DESC;
GO

-- ------------------------------------------------------------
-- I. Transacción externa (simula lo que hacen las APIs):
--    el SP no hace COMMIT; quien llama decide.
-- ------------------------------------------------------------
BEGIN TRANSACTION;
    DECLARE @Id INT;
    EXEC dbo.usp_RegistrarVenta '001-001-000000099', NULL, 1, N'Efectivo',
         N'[{"productoId":2,"cantidad":1}]', @Id OUTPUT;
    SELECT @@TRANCOUNT AS TransaccionesAbiertas, @Id AS VentaID;
ROLLBACK TRANSACTION;   -- la venta 001-001-000000099 NO debe quedar guardada
SELECT COUNT(*) AS DebeSerCero FROM dbo.Ventas WHERE NumeroFactura = '001-001-000000099';
GO
