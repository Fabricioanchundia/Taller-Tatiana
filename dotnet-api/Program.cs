using System.Text.Json;
using Comisariato.Api.Infrastructure;
using Comisariato.Api.Repositories;
using Comisariato.Api.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
// Configuración: appsettings.json → appsettings.{Environment}.json → variables de entorno
// (por ejemplo ConnectionStrings__Comisariato). Esto lo hace CreateBuilder por defecto.

// ---------- Servicios ----------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Comisariato API (.NET)",
        Version = "v1",
        Description = "Taller de transacciones: ventas, anulaciones e inventario sobre SQL Server."
    });
});

// Capas: Controllers → Services → Repositories → Infrastructure
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IVentaRepository, VentaRepository>();
builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<IProductoService, ProductoService>();

builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sqlserver");

var app = builder.Build();

// ---------- Pipeline ----------
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger habilitado en todos los entornos para la demostración del taller.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Comisariato API v1");
    options.RoutePrefix = "swagger";
});

// Menú HTML (wwwroot/index.html) con las 5 operaciones.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                error = e.Value.Exception?.Message
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
});

app.Logger.LogInformation("Comisariato API (.NET) iniciada. Swagger: /swagger  Health: /health");

app.Run();
