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
- Sin `CHECK (Quantity >= 0)` en `Batches` a propósito: la sincronización offline puede dejar stock negativo (ver arriba); la regla vive en el dominio.
- Trade-off conocido: SQL Server ordena `uniqueidentifier` por los últimos bytes, así que los Guid v7 no son secuenciales para el índice clustered y lo fragmentan. Despreciable a escala de una farmacia; si en multi-tenant crece mucho, usar PK nonclustered + índice clustered en otra columna.
- Tests de integración en `tests/Farmacia.Infrastructure.Tests` con Testcontainers: un SQL Server desechable por ejecución (collection fixture), con las migraciones reales aplicadas. Cada verificación usa un `DbContext` nuevo para leer de la base y no de la memoria de EF.
- CQRS con MediatR 14 (edición Community, gratuita bajo USD 5M de ingresos; la clave va en user-secrets como `MediatR:LicenseKey`). La 12.5 es la última Apache pero sin parches.
- Application accede a datos con repositorios por agregado (`IProductRepository`, ...) + `IUnitOfWork`; Application no referencia EF Core. Los repositorios siempre reciben el `PharmacyId` (aislamiento de tenant por construcción). Los handlers se testean con fakes en memoria (`tests/Farmacia.Application.Tests`).
- Application organizada por funcionalidad (`Products/CreateProduct`, `Products/GetProductById`). Errores de caso de uso: `ValidationException` (400), `NotFoundException` (404), `ConflictException` (409); reglas del modelo: `DomainException` (400). Un recurso de otra farmacia se responde como 404, nunca 403.
- El `PharmacyId` nunca viene del body: sale de `ICurrentPharmacy`, implementado por `HttpContextCurrentPharmacy`, que lee el claim `pharmacy_id` del JWT firmado (si falta, 401).
- Autorización: fallback policy que exige usuario autenticado en todo endpoint salvo `[AllowAnonymous]` (solo `/health`, login, refresh y logout; y OpenAPI en Development). Políticas con nombre en `Api/Authentication/Policies` (p. ej. `ManageCatalog` = Owner o StockKeeper) en vez de roles sueltos en los controllers. `SecurityConventionTests` falla si se quita la fallback policy o aparece un endpoint anónimo no esperado.
- Tests de la API en `tests/Farmacia.Api.Tests`: `WebApplicationFactory<Program>` + Testcontainers, ambiente `Testing` (sin seeder), dos farmacias para probar aislamiento por HTTP. La configuración se inyecta con `UseSetting` (con minimal hosting, `ConfigureAppConfiguration` llega tarde). Cliente con base `https://localhost` porque la cookie es Secure.
- API: controllers delgados que solo envían commands/queries con `ISender`. Errores como ProblemDetails (RFC 9457) vía `ProblemDetailsExceptionHandler`; lo inesperado es 500 sin detalles. `UnitOfWork` traduce las violaciones de índice único (SQL 2601/2627) y la concurrencia optimista a `ConflictException` (409).
- Auth: ASP.NET Core Identity (solo core: usuarios, roles, hash, bloqueo) en el mismo `FarmaciaDbContext` (hereda de `IdentityDbContext`), para poder crear farmacia + dueño en una transacción. `ApplicationUser` vive en Infrastructure y pertenece a una sola farmacia (`PharmacyId`). Login con email (`UserName` = email, porque `UserName` es único entre todos los tenants). Contraseña: mínimo 8, mayúscula, minúscula y número, sin símbolo. Bloqueo: 5 intentos fallidos, 15 minutos. Roles en `Application/Authorization/Roles`: Owner, Cashier, StockKeeper.
- JWT propio emitido por la API (no `MapIdentityApi`, que no usa JWT): HS256, clave de 32+ bytes en user-secrets (`Jwt:SigningKey`; Key Vault en producción), validada al arrancar (longitud y no degenerada). Access token de 15 minutos (ClockSkew 30 s, algoritmo fijado a HS256). Claims: `sub`, `email`, `name`, `pharmacy_id`, `role`. Sin registro público: el dueño crea los usuarios.
- Refresh token: 64 bytes aleatorios, guardado como SHA-256 (`RefreshTokens`), 7 días, rotado en cada uso. Viaja solo en la cookie `farmacia_refresh` (HttpOnly, Secure, SameSite=Strict, Path=/api/auth); el access token va en el body y el cliente lo guarda solo en memoria. Reutilizar un token ya rotado revoca toda su familia (todos los tokens del mismo login). Logout revoca la familia.
- Login: mismo mensaje para email inexistente y contraseña incorrecta, y verificación de un hash falso cuando el email no existe (para no revelar emails por el tiempo de respuesta). El login no pasa por MediatR: `IAuthService` (Application) implementado por `AuthService` (Infrastructure). `TimeProvider` inyectado para testear la expiración.
- Desarrollo: `DevelopmentSeeder` crea (idempotente) la farmacia, los roles y un usuario por rol (`dueno@`, `cajero@`, `bodega@farmacia.dev`) con la contraseña de `appsettings.Development.json`; solo existen en la base local.
- Frontend (`web/`): Vite 8 + React 19 + TypeScript 6, npm, oxlint. React Router 8 (modo datos, `createBrowserRouter`; `RouterProvider` se importa de `react-router/dom`, ya no existe `react-router-dom`). TanStack Query para datos del servidor. Tailwind 4 + shadcn/ui (preset `nova` sobre Base UI; `cn` viene del paquete npm `cn`). Formularios con react-hook-form + zod 4 (`@hookform/resolvers` 5: la 6 tiene un conflicto de peer deps opcionales; no usar `--legacy-peer-deps`). Alias `@/` -> `src/`. Código organizado por funcionalidad (`src/features/auth`, `src/lib/api`).
- Cliente HTTP propio sobre `fetch` (`src/lib/api/http.ts`): access token solo en memoria; ante un 401 hace un único refresh compartido (single-flight) y reintenta; un error de red no cierra la sesión (status 0). La sesión se publica con `onSessionChange` y `AuthProvider` la refleja; al cerrar sesión se limpia la caché de TanStack Query.
- Sesión en el frontend: `AuthProvider` arranca en `restoring` y llama a `/api/auth/refresh` para recuperar la sesión tras recargar. Rutas: `RequireAuth` (layout route; redirige al login recordando la página, validada con `safeRedirectPath` contra open redirect) y `RequireRole` (muestra "No tiene permiso"). `permissions.ts` replica las políticas de la API solo para la experiencia de usuario; la API es quien las hace cumplir.
- Tests del frontend con Vitest (`npm test` en `web/`): cliente HTTP con `fetch` falso (refresh único con 10 401 simultáneos, error de red no cierra sesión, etc.), redirección segura y permisos.
- Desarrollo del frontend: `npm run dev` en `web/` (puerto 5173) con proxy de `/api` y `/health` a la API en 5228, así el navegador ve un solo origen y la cookie SameSite=Strict funciona sin CORS.
- Documentación interactiva de la API: Scalar en `/scalar` (solo Development, `AllowAnonymous`), sobre el OpenAPI nativo de .NET (`/openapi/v1.json`) con el esquema Bearer JWT declarado (`Api/OpenApi/OpenApiExtensions.cs`, Microsoft.OpenApi v2). F5 en Visual Studio abre `/scalar`.
- Migraciones en `Infrastructure/Persistence/Migrations`. Finales de línea LF en todo el repo (`.gitattributes`).
- `System.Security.Cryptography.Pkcs` está fijado en Infrastructure porque la versión que trae SqlClient tiene vulnerabilidades conocidas; quitarlo cuando SqlClient la actualice.
- Un lote se puede vender hasta su fecha de vencimiento inclusive; vencido no se vende, solo se da de baja. El dominio no usa `DateTime.Now`: recibe las fechas con la hora local de Colombia (UTC-5).
- Compras (fase 2): toda línea de factura trae lote y vencimiento; hay bonificaciones, descuento por línea y descuento por factura; se compra de contado y a crédito (cuentas por pagar en un paso posterior); IVA mixto (0%, 5%, 19%).
- `Purchase` es un agregado con flujo Borrador -> Confirmada (un corte de luz no hace perder la digitación; el inventario solo cambia al confirmar). Confirmada no se edita: se anula o se ajusta. Factura única por proveedor + número.
- Al confirmar, `Purchase.Confirm` crea un `Batch` por línea con el costo real por unidad base: (cantidad × costo − descuento de línea − parte del descuento de factura [+ IVA si es costo]) / ((cantidad + bonificación) × factor). El descuento de factura se reparte en proporción al valor neto de cada línea (la última recibe el residuo del redondeo) y reduce la base del IVA. La línea guarda una copia del factor de conversión.
- IVA en el costo: configurable por farmacia (`Pharmacy.IsVatResponsible`, por defecto true = el IVA de compras es descontable y no va al costo). Pendiente confirmarlo con el contador.
- Al confirmar se digita el total de la factura impresa; si difiere del calculado en más de $1, no se confirma.
- Casos de uso de compras, uno por acción (cada línea se guarda al agregarla): `CreatePurchaseDraft`, `UpdatePurchaseHeader`, `AddPurchaseItem`, `RemovePurchaseItem`, `SetPurchaseInvoiceDiscount`, `ConfirmPurchase`, `GetPurchaseById`, `ListPurchases`. Confirmar guarda compra + lotes + kardex en un solo `SaveChanges`; `RowVersion` en `Purchase` evita que un doble clic reciba la mercancía dos veces (409). Dueño y bodeguero pueden confirmar (cambiable en `Policies`). `PurchaseReader` arma los DTOs con nombres de producto y proveedor.
- La fecha de recepción la pone el servidor: `IClock` (Application) implementado por `ColombiaClock` (UTC-5 fijo; Colombia no tiene horario de verano).
- `Nit.FromNumber` calcula el dígito de verificación. Tests con contenedor compartido: generar NIT únicos con `Nit.FromNumber` (el índice único de proveedor por NIT rechaza repetidos).
- Venta fraccionada: el stock se guarda en la unidad base (p. ej. tableta); cada `SaleUnit` (caja, blíster, unidad) tiene su factor de conversión y su propio precio.

## Fases
1. Base: estructura, dominio inicial, EF Core + migraciones, auth JWT con roles (dueño, cajero, bodeguero), React con login  <- COMPLETA
2. Núcleo: inventario con lotes y vencimientos, POS, caja  <- SIGUIENTE
3. Multi-tenant
4. Tiempo real con SignalR (dashboard de ventas, alertas de stock y vencimientos)
5. Calidad e infra: xUnit, Docker, CI/CD, despliegue en Azure
6. Offline-first con sincronización
7. Presentación: README, diagramas, ADRs, demo desplegada, video corto

## Estado actual
Fase 1, paso 1 (estructura de la solución) completado: `Farmacia.slnx`, 4 proyectos con referencias, `Directory.Build.props`, `global.json`, endpoint `/health`. Modelo de dominio acordado (ver "Decisiones tomadas"). Implementado: base del dominio (`Entity`, `TenantEntity`, `DomainException`, `Guard`) `Product` + `SaleUnit`, `Category`, `Pharmacy`, `Supplier`, `Customer`, value object `Nit`, `Batch` + `StockMovement` + `FefoAllocator`, todos con tests. EF Core paso 1 hecho: `FarmaciaDbContext` y configuraciones de todas las entidades, Docker Compose de SQL Server, secretos locales. EF Core completo: migración `InitialCreate` aplicada a la base de desarrollo y tests de integración (lote + kardex en la misma transacción, offset de Colombia, concurrencia con `RowVersion`, inserción de `SaleUnit`, `Nit` en la base, índice único de cliente). Primer caso de uso de punta a punta completo: `POST /api/products` y `GET /api/products/{id}` probados contra la base real (201, 200, 400, 404, 409); ejemplos en `src/Farmacia.Api/Farmacia.Api.http`. Auth en curso: C1 hecho (Identity, migración `AddIdentity`, roles y usuarios de desarrollo). C2 hecho: `POST /api/auth/login`, `/refresh`, `/logout` y `GET /api/auth/me`, migración `AddRefreshTokens`, tests de `AuthService` contra SQL Server real. C3 hecho: endpoints protegidos (fallback policy + `ManageCatalog`), `ICurrentPharmacy` desde el token (se eliminó `DevelopmentCurrentPharmacy`), tests de la API (401, 403, 404 entre farmacias, cookie, convenciones de seguridad). React con login completo: D1 (proyecto `web/`, Tailwind + shadcn, proxy, cliente HTTP con refresh único, pantalla de login) y D2 (restaurar la sesión al recargar, `RequireAuth`/`RequireRole`, layout con navegación por permisos, página de catálogo provisional, tests con Vitest). FASE 1 COMPLETA. Extra: Scalar en `/scalar`. Fase 2 en curso, compras: E1 hecho (dominio `Purchase`/`PurchaseItem` con borrador/confirmación, costo real por unidad base, reparto del descuento de factura, verificación del total; `VatRate`; `Pharmacy.IsVatResponsible` con migración `AddPharmacyVatResponsibility`, cuyo `defaultValue: true` se editó a mano). E2a hecho (8 casos de uso, repositorios, migración `AddPurchases`, tests unitarios y de integración: transacción única, doble confirmación, índice único). Siguiente: E2b (endpoints HTTP de compras + tests de API); E3 (pantalla de recepción; requiere listar proveedores y productos). Después: ventas (`Sale`/`Payment`) y caja (`CashSession`).
