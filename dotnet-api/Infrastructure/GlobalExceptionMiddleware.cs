using Comisariato.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Comisariato.Api.Infrastructure;

/// <summary>
/// Middleware global de excepciones → ProblemDetails (RFC 7807).
///  400 → validación de entrada (BadRequestException / BadHttpRequestException)
///  404 → NotFoundException
///  409 → ConflictException (incluye DeadlockException persistente)
///  422 → BusinessException
///  500 → cualquier otra excepción
/// Los errores de validación de [ApiController] ya devuelven
/// ValidationProblemDetails (400) antes de llegar al controlador.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente cerró la conexión: no hay a quién responder.
            _logger.LogInformation("Petición cancelada por el cliente: {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, title, detail) = ex switch
        {
            ApiException api => (api.StatusCode, api.Title, api.Message),
            BadHttpRequestException bad => (StatusCodes.Status400BadRequest, "Solicitud inválida", bad.Message),
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor",
                  "Ocurrió un error inesperado. Revise los logs del servidor.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            // ERROR con stack trace y contexto.
            _logger.LogError(ex, "{Status} {Method} {Path} TraceId={TraceId}: {Message}",
                status, context.Request.Method, context.Request.Path, context.TraceIdentifier, ex.Message);
        }
        else
        {
            _logger.LogWarning("{Status} {Method} {Path} TraceId={TraceId}: {Message}",
                status, context.Request.Method, context.Request.Path, context.TraceIdentifier, ex.Message);
        }

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["timestamp"] = DateTimeOffset.Now;

        context.Response.Clear();
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
