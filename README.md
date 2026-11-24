# Taller de Integración: API Java y API .NET sobre la BD Comisariato

Dos aplicaciones **independientes** (no comparten código ni se comunican entre sí) que consumen la misma base de datos SQL Server `Comisariato`.

| Carpeta | Contenido |
|---|---|
| `database/` | Script base (01), objetos del taller (02) y pruebas en SQL (03) |
| `java-api/` | Spring Boot 3 · Java 17 · JdbcTemplate · mssql-jdbc |
| `dotnet-api/` | ASP.NET Core 8 · ADO.NET (Microsoft.Data.SqlClient) · Polly |
| `docs/pruebas.http` | Peticiones listas para probar los 5 endpoints y sus códigos HTTP |

## Endpoints (iguales en ambas APIs)

| # | Endpoint | Verbo | Objeto de BD | Códigos |
|---|---|---|---|---|
| 1 | `/api/ventas` | POST | `usp_RegistrarVenta` | 201 · 400 · 409 (factura duplicada) · 422 (stock insuficiente) |
| 2 | `/api/ventas/{ventaId}/anular` | POST | `usp_AnularVenta` | 200 · 404 · 409 (ya anulada) |
| 3 | `/api/productos/stock-bajo` | GET | Vista `vw_ProductosStockBajo` | 200 |
| 4 | `/api/empleados/{empleadoId}/ventas` | GET | Función `fn_VentasPorEmpleado` | 200 (array vacío si no tiene ventas) · 404 |
| 5 | `/api/productos/{productoId}/movimientos` | GET | Tabla `MovimientosInventario` | 200 · 404 |

Extra: `GET /api/ventas/{ventaId}` (destino del header `Location` del 201).

**Menú:** cada API sirve un menú web en su raíz (`http://localhost:8080/` y `http://localhost:5080/`) con las 5 operaciones. También se pueden ejecutar desde Swagger.

### Cuerpo de POST /api/ventas

```json
{
  "numeroFactura": "001-001-000000101",
  "clienteId": 1,
  "empleadoId": 1,
  "formaPago": "Efectivo",
  "detalles": [
    { "productoId": 1, "cantidad": 2 },
    { "productoId": 3, "cantidad": 1, "descuento": 0.20 }
  ]
}
```

No se envían precios ni `total`: el SP toma `Productos.PrecioVenta` y calcula Subtotal, IVA (15 %) y Total. `clienteId` puede ser `null` (consumidor final). `formaPago`: `Efectivo`, `Tarjeta` o `Transferencia`.

## 1. Base de datos

Requisito: SQL Server 2016 SP1 o superior (usa `CREATE OR ALTER` y `OPENJSON`).

1. Ejecutar `database/01_Comisariato_base.sql` (una sola vez, sobre una base vacía).
2. Ejecutar `database/02_Objetos_Taller.sql` (se puede repetir sin problemas).
3. (Opcional) `database/03_Pruebas_SQL.sql` para probar los SP directamente en SSMS. Ojo: crea ventas; si después va a grabar el video, reinstale la base o ajuste los números de factura e IDs.

### Convención de errores de los SP

`THROW` usa el número **50000 + código HTTP**, y ambas APIs lo traducen igual:

| Error SQL | Excepción | HTTP |
|---|---|---|
| 50400 | BadRequestException | 400 |
| 50404 | NotFoundException | 404 |
| 50409 · 2627 · 2601 | ConflictException | 409 |
| 50422 · 547 | BusinessException | 422 |
| 1205 (deadlock) | DeadlockException → 1 reintento a 1 s; si se repite | 409 |
| cualquier otro | — | 500 |

## 2. API Java

Requisitos: JDK 17+, Maven 3.9+.

```bash
cd java-api
# Credenciales por variables de entorno (no se suben al repo)
# Windows PowerShell:
$env:DB_USER="sa"; $env:DB_PASSWORD="TuClave"
# Linux / macOS:
export DB_USER=sa DB_PASSWORD=TuClave
# Opcional: export DB_URL="jdbc:sqlserver://localhost:1433;databaseName=Comisariato;encrypt=true;trustServerCertificate=true"

mvn spring-boot:run
```

- Menú: http://localhost:8080/
- Swagger UI: http://localhost:8080/swagger-ui.html
- Health: http://localhost:8080/actuator/health

Si el login `sa` está deshabilitado, habilite la autenticación mixta en SQL Server o cree un login SQL con permisos sobre `Comisariato`. `application.yml.example` tiene ejemplos (incluido `SQLEXPRESS`).

## 3. API .NET

Requisitos: .NET 8 SDK.

```bash
cd dotnet-api
dotnet restore
dotnet run --launch-profile http
```

- Menú: http://localhost:5080/
- Swagger UI: http://localhost:5080/swagger
- Health: http://localhost:5080/health

La cadena de conexión de `appsettings.json` usa autenticación de Windows. Para usar autenticación SQL sin escribir la clave en el repo:

```powershell
$env:ConnectionStrings__Comisariato="Server=localhost,1433;Database=Comisariato;User Id=sa;Password=TuClave;TrustServerCertificate=True"
dotnet run --launch-profile http
```

## 4. Probar

Abra `docs/pruebas.http` en Visual Studio, VS Code (extensión REST Client) o IntelliJ y ejecute las peticiones en orden. Cambie `@host` (8080 Java / 5080 .NET) y `@fac` para que las dos APIs no choquen con el mismo número de factura.

## 5. Subir a GitHub

```bash
git init
git add .
git commit -m "Taller Comisariato: API Java y API .NET"
git branch -M main
git remote add origin https://github.com/<usuario>/<repositorio>.git
git push -u origin main
```

`.gitignore` excluye `target/`, `bin/`, `obj/` y archivos de credenciales locales.

## 6. Guion sugerido para el video demo (8–10 min)

1. **(1 min)** Mostrar la estructura del repo y los objetos en SSMS (SPs, vista, función).
2. **(30 s)** Levantar la API Java; mostrar `/actuator/health` en `UP`.
3. **(3 min)** En Swagger o en el menú, ejecutar en orden: venta OK (201) → misma factura (409) → sin campos (400) → stock insuficiente (422) → venta de 25 detergentes → stock bajo → anular (200) → anular otra vez (409) → venta inexistente (404) → ventas por empleado 1, 2 (`[]`) y 999 (404) → movimientos del producto 1.
4. **(1 min)** Mostrar en consola los logs INFO (COMMIT) y ERROR (ROLLBACK con stack trace).
5. **(30 s)** En SSMS, comprobar que la venta rechazada por stock **no** dejó cabecera ni detalle (rollback).
6. **(3 min)** Repetir 2–4 con la API .NET (`/health`, `/swagger`, prefijo de factura `0000002`).
7. **(1 min)** Explicar en el código: `@Transactional` + `@Retryable` (Java) y `SqlTransaction` + Polly (.NET).
