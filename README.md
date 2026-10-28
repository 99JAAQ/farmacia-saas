# Farmacia SaaS

Software de gestión para farmacias en Colombia: inventario con lotes y vencimientos, compras a proveedores, caja por turnos y, próximamente, punto de venta (POS). Está construido para una farmacia real y pensado como SaaS multi-tenant: cada farmacia ve solo sus datos.

> English summary: [README.en.md](README.en.md)

## Qué hace hoy

- **Catálogo**: productos con varias presentaciones (caja, blíster, tableta), cada una con su factor de conversión, su precio y su código de barras.
- **Compras**: facturas de proveedor en borrador que se confirman al recibir la mercancía. Manejan bonificaciones, descuentos por línea y por factura, IVA mixto y verificación contra el total impreso. Al confirmar, cada línea crea un lote con su costo real por unidad.
- **Inventario**: stock por lote con kardex (cada cambio queda registrado) y salida por FEFO (primero lo que vence primero).
- **Caja**: una caja, varios turnos al día. Abre con una base y registra ingresos y egresos; al cerrar, el cajero cuenta a ciegas (sin ver cuánto debería haber).
- **Usuarios y roles**: dueño, cajero y bodeguero, con permisos distintos.

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API (controllers), Clean Architecture + CQRS con MediatR |
| Datos | EF Core 10, SQL Server 2022 (Docker en desarrollo) |
| Auth | ASP.NET Core Identity + JWT propio + refresh token en cookie HttpOnly |
| Frontend | React 19, TypeScript, Vite, TanStack Query, React Router, Tailwind + shadcn/ui |
| Tests | xUnit + Testcontainers (SQL Server real), Vitest |

## Arrancar en 5 minutos

Requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download), [Node.js 22+](https://nodejs.org) y [Docker Desktop](https://www.docker.com/products/docker-desktop/).

```bash
# 1. Base de datos
cp .env.example .env              # y cambie MSSQL_SA_PASSWORD
docker compose up -d

# 2. Secretos de la API (una sola vez; ver docs/desarrollo.md)
dotnet user-secrets set "ConnectionStrings:Farmacia" "Server=localhost,14330;Database=Farmacia;User Id=sa;Password=<la de .env>;TrustServerCertificate=True" --project src/Farmacia.Api
dotnet user-secrets set "Jwt:SigningKey" "<clave base64 de 32+ bytes>" --project src/Farmacia.Api

# 3. Esquema de la base
dotnet tool restore
dotnet ef database update --project src/Farmacia.Infrastructure --startup-project src/Farmacia.Api

# 4. API (http://localhost:5228, documentación en /scalar)
dotnet run --project src/Farmacia.Api

# 5. Frontend, en otra terminal (http://localhost:5173)
cd web && npm install && npm run dev
```

En desarrollo se crean usuarios de prueba: `dueno@farmacia.dev`, `cajero@farmacia.dev` y `bodega@farmacia.dev`, con la contraseña definida en `src/Farmacia.Api/appsettings.Development.json`.

## Documentación

Todo está en [`docs/`](docs/README.md). Si es nuevo en el equipo, empiece por [arquitectura](docs/arquitectura.md) y [estructura de carpetas](docs/estructura-carpetas.md).

## Tests

```bash
dotnet test            # backend (Docker encendido: los tests de integración levantan su propio SQL Server)
cd web && npm test     # frontend
```
