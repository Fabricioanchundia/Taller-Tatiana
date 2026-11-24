package edu.comisariato.api.exception;

import java.net.URI;
import java.time.OffsetDateTime;
import java.util.LinkedHashMap;
import java.util.Map;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatus;
import org.springframework.http.HttpStatusCode;
import org.springframework.http.ProblemDetail;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.context.request.WebRequest;
import org.springframework.web.servlet.mvc.method.annotation.ResponseEntityExceptionHandler;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.ConstraintViolationException;

/**
 * Traduce excepciones a respuestas HTTP con formato ProblemDetail (RFC 7807).
 *
 * <pre>
 *  400 → validación de entrada (Bean Validation, JSON mal formado, BadRequestException)
 *  404 → NotFoundException
 *  409 → ConflictException (factura duplicada, venta ya anulada, deadlock persistente)
 *  422 → BusinessException (stock insuficiente, empleado inactivo...)
 *  500 → cualquier otro error
 * </pre>
 *
 * Al extender ResponseEntityExceptionHandler, Spring ya responde 400/404/405
 * para los errores propios del framework (JSON ilegible, tipo de parámetro
 * incorrecto, ruta inexistente, etc.).
 */
@RestControllerAdvice
public class GlobalExceptionHandler extends ResponseEntityExceptionHandler {

    private static final Logger log = LoggerFactory.getLogger(GlobalExceptionHandler.class);

    /** Errores de @Valid sobre el cuerpo de la petición → 400 con detalle por campo. */
    @Override
    protected ResponseEntity<Object> handleMethodArgumentNotValid(
            MethodArgumentNotValidException ex, HttpHeaders headers,
            HttpStatusCode status, WebRequest request) {

        Map<String, String> errores = new LinkedHashMap<>();
        ex.getBindingResult().getFieldErrors()
                .forEach(fe -> errores.putIfAbsent(fe.getField(), fe.getDefaultMessage()));
        ex.getBindingResult().getGlobalErrors()
                .forEach(ge -> errores.putIfAbsent(ge.getObjectName(), ge.getDefaultMessage()));

        ProblemDetail problem = ProblemDetail.forStatusAndDetail(
                HttpStatus.BAD_REQUEST, "La solicitud tiene campos obligatorios faltantes o inválidos.");
        problem.setTitle("Error de validación");
        problem.setProperty("errores", errores);
        problem.setProperty("timestamp", OffsetDateTime.now());

        log.warn("400 Validación fallida en {}: {}", request.getDescription(false), errores);
        return handleExceptionInternal(ex, problem, headers, HttpStatus.BAD_REQUEST, request);
    }

    @ExceptionHandler(ConstraintViolationException.class)
    public ResponseEntity<ProblemDetail> handleConstraintViolation(
            ConstraintViolationException ex, HttpServletRequest request) {
        return build(HttpStatus.BAD_REQUEST, "Error de validación", ex.getMessage(), request, ex);
    }

    @ExceptionHandler(BadRequestException.class)
    public ResponseEntity<ProblemDetail> handleBadRequest(BadRequestException ex, HttpServletRequest request) {
        return build(HttpStatus.BAD_REQUEST, "Solicitud inválida", ex.getMessage(), request, ex);
    }

    @ExceptionHandler(NotFoundException.class)
    public ResponseEntity<ProblemDetail> handleNotFound(NotFoundException ex, HttpServletRequest request) {
        return build(HttpStatus.NOT_FOUND, "Recurso no encontrado", ex.getMessage(), request, ex);
    }

    @ExceptionHandler(ConflictException.class)
    public ResponseEntity<ProblemDetail> handleConflict(ConflictException ex, HttpServletRequest request) {
        return build(HttpStatus.CONFLICT, "Conflicto de estado", ex.getMessage(), request, ex);
    }

    @ExceptionHandler(BusinessException.class)
    public ResponseEntity<ProblemDetail> handleBusiness(BusinessException ex, HttpServletRequest request) {
        return build(HttpStatus.UNPROCESSABLE_ENTITY, "Regla de negocio violada", ex.getMessage(), request, ex);
    }

    @ExceptionHandler(Exception.class)
    public ResponseEntity<ProblemDetail> handleUnexpected(Exception ex, HttpServletRequest request) {
        return build(HttpStatus.INTERNAL_SERVER_ERROR, "Error interno del servidor",
                "Ocurrió un error inesperado. Revise los logs del servidor.", request, ex);
    }

    private ResponseEntity<ProblemDetail> build(HttpStatus status, String title, String detail,
                                                HttpServletRequest request, Exception ex) {
        ProblemDetail problem = ProblemDetail.forStatusAndDetail(status, detail);
        problem.setTitle(title);
        problem.setInstance(URI.create(request.getRequestURI()));
        problem.setProperty("timestamp", OffsetDateTime.now());

        if (status.is5xxServerError()) {
            // ERROR con stack trace y contexto de la petición.
            log.error("{} {} {} -> {}", status.value(), request.getMethod(),
                    request.getRequestURI(), ex.getMessage(), ex);
        } else {
            log.warn("{} {} {} -> {}", status.value(), request.getMethod(),
                    request.getRequestURI(), ex.getMessage());
        }
        return ResponseEntity.status(status).body(problem);
    }
}
