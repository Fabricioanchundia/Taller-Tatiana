package edu.comisariato.api.exception;

/**
 * 400 Bad Request: el dato de entrada es inválido (validación hecha en el servidor/SP).
 */
public class BadRequestException extends RuntimeException {

    public BadRequestException(String message) {
        super(message);
    }

    public BadRequestException(String message, Throwable cause) {
        super(message, cause);
    }
}
