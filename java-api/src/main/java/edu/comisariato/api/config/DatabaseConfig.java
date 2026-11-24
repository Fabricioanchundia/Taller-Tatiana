package edu.comisariato.api.config;

import javax.sql.DataSource;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.support.JdbcTransactionManager;
import org.springframework.retry.annotation.EnableRetry;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.annotation.EnableTransactionManagement;

/**
 * Configuración de acceso a datos.
 *
 * <ul>
 *   <li>El {@link DataSource} (pool HikariCP) se crea a partir de
 *       {@code spring.datasource.*} en application.yml / variables de entorno.</li>
 *   <li>{@link JdbcTemplate}: SQL explícito, sin JPA/Hibernate.</li>
 *   <li>{@link JdbcTransactionManager}: permite usar {@code @Transactional}
 *       en la capa de servicio; JdbcTemplate participa automáticamente
 *       de la transacción abierta por el servicio.</li>
 *   <li>{@link EnableRetry}: habilita {@code @Retryable} para reintentar
 *       ante deadlocks (error 1205). El interceptor de reintento tiene mayor
 *       precedencia que el transaccional, así que cada intento abre una
 *       transacción nueva.</li>
 * </ul>
 */
@Configuration
@EnableTransactionManagement
@EnableRetry
public class DatabaseConfig {

    private static final int QUERY_TIMEOUT_SECONDS = 30;

    @Bean
    public JdbcTemplate jdbcTemplate(DataSource dataSource) {
        JdbcTemplate jdbcTemplate = new JdbcTemplate(dataSource);
        jdbcTemplate.setQueryTimeout(QUERY_TIMEOUT_SECONDS);
        return jdbcTemplate;
    }

    @Bean
    public PlatformTransactionManager transactionManager(DataSource dataSource) {
        return new JdbcTransactionManager(dataSource);
    }
}
