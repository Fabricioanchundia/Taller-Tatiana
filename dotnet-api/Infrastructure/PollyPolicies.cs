using Comisariato.Api.Exceptions;
using Polly;
using Polly.Retry;

namespace Comisariato.Api.Infrastructure;

/// <summary>Políticas de resiliencia con Polly.</summary>
public static class PollyPolicies
{
    /// <summary>
    /// Reintento ante deadlock (SQL Server 1205): 1 reintento después de 1 segundo.
    /// Cada intento vuelve a ejecutar toda la unidad de trabajo, es decir,
    /// abre una conexión y una transacción nuevas. Si el segundo intento
    /// también falla, la DeadlockException se propaga y se responde 409.
    /// </summary>
    public static AsyncRetryPolicy DeadlockRetry(ILogger logger) =>
        Policy
            .Handle<DeadlockException>()
            .WaitAndRetryAsync(
                1,                                   // un solo reintento
                _ => TimeSpan.FromSeconds(1),        // espera de 1 segundo (Task.Delay interno)
                (exception, delay, retryAttempt, _) =>
                    logger.LogWarning(exception,
                        "Deadlock 1205 detectado. Reintento {RetryAttempt} en {DelayMs} ms.",
                        retryAttempt, delay.TotalMilliseconds));
}
