using Microsoft.Data.SqlClient;

namespace Comisariato.Api.Infrastructure;

/// <summary>Crea y abre conexiones a SQL Server (ADO.NET).</summary>
public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        // Se puede sobrescribir con la variable de entorno ConnectionStrings__Comisariato
        _connectionString = configuration.GetConnectionString("Comisariato")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'ConnectionStrings:Comisariato'. Revise appsettings.example.json.");
    }

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
