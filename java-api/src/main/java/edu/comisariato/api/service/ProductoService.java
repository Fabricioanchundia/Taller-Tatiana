package edu.comisariato.api.service;

import java.util.List;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import edu.comisariato.api.dto.MovimientoInventarioResponse;
import edu.comisariato.api.dto.ProductoStockBajoResponse;
import edu.comisariato.api.exception.NotFoundException;
import edu.comisariato.api.repository.ProductoRepository;

@Service
public class ProductoService {

    private static final Logger log = LoggerFactory.getLogger(ProductoService.class);

    private final ProductoRepository productoRepository;

    public ProductoService(ProductoRepository productoRepository) {
        this.productoRepository = productoRepository;
    }

    @Transactional(readOnly = true)
    public List<ProductoStockBajoResponse> productosStockBajo() {
        List<ProductoStockBajoResponse> productos = productoRepository.productosStockBajo();
        log.info("Consulta de productos con stock bajo resultados={}", productos.size());
        return productos;
    }

    @Transactional(readOnly = true)
    public List<MovimientoInventarioResponse> movimientosPorProducto(int productoId) {
        if (!productoRepository.existeProducto(productoId)) {
            throw new NotFoundException("El producto " + productoId + " no existe.");
        }
        List<MovimientoInventarioResponse> movimientos =
                productoRepository.movimientosPorProducto(productoId);
        log.info("Consulta de movimientos productoId={} resultados={}", productoId, movimientos.size());
        return movimientos;
    }
}
