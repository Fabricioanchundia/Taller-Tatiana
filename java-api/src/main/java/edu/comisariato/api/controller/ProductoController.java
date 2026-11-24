package edu.comisariato.api.controller;

import java.util.List;

import org.springframework.http.ProblemDetail;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import edu.comisariato.api.dto.MovimientoInventarioResponse;
import edu.comisariato.api.dto.ProductoStockBajoResponse;
import edu.comisariato.api.service.ProductoService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.constraints.Positive;

@RestController
@RequestMapping("/api/productos")
@Tag(name = "Productos", description = "Inventario")
public class ProductoController {

    private final ProductoService productoService;

    public ProductoController(ProductoService productoService) {
        this.productoService = productoService;
    }

    @GetMapping("/stock-bajo")
    @Operation(summary = "3. Productos con stock bajo (vw_ProductosStockBajo)")
    @ApiResponse(responseCode = "200", description = "Lista (puede estar vacía)")
    public ResponseEntity<List<ProductoStockBajoResponse>> productosStockBajo() {
        return ResponseEntity.ok(productoService.productosStockBajo());
    }

    @GetMapping("/{productoId}/movimientos")
    @Operation(summary = "5. Movimientos de inventario por producto (tabla MovimientosInventario)")
    @ApiResponse(responseCode = "200", description = "Lista (puede estar vacía)")
    @ApiResponse(responseCode = "404", description = "El producto no existe",
            content = @Content(schema = @Schema(implementation = ProblemDetail.class)))
    public ResponseEntity<List<MovimientoInventarioResponse>> movimientosPorProducto(
            @PathVariable("productoId") @Positive int productoId) {
        return ResponseEntity.ok(productoService.movimientosPorProducto(productoId));
    }
}
