package edu.comisariato.api.controller;

import java.net.URI;

import org.springframework.http.ProblemDetail;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.servlet.support.ServletUriComponentsBuilder;

import edu.comisariato.api.dto.VentaRequest;
import edu.comisariato.api.dto.VentaResponse;
import edu.comisariato.api.service.VentaService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Positive;

@RestController
@RequestMapping("/api/ventas")
@Tag(name = "Ventas", description = "Registro y anulación de ventas")
public class VentaController {

    private final VentaService ventaService;

    public VentaController(VentaService ventaService) {
        this.ventaService = ventaService;
    }

    @PostMapping
    @Operation(summary = "1. Registrar venta (usp_RegistrarVenta)",
            description = "El Total no se envía: lo calcula el SP en el servidor.")
    @ApiResponse(responseCode = "201", description = "Venta creada")
    @ApiResponse(responseCode = "400", description = "Falta un campo obligatorio o es inválido",
            content = @Content(schema = @Schema(implementation = ProblemDetail.class)))
    @ApiResponse(responseCode = "409", description = "El NumeroFactura ya existe",
            content = @Content(schema = @Schema(implementation = ProblemDetail.class)))
    @ApiResponse(responseCode = "422", description = "Stock insuficiente u otra regla de negocio",
            content = @Content(schema = @Schema(implementation = ProblemDetail.class)))
    public ResponseEntity<VentaResponse> registrarVenta(@Valid @RequestBody VentaRequest request) {
        VentaResponse venta = ventaService.registrarVenta(request);
        URI location = ServletUriComponentsBuilder.fromCurrentRequest()
                .path("/{ventaId}")
                .buildAndExpand(venta.ventaId())
                .toUri();
        return ResponseEntity.created(location).body(venta);
    }

    @PostMapping("/{ventaId}/anular")
    @Operation(summary = "2. Anular venta (usp_AnularVenta)",
            description = "Devuelve el stock y registra movimientos de entrada. "
                    + "empleadoId (opcional) indica quién anula.")
    @ApiResponse(responseCode = "200", description = "Venta anulada")
    @ApiResponse(responseCode = "404", description = "La venta no existe",
            content = @Content(schema = @Schema(implementation = ProblemDetail.class)))
    @ApiResponse(responseCode = "409", description = "La venta ya estaba anulada",
            content = @Content(schema = @Schema(implementation = ProblemDetail.class)))
    public ResponseEntity<VentaResponse> anularVenta(
            @PathVariable("ventaId") @Positive int ventaId,
            @RequestParam(name = "empleadoId", required = false) @Positive Integer empleadoId) {
        return ResponseEntity.ok(ventaService.anularVenta(ventaId, empleadoId));
    }

    @GetMapping("/{ventaId}")
    @Operation(summary = "Consultar una venta por id (apoyo para el header Location)")
    @ApiResponse(responseCode = "200", description = "Venta encontrada")
    @ApiResponse(responseCode = "404", description = "La venta no existe",
            content = @Content(schema = @Schema(implementation = ProblemDetail.class)))
    public ResponseEntity<VentaResponse> obtenerVenta(@PathVariable("ventaId") @Positive int ventaId) {
        return ResponseEntity.ok(ventaService.obtenerVenta(ventaId));
    }
}
