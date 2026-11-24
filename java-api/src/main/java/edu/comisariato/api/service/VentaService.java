package edu.comisariato.api.service;

import java.util.List;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.retry.annotation.Backoff;
import org.springframework.retry.annotation.Retryable;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import edu.comisariato.api.dto.VentaEmpleadoResponse;
import edu.comisariato.api.dto.VentaRequest;
import edu.comisariato.api.dto.VentaResponse;
import edu.comisariato.api.exception.DeadlockException;
import edu.comisariato.api.exception.NotFoundException;
import edu.comisariato.api.repository.VentaRepository;

/**
 * Capa de servicio: aquí se abre la transacción (@Transactional).
 *
 * <p>Flujo de las operaciones que modifican datos:
 * <ol>
 *   <li>Spring abre la transacción al entrar al método.</li>
 *   <li>El repositorio ejecuta el SP dentro de esa transacción.</li>
 *   <li>Si el método termina bien → COMMIT.</li>
 *   <li>Si lanza una RuntimeException → ROLLBACK automático.</li>
 *   <li>Si la excepción es un deadlock (1205) → @Retryable espera 1 s y
 *       reintenta UNA vez con una transacción nueva.</li>
 * </ol>
 */
@Service
public class VentaService {

    private static final Logger log = LoggerFactory.getLogger(VentaService.class);

    private final VentaRepository ventaRepository;

    public VentaService(VentaRepository ventaRepository) {
        this.ventaRepository = ventaRepository;
    }

    @Transactional
    @Retryable(retryFor = DeadlockException.class, maxAttempts = 2, backoff = @Backoff(delay = 1000))
    public VentaResponse registrarVenta(VentaRequest request) {
        log.info("Registrando venta factura={} empleadoId={} clienteId={} lineas={}",
                request.numeroFactura(), request.empleadoId(), request.clienteId(),
                request.detalles().size());
        try {
            int ventaId = ventaRepository.registrarVenta(request);
            VentaResponse venta = ventaRepository.buscarPorId(ventaId)
                    .orElseThrow(() -> new IllegalStateException(
                            "La venta " + ventaId + " no se encontró después de registrarla."));
            log.info("Venta registrada (COMMIT) ventaId={} factura={} total={}",
                    venta.ventaId(), venta.numeroFactura(), venta.total());
            return venta;
        } catch (DeadlockException ex) {
            log.warn("Deadlock (1205) al registrar factura={}. Se hará ROLLBACK y se reintentará una vez.",
                    request.numeroFactura());
            throw ex;
        } catch (RuntimeException ex) {
            log.error("Error al registrar venta factura={} empleadoId={} (ROLLBACK): {}",
                    request.numeroFactura(), request.empleadoId(), ex.getMessage(), ex);
            throw ex;
        }
    }

    @Transactional
    @Retryable(retryFor = DeadlockException.class, maxAttempts = 2, backoff = @Backoff(delay = 1000))
    public VentaResponse anularVenta(int ventaId, Integer empleadoId) {
        log.info("Anulando venta ventaId={} empleadoId={}", ventaId, empleadoId);
        try {
            ventaRepository.anularVenta(ventaId, empleadoId);
            VentaResponse venta = ventaRepository.buscarPorId(ventaId)
                    .orElseThrow(() -> new NotFoundException("La venta " + ventaId + " no existe."));
            log.info("Venta anulada (COMMIT) ventaId={} factura={}", ventaId, venta.numeroFactura());
            return venta;
        } catch (DeadlockException ex) {
            log.warn("Deadlock (1205) al anular ventaId={}. Se hará ROLLBACK y se reintentará una vez.",
                    ventaId);
            throw ex;
        } catch (RuntimeException ex) {
            log.error("Error al anular venta ventaId={} (ROLLBACK): {}", ventaId, ex.getMessage(), ex);
            throw ex;
        }
    }

    @Transactional(readOnly = true)
    public VentaResponse obtenerVenta(int ventaId) {
        return ventaRepository.buscarPorId(ventaId)
                .orElseThrow(() -> new NotFoundException("La venta " + ventaId + " no existe."));
    }

    @Transactional(readOnly = true)
    public List<VentaEmpleadoResponse> ventasPorEmpleado(int empleadoId) {
        if (!ventaRepository.existeEmpleado(empleadoId)) {
            throw new NotFoundException("El empleado " + empleadoId + " no existe.");
        }
        List<VentaEmpleadoResponse> ventas = ventaRepository.ventasPorEmpleado(empleadoId);
        log.info("Consulta de ventas empleadoId={} resultados={}", empleadoId, ventas.size());
        return ventas;
    }
}
