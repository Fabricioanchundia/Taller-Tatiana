-- ============================================================
-- BASE DE DATOS: Comisariato
-- Script 02: objetos programables requeridos por el taller
--   1. dbo.usp_RegistrarVenta     (POST /api/ventas)
--   2. dbo.usp_AnularVenta        (POST /api/ventas/{id}/anular)
--   3. dbo.vw_ProductosStockBajo  (GET  /api/productos/stock-bajo)
--   4. dbo.fn_VentasPorEmpleado   (GET  /api/empleados/{id}/ventas)
--   5. dbo.MovimientosInventario  (tabla ya creada en el script 01)
--
-- Requisitos: SQL Server 2016 SP1 o superior (CREATE OR ALTER, OPENJSON)
--             y nivel de compatibilidad >= 130.
-- Se puede ejecutar varias veces: usa CREATE OR ALTER.
--
-- CONVENCIÓN DE ERRORES (THROW):
--   El número de error = 50000 + código HTTP que debe devolver la API.
--     50400 -> 400 Bad Request          (dato de entrada inválido)
--     50404 -> 404 Not Found            (recurso no existe)
--     50409 -> 409 Conflict             (factura duplicada, venta ya anulada)
--     50422 -> 422 Unprocessable Entity (regla de negocio: stock insuficiente...)
--   Las APIs Java y .NET traducen estos números a su excepción de dominio.
--
-- MANEJO DE TRANSACCIONES:
--   Las APIs abren la transacción en la capa de servicio y luego llaman
--   al SP. Por eso cada SP revisa @@TRANCOUNT:
--     - Si ya existe una transacción (la abrió la API), la usa y deja que
--       la API haga COMMIT / ROLLBACK.
--     - Si no existe (ejecución directa desde SSMS), abre la suya propia.
-- ============================================================

USE Comisariato;
GO

-- ============================================================
-- 1. usp_RegistrarVenta
-- ------------------------------------------------------------
-- @Detalles: arreglo JSON con las líneas de la venta, por ejemplo:
--   [{"productoId":1,"cantidad":2,"descuento":0.00},
--    {"productoId":3,"cantidad":1}]
-- El cliente NO envía precios ni totales: el precio se toma de
-- Productos.PrecioVenta y el Subtotal/IVA/Total se calculan aquí.
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_RegistrarVenta
    @NumeroFactura VARCHAR(20),
    @ClienteID     INT = NULL,          -- NULL = consumidor final
    @EmpleadoID    INT,
    @FormaPago     NVARCHAR(30),
    @Detalles      NVARCHAR(MAX),
    @VentaID       INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @TasaIVA    DECIMAL(5,4) = 0.15;  -- IVA vigente en Ecuador (15 %)
    DECLARE @TranPropia BIT = 0;
    DECLARE @Msg        NVARCHAR(2048);

    BEGIN TRY
        ----------------------------------------------------------------
        -- 1. Validaciones de entrada (400)
        ----------------------------------------------------------------
        IF @NumeroFactura IS NULL OR LTRIM(RTRIM(@NumeroFactura)) = ''
            THROW 50400, N'El número de factura es obligatorio.', 1;

        IF @EmpleadoID IS NULL
            THROW 50400, N'El empleado es obligatorio.', 1;

        IF @FormaPago IS NULL
           OR @FormaPago NOT IN (N'Efectivo', N'Tarjeta', N'Transferencia')
            THROW 50400, N'Forma de pago inválida. Valores permitidos: Efectivo, Tarjeta, Transferencia.', 1;

        IF @Detalles IS NULL OR ISJSON(@Detalles) = 0
            THROW 50400, N'El detalle de la venta debe ser un arreglo JSON válido.', 1;

        DECLARE @Json TABLE (
            ProductoID INT NULL,
            Cantidad   INT NULL,
            Descuento  DECIMAL(10,2) NULL
        );

        INSERT INTO @Json (ProductoID, Cantidad, Descuento)
        SELECT j.ProductoID, j.Cantidad, ISNULL(j.Descuento, 0)
        FROM OPENJSON(@Detalles)
             WITH (
                 ProductoID INT           '$.productoId',
                 Cantidad   INT           '$.cantidad',
                 Descuento  DECIMAL(10,2) '$.descuento'
             ) AS j;

        IF NOT EXISTS (SELECT 1 FROM @Json)
            THROW 50400, N'La venta debe tener al menos un producto.', 1;

        IF EXISTS (SELECT 1 FROM @Json WHERE ProductoID IS NULL OR Cantidad IS NULL)
            THROW 50400, N'Cada línea debe indicar productoId y cantidad.', 1;

        IF EXISTS (SELECT 1 FROM @Json WHERE Cantidad <= 0)
            THROW 50400, N'La cantidad de cada línea debe ser mayor que cero.', 1;

        IF EXISTS (SELECT 1 FROM @Json WHERE Descuento < 0)
            THROW 50400, N'El descuento no puede ser negativo.', 1;

        IF EXISTS (SELECT ProductoID FROM @Json GROUP BY ProductoID HAVING COUNT(*) > 1)
            THROW 50400, N'Un producto no puede repetirse en la misma venta; sume las cantidades en una sola línea.', 1;

        ----------------------------------------------------------------
        -- 2. Transacción (propia solo si nadie la abrió antes)
        ----------------------------------------------------------------
        IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @TranPropia = 1;
        END;

        ----------------------------------------------------------------
        -- 3. Factura duplicada (409)
        --    UPDLOCK + HOLDLOCK bloquea el rango de la clave única y evita
        --    que dos cajas registren la misma factura al mismo tiempo.
        ----------------------------------------------------------------
        IF EXISTS (SELECT 1
                   FROM dbo.Ventas WITH (UPDLOCK, HOLDLOCK)
                   WHERE NumeroFactura = @NumeroFactura)
        BEGIN
            SET @Msg = CONCAT(N'Ya existe una venta con el número de factura ', @NumeroFactura, N'.');
            THROW 50409, @Msg, 1;
        END;

        ----------------------------------------------------------------
        -- 4. Referencias (422)
        ----------------------------------------------------------------
        IF NOT EXISTS (SELECT 1 FROM dbo.Empleados
                       WHERE EmpleadoID = @EmpleadoID AND Activo = 1)
        BEGIN
            SET @Msg = CONCAT(N'El empleado ', @EmpleadoID, N' no existe o está inactivo.');
            THROW 50422, @Msg, 1;
        END;

        IF @ClienteID IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.Clientes WHERE ClienteID = @ClienteID)
        BEGIN
            SET @Msg = CONCAT(N'El cliente ', @ClienteID, N' no existe.');
            THROW 50422, @Msg, 1;
        END;

        ----------------------------------------------------------------
        -- 5. Bloquear los productos y leer precio/stock actuales.
        --    UPDLOCK evita que otra transacción venda el mismo stock
        --    entre la verificación y la actualización.
        ----------------------------------------------------------------
        DECLARE @Lineas TABLE (
            ProductoID     INT           NOT NULL PRIMARY KEY,
            Nombre         NVARCHAR(150) NOT NULL,
            Cantidad       INT           NOT NULL,
            Descuento      DECIMAL(10,2) NOT NULL,
            PrecioUnitario DECIMAL(10,2) NOT NULL,
            StockActual    INT           NOT NULL,
            Activo         BIT           NOT NULL
        );

        INSERT INTO @Lineas (ProductoID, Nombre, Cantidad, Descuento,
                             PrecioUnitario, StockActual, Activo)
        SELECT p.ProductoID, p.Nombre, j.Cantidad, j.Descuento,
               p.PrecioVenta, p.Stock, p.Activo
        FROM @Json AS j
        INNER JOIN dbo.Productos AS p WITH (UPDLOCK, ROWLOCK)
                ON p.ProductoID = j.ProductoID;

        SET @Msg = NULL;
        SELECT TOP (1) @Msg = CONCAT(N'El producto ', j.ProductoID, N' no existe.')
        FROM @Json AS j
        WHERE NOT EXISTS (SELECT 1 FROM @Lineas AS l WHERE l.ProductoID = j.ProductoID)
        ORDER BY j.ProductoID;
        IF @Msg IS NOT NULL
            THROW 50422, @Msg, 1;

        SELECT TOP (1) @Msg = CONCAT(N'El producto "', Nombre, N'" está inactivo y no se puede vender.')
        FROM @Lineas
        WHERE Activo = 0
        ORDER BY ProductoID;
        IF @Msg IS NOT NULL
            THROW 50422, @Msg, 1;

        SELECT TOP (1) @Msg = CONCAT(N'El descuento del producto "', Nombre,
                                     N'" supera el valor de la línea.')
        FROM @Lineas
        WHERE Descuento > Cantidad * PrecioUnitario
        ORDER BY ProductoID;
        IF @Msg IS NOT NULL
            THROW 50422, @Msg, 1;

        -- Stock insuficiente (422)
        SELECT TOP (1) @Msg = CONCAT(N'Stock insuficiente para "', Nombre,
                                     N'": disponible ', StockActual,
                                     N', solicitado ', Cantidad, N'.')
        FROM @Lineas
        WHERE Cantidad > StockActual
        ORDER BY ProductoID;
        IF @Msg IS NOT NULL
            THROW 50422, @Msg, 1;

        ----------------------------------------------------------------
        -- 6. Totales calculados en el servidor
        ----------------------------------------------------------------
        DECLARE @Subtotal DECIMAL(10,2) =
            (SELECT SUM(CONVERT(DECIMAL(12,2), Cantidad * PrecioUnitario - Descuento))
             FROM @Lineas);
        DECLARE @IVA   DECIMAL(10,2) = ROUND(@Subtotal * @TasaIVA, 2);
        DECLARE @Total DECIMAL(10,2) = @Subtotal + @IVA;

        ----------------------------------------------------------------
        -- 7. Cabecera + detalle
        ----------------------------------------------------------------
        INSERT INTO dbo.Ventas (NumeroFactura, ClienteID, EmpleadoID,
                                Subtotal, IVA, Total, FormaPago, Estado)
        VALUES (@NumeroFactura, @ClienteID, @EmpleadoID,
                @Subtotal, @IVA, @Total, @FormaPago, N'Completada');

        SET @VentaID = CAST(SCOPE_IDENTITY() AS INT);

        INSERT INTO dbo.DetalleVenta (VentaID, ProductoID, Cantidad,
                                      PrecioUnitario, Descuento)
        SELECT @VentaID, ProductoID, Cantidad, PrecioUnitario, Descuento
        FROM @Lineas;

        ----------------------------------------------------------------
        -- 8. Descontar stock y registrar la bitácora de inventario.
        --    OUTPUT no puede escribir directo en MovimientosInventario
        --    porque esa tabla tiene claves foráneas; se usa una variable.
        ----------------------------------------------------------------
        DECLARE @Cambios TABLE (
            ProductoID    INT NOT NULL,
            Cantidad      INT NOT NULL,
            StockAnterior INT NOT NULL,
            StockNuevo    INT NOT NULL
        );

        UPDATE p
           SET p.Stock = p.Stock - l.Cantidad
        OUTPUT inserted.ProductoID, l.Cantidad, deleted.Stock, inserted.Stock
          INTO @Cambios (ProductoID, Cantidad, StockAnterior, StockNuevo)
        FROM dbo.Productos AS p
        INNER JOIN @Lineas AS l ON l.ProductoID = p.ProductoID;

        INSERT INTO dbo.MovimientosInventario
            (ProductoID, TipoMovimiento, Cantidad, StockAnterior, StockNuevo,
             ReferenciaTipo, ReferenciaID, EmpleadoID)
        SELECT ProductoID, N'Salida', Cantidad, StockAnterior, StockNuevo,
               N'Venta', @VentaID, @EmpleadoID
        FROM @Cambios;

        IF @TranPropia = 1
            COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @TranPropia = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- ============================================================
-- 2. usp_AnularVenta
-- ------------------------------------------------------------
-- Cambia el estado a 'Anulada', devuelve el stock de cada línea y
-- registra un movimiento de 'Entrada' por cada producto.
-- @EmpleadoID (opcional) = quién anula; queda en la bitácora.
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_AnularVenta
    @VentaID    INT,
    @EmpleadoID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @TranPropia BIT = 0;
    DECLARE @Msg        NVARCHAR(2048);
    DECLARE @Estado     NVARCHAR(20);

    BEGIN TRY
        IF @VentaID IS NULL OR @VentaID <= 0
            THROW 50400, N'El identificador de la venta es inválido.', 1;

        IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @TranPropia = 1;
        END;

        -- Bloquea la venta para que dos anulaciones simultáneas no
        -- devuelvan el stock dos veces.
        SELECT @Estado = Estado
        FROM dbo.Ventas WITH (UPDLOCK, ROWLOCK)
        WHERE VentaID = @VentaID;

        IF @Estado IS NULL
        BEGIN
            SET @Msg = CONCAT(N'La venta ', @VentaID, N' no existe.');
            THROW 50404, @Msg, 1;
        END;

        IF @Estado = N'Anulada'
        BEGIN
            SET @Msg = CONCAT(N'La venta ', @VentaID, N' ya se encuentra anulada.');
            THROW 50409, @Msg, 1;
        END;

        IF @EmpleadoID IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.Empleados
                           WHERE EmpleadoID = @EmpleadoID AND Activo = 1)
        BEGIN
            SET @Msg = CONCAT(N'El empleado ', @EmpleadoID, N' no existe o está inactivo.');
            THROW 50422, @Msg, 1;
        END;

        UPDATE dbo.Ventas
           SET Estado = N'Anulada'
         WHERE VentaID = @VentaID;

        DECLARE @Cambios TABLE (
            ProductoID    INT NOT NULL,
            Cantidad      INT NOT NULL,
            StockAnterior INT NOT NULL,
            StockNuevo    INT NOT NULL
        );

        UPDATE p
           SET p.Stock = p.Stock + d.Cantidad
        OUTPUT inserted.ProductoID, d.Cantidad, deleted.Stock, inserted.Stock
          INTO @Cambios (ProductoID, Cantidad, StockAnterior, StockNuevo)
        FROM dbo.Productos AS p
        INNER JOIN dbo.DetalleVenta AS d ON d.ProductoID = p.ProductoID
        WHERE d.VentaID = @VentaID;

        INSERT INTO dbo.MovimientosInventario
            (ProductoID, TipoMovimiento, Cantidad, StockAnterior, StockNuevo,
             ReferenciaTipo, ReferenciaID, EmpleadoID)
        SELECT ProductoID, N'Entrada', Cantidad, StockAnterior, StockNuevo,
               N'Venta', @VentaID, @EmpleadoID
        FROM @Cambios;

        IF @TranPropia = 1
            COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @TranPropia = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- ============================================================
-- 3. vw_ProductosStockBajo
-- ------------------------------------------------------------
-- Productos activos cuyo stock llegó al mínimo o está por debajo.
-- UnidadesFaltantes = cuánto falta para volver al mínimo.
-- ============================================================
CREATE OR ALTER VIEW dbo.vw_ProductosStockBajo
AS
SELECT
    p.ProductoID,
    p.CodigoBarras,
    p.Nombre,
    c.Nombre                AS Categoria,
    pr.RazonSocial          AS Proveedor,
    p.Stock,
    p.StockMinimo,
    (p.StockMinimo - p.Stock) AS UnidadesFaltantes
FROM dbo.Productos AS p
INNER JOIN dbo.Categorias  AS c  ON c.CategoriaID  = p.CategoriaID
LEFT  JOIN dbo.Proveedores AS pr ON pr.ProveedorID = p.ProveedorID
WHERE p.Activo = 1
  AND p.Stock <= p.StockMinimo;
GO

-- ============================================================
-- 4. fn_VentasPorEmpleado (función con valores de tabla en línea)
-- ------------------------------------------------------------
-- Devuelve todas las ventas (incluidas las anuladas) de un empleado.
-- Si el empleado no tiene ventas, devuelve 0 filas; la API se
-- encarga de responder 404 cuando el empleado no existe.
-- ============================================================
CREATE OR ALTER FUNCTION dbo.fn_VentasPorEmpleado (@EmpleadoID INT)
RETURNS TABLE
AS
RETURN
(
    SELECT
        v.VentaID,
        v.NumeroFactura,
        v.FechaVenta,
        v.ClienteID,
        COALESCE(c.Nombres + N' ' + c.Apellidos, N'Consumidor final') AS Cliente,
        v.Subtotal,
        v.IVA,
        v.Total,
        v.FormaPago,
        v.Estado,
        (SELECT COUNT(*) FROM dbo.DetalleVenta AS d
          WHERE d.VentaID = v.VentaID) AS NumeroItems
    FROM dbo.Ventas AS v
    LEFT JOIN dbo.Clientes AS c ON c.ClienteID = v.ClienteID
    WHERE v.EmpleadoID = @EmpleadoID
);
GO

-- ============================================================
-- 5. Índice de apoyo para el historial de movimientos por producto
--    (GET /api/productos/{id}/movimientos ordena por fecha).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_MovInv_Producto_Fecha'
                 AND object_id = OBJECT_ID(N'dbo.MovimientosInventario'))
BEGIN
    CREATE INDEX IX_MovInv_Producto_Fecha
        ON dbo.MovimientosInventario (ProductoID, FechaMovimiento DESC, MovimientoID DESC);
END;
GO
