package edu.comisariato.api.controller;

import java.util.List;

import org.springframework.http.ProblemDetail;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import edu.comisariato.api.dto.VentaEmpleadoResponse;
import edu.comisariato.api.service.VentaService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.constraints.Positive;

@RestController
@RequestMapping("/api/empleados")
@Tag(name = "Empleados", description = "Consultas por empleado")
public class EmpleadoController {

    private final VentaService ventaService;

    public EmpleadoController(VentaService ventaService) {
        this.ventaService = ventaService;
    }

    @GetMapping("/{empleadoId}/ventas")
    @Operation(summary = "4. Ventas por empleado (fn_VentasPorEmpleado)")
    @ApiResponse(responseCode = "200", description = "Lista de ventas; arreglo vacío si no tiene ventas")
    @ApiResponse(responseCode = "404", description = "El empleado no existe",
            content = @Content(schema = @Schema(implementation = ProblemDetail.class)))
    public ResponseEntity<List<VentaEmpleadoResponse>> ventasPorEmpleado(
            @PathVariable("empleadoId") @Positive int empleadoId) {
        return ResponseEntity.ok(ventaService.ventasPorEmpleado(empleadoId));
    }
}
