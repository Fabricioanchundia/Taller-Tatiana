package edu.comisariato.api.exception;

/**
 * Deadlock detectado por SQL Server (error 1205).
 * El servicio la reintenta una vez con {@code @Retryable}; si vuelve a
 * ocurrir se propaga como 409 Conflict (por eso hereda de ConflictException).
 */
public class DeadlockException extends ConflictException {

    public DeadlockException(String message, Throwable cause) {
        super(message, cause);
    }
}
