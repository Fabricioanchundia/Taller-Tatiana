package edu.comisariato.api.exception;

/**
 * 404 Not Found: el recurso solicitado no existe.
 */
public class NotFoundException extends RuntimeException {

    public NotFoundException(String message) {
        super(message);
    }

    public NotFoundException(String message, Throwable cause) {
        super(message, cause);
    }
}
