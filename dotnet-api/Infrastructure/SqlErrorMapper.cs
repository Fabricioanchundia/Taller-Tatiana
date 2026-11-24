using System.Diagnostics.CodeAnalysis;
using Comisariato.Api.Exceptions;
using Microsoft.Data.SqlClient;

namespace Comisariato.Api.Infrastructure;

/// <summary>
/// Traduce errores de SQL Server a excepciones de dominio.
/// Convención de los SP: número de error = 50000 + código HTTP.
/// </summary>
public static class SqlErrorMapper
{
    public const int BadRequest = 50400;
    public const int NotFound = 50404;
    public const int Conflict = 50409;
    public const int Business = 50422;
    public const int Deadlock = 1205;
    public const int UniqueConstraint = 2627;
    public const int UniqueIndex = 2601;
    public const int CheckOrForeignKey = 547;

    public static bool TryMap(SqlException ex, [NotNullWhen(true)] out ApiException? mapped)
    {
        // Se recorre la colección porque SQL Server puede devolver varios errores
        // (por ejemplo, el THROW del SP seguido de un error 266 de conteo de transacciones).
        foreach (SqlError error in ex.Errors)
        {
            mapped = error.Number switch
            {
                BadRequest => new BadRequestException(error.Message, ex),
                NotFound => new NotFoundException(error.Message, ex),
                Conflict => new ConflictException(error.Message, ex),
                Business => new BusinessException(error.Message, ex),
                UniqueConstraint or UniqueIndex =>
                    new ConflictException("Registro duplicado (por ejemplo, número de factura repetido).", ex),
                CheckOrForeignKey =>
                    new BusinessException("Se violó una restricción de integridad de la base de datos.", ex),
                Deadlock =>
                    new DeadlockException("La transacción fue elegida como víctima de un deadlock (1205).", ex),
                _ => null
            };

            if (mapped is not null)
            {
                return true;
            }
        }

        mapped = null;
        return false;
    }
}
