using Comisariato.Api.DTOs;
using Comisariato.Api.Exceptions;
using Comisariato.Api.Infrastructure;
using Comisariato.Api.Repositories;

namespace Comisariato.Api.Services;

public sealed class ProductoService : IProductoService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IProductoRepository _productoRepository;
    private readonly ILogger<ProductoService> _logger;

    public ProductoService(
        ISqlConnectionFactory connectionFactory,
        IProductoRepository productoRepository,
        ILogger<ProductoService> logger)
    {
        _connectionFactory = connectionFactory;
        _productoRepository = productoRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductoStockBajoResponse>> ProductosStockBajoAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var productos = await _productoRepository.ProductosStockBajoAsync(connection, cancellationToken);
        _logger.LogInformation("Consulta de productos con stock bajo resultados={Total}", productos.Count);
        return productos;
    }

    public async Task<IReadOnlyList<MovimientoInventarioResponse>> MovimientosPorProductoAsync(int productoId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        if (!await _productoRepository.ExisteProductoAsync(connection, productoId, cancellationToken))
        {
            throw new NotFoundException($"El producto {productoId} no existe.");
        }

        var movimientos = await _productoRepository.MovimientosPorProductoAsync(
            connection, productoId, cancellationToken);
        _logger.LogInformation("Consulta de movimientos productoId={ProductoId} resultados={Total}",
            productoId, movimientos.Count);
        return movimientos;
    }
}
