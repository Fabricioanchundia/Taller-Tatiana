package edu.comisariato.api.repository;

import java.sql.SQLException;

import org.springframework.dao.DataAccessException;

import edu.comisariato.api.exception.BadRequestException;
import edu.comisariato.api.exception.BusinessException;
import edu.comisariato.api.exception.ConflictException;
import edu.comisariato.api.exception.DeadlockException;
import edu.comisariato.api.exception.NotFoundException;

/**
 * Traduce los errores de SQL Server a excepciones de dominio.
 * Convención de los SP: número de error = 50000 + código HTTP.
 */
final class SqlServerErrorTranslator {

    static final int ERR_BAD_REQUEST = 50400;
    static final int ERR_NOT_FOUND = 50404;
    static final int ERR_CONFLICT = 50409;
    static final int ERR_BUSINESS = 50422;
    static final int ERR_DEADLOCK = 1205;
    static final int ERR_UNIQUE_CONSTRAINT = 2627;
    static final int ERR_UNIQUE_INDEX = 2601;
    static final int ERR_CHECK_OR_FK = 547;

    private SqlServerErrorTranslator() {
    }

    static RuntimeException translate(DataAccessException ex) {
        SQLException sqlEx = findSqlException(ex);
        if (sqlEx == null) {
            return ex;
        }
        String message = sqlEx.getMessage();
        return switch (sqlEx.getErrorCode()) {
            case ERR_BAD_REQUEST -> new BadRequestException(message, ex);
            case ERR_NOT_FOUND -> new NotFoundException(message, ex);
            case ERR_CONFLICT -> new ConflictException(message, ex);
            case ERR_BUSINESS -> new BusinessException(message, ex);
            case ERR_UNIQUE_CONSTRAINT, ERR_UNIQUE_INDEX ->
                    new ConflictException("Registro duplicado (por ejemplo, número de factura repetido).", ex);
            case ERR_CHECK_OR_FK ->
                    new BusinessException("Se violó una restricción de integridad de la base de datos.", ex);
            case ERR_DEADLOCK ->
                    new DeadlockException("La transacción fue elegida como víctima de un deadlock (1205).", ex);
            default -> ex;
        };
    }

    private static SQLException findSqlException(Throwable ex) {
        Throwable current = ex;
        while (current != null) {
            if (current instanceof SQLException sqlException) {
                return sqlException;
            }
            current = current.getCause();
        }
        return null;
    }
}
