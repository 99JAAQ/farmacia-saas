# Estructura de carpetas

## Raíz

```
Farmacias_Saas/
├── Farmacia.slnx            → solución .NET (formato nuevo .slnx)
├── Directory.Build.props    → configuración común: net10.0, nullable, warnings como errores
├── global.json              → fija la versión del SDK de .NET
├── dotnet-tools.json        → herramientas locales (dotnet-ef); se instalan con `dotnet tool restore`
├── docker-compose.yml       → SQL Server de desarrollo (puerto 14330)
├── .env.example             → plantilla de la contraseña de SQL Server (.env está ignorado)
├── .gitattributes           → finales de línea LF en todo el repo
├── CLAUDE.md                → contexto y decisiones para el asistente de IA
├── docs/                    → esta documentación
├── src/                     → código del backend (4 proyectos)
├── tests/                   → tests del backend (4 proyectos, uno por capa)
└── web/                     → frontend React
```

## Backend: `src/`

### `Farmacia.Domain`: el negocio

```
Farmacia.Domain/
├── Common/          → Entity, TenantEntity, DomainException, Guard, Money, Nit, VatRate
├── Pharmacies/      → Pharmacy (el tenant)
├── Products/        → Product, SaleUnit (presentación), Category
├── Inventory/       → Batch (lote), StockMovement (kardex), FefoAllocator
├── Suppliers/       → Supplier
├── Customers/       → Customer, DocumentType (códigos DIAN)
├── Purchases/       → Purchase, PurchaseItem, estados y forma de pago
└── Cash/            → CashSession (turno), CashMovement, enums
```

Una carpeta por área del negocio. Cada **agregado** (`Purchase`, `CashSession`, `Batch`, `Product`…) tiene una raíz con métodos que protegen sus reglas. Sus hijos (`PurchaseItem`, `CashMovement`, `SaleUnit`) solo se crean a través de la raíz (tienen constructor `internal`).

### `Farmacia.Application`: los casos de uso

```
Farmacia.Application/
├── Abstractions/
│   ├── ICurrentPharmacy.cs, ICurrentUser.cs, IClock.cs   → quién pide y qué hora es
│   ├── Authentication/IAuthService.cs                    → login (no pasa por MediatR)
│   └── Persistence/                                      → un repositorio por agregado + IUnitOfWork
├── Authorization/   → nombres de roles y de claims del JWT
├── Common/Exceptions/ → ValidationException, NotFoundException, ConflictException, ForbiddenException…
├── Products/        → CreateProduct/, GetProductById/, SearchProducts/, ProductDto
├── Suppliers/       → CreateSupplier/, GetSupplierById/, SearchSuppliers/
├── Purchases/       → CreatePurchaseDraft/, AddPurchaseItem/, ConfirmPurchase/, …, PurchaseReader
├── Cash/            → OpenCashSession/, RecordCashMovement/, CloseCashSession/, …, CashSessionReader
└── DependencyInjection.cs → AddApplication(): MediatR y los lectores
```

Una carpeta por caso de uso, con el command (o query) y su handler en el mismo archivo. Los `*Reader` arman los DTOs de lectura, que necesitan datos de varios agregados (nombres de producto, de proveedor, de usuario).

### `Farmacia.Infrastructure`: los detalles técnicos

```
Farmacia.Infrastructure/
├── Persistence/
│   ├── FarmaciaDbContext.cs   → DbContext (incluye las tablas de Identity) y guardado del kardex
│   ├── Configurations/        → Fluent API: una clase por entidad (tablas, índices, precisión, FK)
│   ├── Migrations/            → migraciones de EF Core (generadas; no se editan salvo casos documentados)
│   ├── Repositories/          → implementaciones de los repositorios de Application
│   ├── Converters/            → NitConverter (Nit ↔ texto)
│   ├── SearchPattern.cs       → búsqueda sin tildes ni mayúsculas
│   └── UnitOfWork.cs          → SaveChanges + traducción de errores SQL a ConflictException
├── Identity/                  → ApplicationUser, AuthService, AccessTokenIssuer, RefreshToken, JwtOptions
├── Time/ColombiaClock.cs      → IClock con hora de Colombia
└── DependencyInjection.cs     → AddInfrastructure(): DbContext, repositorios, Identity, JWT
```

### `Farmacia.Api`: la puerta HTTP

```
Farmacia.Api/
├── Program.cs                 → arranque: servicios, middleware, JSON con enums como texto
├── Controllers/               → un controller por recurso (Auth, Products, Suppliers, Purchases, CashSessions)
├── Authentication/            → JWT, Policies (quién puede qué), ICurrentPharmacy/ICurrentUser desde el token
├── ErrorHandling/             → excepciones → ProblemDetails
├── OpenApi/                   → documento OpenAPI + Scalar (/scalar, solo en Development)
├── Development/DevelopmentSeeder.cs → datos de prueba (solo en Development)
├── Farmacia.Api.http          → ejemplos de peticiones para VS Code / Visual Studio
└── appsettings*.json          → configuración sin secretos (los secretos van en user-secrets)
```

## Tests del backend: `tests/`

| Proyecto | Qué prueba | Contra qué |
|---|---|---|
| `Farmacia.Domain.Tests` | Reglas del modelo (NIT, FEFO, costos de compra, cierre de caja) | Nada: objetos en memoria |
| `Farmacia.Application.Tests` | Casos de uso: permisos, 404 entre farmacias, conteo a ciegas | Fakes en memoria (`Fakes/Fakes.cs`) |
| `Farmacia.Infrastructure.Tests` | Transacciones, índices únicos, concurrencia, Identity | SQL Server real en un contenedor desechable (Testcontainers) |
| `Farmacia.Api.Tests` | Flujos HTTP completos, 401/403/404, cookie, convenciones de seguridad | La API entera en memoria (`WebApplicationFactory`) + SQL Server en contenedor |

La carpeta de cada proyecto de tests replica la del proyecto que prueba (`Domain.Tests/Purchases/PurchaseTests.cs` prueba `Domain/Purchases/Purchase.cs`).

## Frontend: `web/`

```
web/
├── vite.config.ts           → alias @/, proxy a la API, configuración de Vitest (TZ=America/Bogota)
├── components.json          → configuración de shadcn/ui
└── src/
    ├── main.tsx, App.tsx    → arranque: QueryClient, AuthProvider, RouterProvider
    ├── index.css            → tema: tokens de color (teal + success/warning), modo oscuro del sistema
    ├── router.tsx           → todas las rutas y sus permisos
    ├── components/
    │   ├── AppLayout.tsx    → barra lateral (o superior en pantallas angostas) y menú según permisos
    │   ├── navigation.ts    → las secciones de la app: ruta, nombre, ícono y política
    │   ├── PrintDialog.tsx  → diálogo con Imprimir para el tiquete y el comprobante (80 mm)
    │   ├── Logo.tsx         → marca (el mismo dibujo que public/favicon.svg)
    │   └── ui/              → componentes de shadcn/ui (generados por la CLI; se editan poco)
    ├── features/            → una carpeta por funcionalidad
    │   ├── auth/            → login, sesión, RequireAuth/RequireRole, permissions.ts
    │   ├── products/        → api, buscador con lector de código de barras
    │   ├── suppliers/       → api, buscador y creación de proveedor
    │   ├── purchases/       → pantallas de compras
    │   ├── cash/            → pantalla de caja
    │   ├── sales/           → POS: carrito (lógica pura + localStorage), pagos, tiquete imprimible
    │   ├── returns/         → devoluciones: borrador (lógica pura), pantalla, comprobante
    │   ├── customers/       → clientes y fiado: búsqueda, deudores, cupo, movimientos
    │   ├── catalog/         → catálogo: lista con stock, ficha del producto, presentaciones, lotes
    │   └── inventory/       → bajas de lotes vencidos, ajustes por conteo y kardex
    ├── lib/
    │   ├── api/http.ts      → cliente HTTP (token en memoria, refresh único)
    │   ├── format.ts        → dinero, fechas y horas en formato colombiano
    │   └── nit.ts           → dígito de verificación del NIT
    └── pages/               → páginas simples sin funcionalidad propia (inicio)
```

Dentro de cada `features/<x>/`: `api.ts` (tipos y llamadas HTTP), `labels.ts` (textos de los enums) y los componentes. Los tests van junto al archivo que prueban (`scan.ts` → `scan.test.ts`).

## Receta: agregar una funcionalidad de punta a punta

Ejemplo: "anular una compra". Siga este orden; cada paso tiene sus tests antes de pasar al siguiente.

1. **Dominio**: agregue el método `Purchase.Void(...)` con sus reglas y estados, y sus tests en `Domain.Tests`.
2. **Caso de uso**: cree `Application/Purchases/VoidPurchase/VoidPurchase.cs` con el command y el handler, y sus tests con fakes en `Application.Tests`.
3. **Persistencia** (si cambió el modelo): ajuste la configuración en `Infrastructure/Persistence/Configurations` y genere la migración ([cómo](base-de-datos.md#migraciones)). Si hay índices o transacciones nuevas, agregue tests en `Infrastructure.Tests`.
4. **Endpoint**: agregue la acción al controller, con su política y sus `ProducesResponseType`, y un test en `Api.Tests`. Si crea un endpoint anónimo, actualice a propósito `SecurityConventionTests`.
5. **Frontend**: agregue la función en `features/purchases/api.ts`, el componente y, si hay un permiso nuevo, actualice `permissions.ts`. Este debe reflejar `Policies.cs`.
6. **Documentación**: actualice el documento de `docs/` que corresponda.
