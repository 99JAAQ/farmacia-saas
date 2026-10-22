# Farmacia SaaS — contexto del proyecto

## Qué es
Software de gestión para una farmacia real (inventario, POS, caja, compras, reportes), construido como SaaS multi-tenant. Es además el proyecto principal de portafolio de su autor, que está en búsqueda de empleo (meta: trabajo remoto para el exterior). Debe quedar desplegado, bien documentado y fácil de explicar en una entrevista.

## Cómo quiero trabajar contigo
- Actúa como tutor, no solo como generador de código: explica el porqué de cada decisión antes o junto al código.
- Avanza por pasos pequeños; al terminar cada paso, dime qué verificar (build, test, endpoint) antes de seguir.
- Cuando haya una decisión de diseño, plantéame las opciones con sus trade-offs y déjame elegir.
- Responde en español. Los nombres de código (clases, métodos) van en inglés; los mensajes de commit van en español.
- No hagas commits sin que te lo pida.

## Stack
- Backend: .NET 10, ASP.NET Core Web API (controllers), EF Core, SQL Server
- Arquitectura: Clean Architecture + CQRS con MediatR
- Frontend: React + TypeScript (Vite), en la carpeta `web/`
- Tiempo real (fase 4): SignalR
- Infra (fase 5): Docker, GitHub Actions, Azure

## Estructura
```
src/Farmacia.Domain          -> entidades y reglas de negocio, sin dependencias
src/Farmacia.Application     -> casos de uso (commands/queries), interfaces
src/Farmacia.Infrastructure  -> EF Core, repositorios, servicios externos
src/Farmacia.Api             -> controllers, configuración, auth
web/                         -> frontend React
```
Regla de dependencias: siempre hacia adentro (Api -> Infrastructure -> Application -> Domain).

## Decisiones tomadas
- Todas las entidades del negocio llevan `FarmaciaId` (en código: `PharmacyId`) desde el inicio, para preparar el multi-tenant sin migraciones dolorosas después.
- Offline-first se aborda en la fase 6: la farmacia tiene cortes de energía e internet inestable, así que el POS debe poder vender sin conexión y sincronizar después. Mantener la lógica de negocio desacoplada de la base de datos para permitirlo.
- Ids `Guid` v7 generados en el dominio (no por la base de datos): el POS offline debe poder crear registros sin conexión y sin choques de Id al sincronizar.
- Modelo de dominio: Pharmacy (tenant), User/Role, Product, Category, SaleUnit, Batch, StockMovement, Supplier, Purchase/PurchaseItem, Customer (opcional en la venta), Sale/SaleItem/Payment, CashSession/CashMovement.
- La farmacia está en Colombia: NIT obligatorio en farmacia y proveedores, como value object `Nit` que valida el dígito de verificación (módulo 11 DIAN) y exige el formato `900123456-8`; el cliente lleva tipo de documento (`DocumentType`, con los códigos DIAN como valores del enum) pensando en la facturación electrónica.
- Usuarios con ASP.NET Core Identity (vive en Infrastructure).
- Sin `Warehouse`: la farmacia tiene un solo inventario.
- Stock: la cantidad se guarda en `Batch` y cada cambio queda en `StockMovement` (kardex), ambos en la misma transacción. Se vende por FEFO (primero lo que vence primero).
- `Batch` es un agregado aparte de `Product`. Sus métodos generan los `StockMovement` en `PendingMovements`, e Infrastructure los guarda en el mismo `SaveChanges`. El servicio de dominio `FefoAllocator` solo planifica de qué lotes sale una venta; quien vende aplica `Batch.Sell`.
- Stock nunca negativo en línea. En la sincronización offline (fase 6) puede quedar negativo, y se marca como inconsistencia para revisar.
- Base de datos local: SQL Server 2022 en Docker (`docker-compose.yml`), puerto 14330 en el host para no chocar con el SQL Server instalado en 1433. La contraseña va en `.env` (ignorado; plantilla en `.env.example`) y la cadena de conexión `ConnectionStrings:Farmacia` en user-secrets del proyecto Api.
- EF Core con Fluent API en `Infrastructure/Persistence` (el dominio no tiene atributos). Ids con `ValueGeneratedNever`, FK a `Pharmacy` sin cascada, `Nit` guardado como texto, `RowVersion` en `Batch` para concurrencia optimista. `dotnet-ef` como herramienta local (`dotnet-tools.json`).
- `System.Security.Cryptography.Pkcs` está fijado en Infrastructure porque la versión que trae SqlClient tiene vulnerabilidades conocidas; quitarlo cuando SqlClient la actualice.
- Un lote se puede vender hasta su fecha de vencimiento inclusive; vencido no se vende, solo se da de baja. El dominio no usa `DateTime.Now`: recibe las fechas con la hora local de Colombia (UTC-5).
- Venta fraccionada: el stock se guarda en la unidad base (p. ej. tableta); cada `SaleUnit` (caja, blíster, unidad) tiene su factor de conversión y su propio precio.

## Fases
1. Base: estructura, dominio inicial, EF Core + migraciones, auth JWT con roles (dueño, cajero, bodeguero), React con login  <- EN CURSO
2. Núcleo: inventario con lotes y vencimientos, POS, caja
3. Multi-tenant
4. Tiempo real con SignalR (dashboard de ventas, alertas de stock y vencimientos)
5. Calidad e infra: xUnit, Docker, CI/CD, despliegue en Azure
6. Offline-first con sincronización
7. Presentación: README, diagramas, ADRs, demo desplegada, video corto

## Estado actual
Fase 1, paso 1 (estructura de la solución) completado: `Farmacia.slnx`, 4 proyectos con referencias, `Directory.Build.props`, `global.json`, endpoint `/health`. Modelo de dominio acordado (ver "Decisiones tomadas"). Implementado: base del dominio (`Entity`, `TenantEntity`, `DomainException`, `Guard`) `Product` + `SaleUnit`, `Category`, `Pharmacy`, `Supplier`, `Customer`, value object `Nit`, `Batch` + `StockMovement` + `FefoAllocator`, todos con tests. EF Core paso 1 hecho: `FarmaciaDbContext` y configuraciones de todas las entidades, Docker Compose de SQL Server, secretos locales. Siguiente: paso 2, migración inicial (revisarla juntos); paso 3, aplicarla y probar que un lote se guarda con su movimiento. Después, en el dominio: `Purchase`, `Sale` y caja.
