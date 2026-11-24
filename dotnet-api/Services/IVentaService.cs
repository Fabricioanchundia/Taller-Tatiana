using Comisariato.Api.DTOs;

namespace Comisariato.Api.Services;

public interface IVentaService
{
    Task<VentaResponse> RegistrarVentaAsync(VentaRequest request, CancellationToken cancellationToken);

    Task<VentaResponse> AnularVentaAsync(int ventaId, int? empleadoId, CancellationToken cancellationToken);

    Task<VentaResponse> ObtenerVentaAsync(int ventaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VentaEmpleadoResponse>> VentasPorEmpleadoAsync(int empleadoId,
        CancellationToken cancellationToken);
}
