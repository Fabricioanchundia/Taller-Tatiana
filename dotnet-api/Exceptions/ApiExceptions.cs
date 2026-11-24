namespace Comisariato.Api.Exceptions;

/// <summary>
/// Excepción base de dominio. Cada subclase sabe qué código HTTP le corresponde;
/// el GlobalExceptionMiddleware la convierte en ProblemDetails (RFC 7807).
/// </summary>
public abstract class ApiException : Exception
{
    protected ApiException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public abstract int StatusCode { get; }

    public abstract string Title { get; }
}

/// <summary>400: dato de entrada inválido detectado en el servidor o en el SP.</summary>
public class BadRequestException : ApiException
{
    public BadRequestException(string message, Exception? innerException = null)
        : base(message, innerException) { }

    public override int StatusCode => StatusCodes.Status400BadRequest;
    public override string Title => "Solicitud inválida";
}

/// <summary>404: el recurso no existe.</summary>
public class NotFoundException : ApiException
{
    public NotFoundException(string message, Exception? innerException = null)
        : base(message, innerException) { }

    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string Title => "Recurso no encontrado";
}

/// <summary>409: conflicto de estado (factura duplicada, venta ya anulada).</summary>
public class ConflictException : ApiException
{
    public ConflictException(string message, Exception? innerException = null)
        : base(message, innerException) { }

    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string Title => "Conflicto de estado";
}

/// <summary>
/// Deadlock (error 1205). Polly la reintenta una vez; si persiste se
/// responde 409 porque hereda de ConflictException.
/// </summary>
public class DeadlockException : ConflictException
{
    public DeadlockException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

/// <summary>422: regla de negocio violada (stock insuficiente, empleado inactivo...).</summary>
public class BusinessException : ApiException
{
    public BusinessException(string message, Exception? innerException = null)
        : base(message, innerException) { }

    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string Title => "Regla de negocio violada";
}
