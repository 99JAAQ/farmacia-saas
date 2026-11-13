# Farmacia SaaS

[![CI](https://github.com/99JAAQ/farmacia-saas/actions/workflows/ci.yml/badge.svg)](https://github.com/99JAAQ/farmacia-saas/actions/workflows/ci.yml)

Software de gestión para farmacias en Colombia: punto de venta, caja por turnos, inventario por lotes con vencimientos, compras a proveedores y fiado. Está construido para una farmacia real y pensado como SaaS multi-tenant: cada farmacia ve solo sus datos, y SQL Server lo hace cumplir además del código.

> English summary: [README.en.md](README.en.md)

**Demo:** https://ca-farmacia.politemoss-136a2eb5.centralus.azurecontainerapps.io. Corre en planes gratuitos de Azure, así que la primera visita después de un rato sin uso tarda unos segundos en arrancar.

## Qué hace

- **Punto de venta**: compatible con lector de código de barras, venta fraccionada (caja, blíster, tableta), pago mixto (efectivo, tarjeta, Nequi/Daviplata, transferencia, fiado) y tiquete para impresora térmica de 80 mm. Sale primero lo que vence primero (FEFO).
- **Caja**: varios turnos al día, con base, ingresos y egresos, y cierre con conteo a ciegas (el cajero no ve cuánto debería haber).
- **Devoluciones**: por línea y cantidad, con reintegro en efectivo. El producto vuelve al mismo lote o se descarta.
- **Fiado**: cupo por cliente, abonos al saldo y estado de cuenta.
- **Compras y cuentas por pagar**: facturas con bonificaciones, descuentos e IVA mixto. Cada línea crea un lote con su costo real, y los pagos se registran por factura, con alerta de las vencidas.
- **Inventario**: kardex de cada movimiento con quién lo hizo, bajas de vencidos, ajustes por conteo, y alertas de stock mínimo y vencimientos.
- **En vivo**: el panel del día del dueño (ventas, margen bruto y medios de pago), el stock y las alertas se actualizan solos (SignalR).
- **Multi-tenant**: un administrador de la plataforma crea farmacias con su dueño, y el dueño crea a sus cajeros y bodegueros con contraseña temporal obligatoria.

## Lo técnico que vale la pena mirar

| Tema | Dónde |
|---|---|
| Aislamiento entre farmacias en dos capas: repositorios y Row-Level Security de SQL Server | [ADR 0014](docs/adr/0014-row-level-security.md) |
| JWT propio con refresh token rotado en cookie HttpOnly y detección de reutilización | [ADR 0006](docs/adr/0006-jwt-propio-y-refresh-en-cookie.md) |
| Tiempo real con avisos sin datos, enviados después del commit y derivados de lo que se guardó | [ADR 0017](docs/adr/0017-tiempo-real-con-avisos-delgados.md), [ADR 0018](docs/adr/0018-avisos-desde-lo-que-se-guarda.md) |
| Stock por lote con kardex en la misma transacción, FEFO y concurrencia optimista | [ADR 0007](docs/adr/0007-lotes-kardex-y-fefo.md) |
| Consecutivos sin saltos ni repetidos bajo concurrencia (`MERGE … HOLDLOCK`) | [Base de datos](docs/base-de-datos.md#los-consecutivos-tiquetes-y-devoluciones) |
| Un solo contenedor para la API y el frontend (cookie `SameSite=Strict` sin CORS) | [ADR 0019](docs/adr/0019-un-contenedor-para-api-y-frontend.md) |
| Tests que pueden fallar: pruebas de mutación de las reglas importantes | [Desarrollo](docs/desarrollo.md#tests) |

Todas las decisiones están en [`docs/adr/`](docs/adr/README.md).

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API (controllers), Clean Architecture + CQRS con MediatR, SignalR |
| Datos | EF Core 10, SQL Server 2022 con Row-Level Security |
| Auth | ASP.NET Core Identity + JWT propio + refresh token en cookie HttpOnly |
| Frontend | React 19, TypeScript, Vite, TanStack Query, React Router, Tailwind + shadcn/ui |
| Tests | xUnit + Testcontainers (SQL Server real), Vitest, Playwright (end to end) |
| Infra | Docker (imagen chiseled sin shell), GitHub Actions, Azure Container Apps + Azure SQL + Key Vault (Bicep, OIDC, identidad administrada) |

## Probarlo en un minuto (Docker)

```bash
cp .env.example .env     # complete MSSQL_SA_PASSWORD y JWT_SIGNING_KEY
docker compose --profile app up --build
```

Abra http://localhost:8080 y entre con `admin@farmacia.local` y la contraseña de `BOOTSTRAP_ADMIN_PASSWORD`. Le pedirá cambiarla; después cree su primera farmacia.

## Desarrollo

Requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download), [Node.js 24](https://nodejs.org) y [Docker Desktop](https://www.docker.com/products/docker-desktop/).

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

En desarrollo se crean usuarios de prueba: `dueno@farmacia.dev`, `cajero@farmacia.dev`, `bodega@farmacia.dev` y el administrador de la plataforma `admin@farmacia.dev`, con la contraseña definida en `src/Farmacia.Api/appsettings.Development.json`.

## Tests

```bash
dotnet test                                    # backend (Docker encendido: los tests de integración levantan su propio SQL Server)
cd web && npm test                             # frontend
cd e2e && npm install && npx playwright test   # end to end, con la app corriendo en Docker (ver arriba)
```

El CI (GitHub Actions) corre los tres en cada push.

## Documentación

Todo está en [`docs/`](docs/README.md). Si es nuevo en el equipo, empiece por [arquitectura](docs/arquitectura.md) y [estructura de carpetas](docs/estructura-carpetas.md).
