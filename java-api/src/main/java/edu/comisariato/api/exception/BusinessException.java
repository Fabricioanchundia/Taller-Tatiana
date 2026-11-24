package edu.comisariato.api.exception;

/**
 * 422 Unprocessable Entity: se violó una regla de negocio (stock insuficiente, empleado inactivo...).
 */
public class BusinessException extends RuntimeException {

    public BusinessException(String message) {
        super(message);
    }

    public BusinessException(String message, Throwable cause) {
        super(message, cause);
    }
}
