package edu.comisariato.api.exception;

/**
 * 409 Conflict: conflicto de estado (factura duplicada, venta ya anulada).
 */
public class ConflictException extends RuntimeException {

    public ConflictException(String message) {
        super(message);
    }

    public ConflictException(String message, Throwable cause) {
        super(message, cause);
    }
}
