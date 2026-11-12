# Guía de desarrollo

## Requisitos

| Herramienta | Versión | Para qué |
|---|---|---|
| .NET SDK | 10.0.401 o superior dentro de 10.0 (`global.json`) | Backend |
| Node.js | 22 o superior | Frontend |
| Docker Desktop | Reciente | SQL Server de desarrollo y tests de integración |
| Editor | Visual Studio 2026, Rider o VS Code (con C# Dev Kit) | — |

## Configuración inicial (una sola vez)

### 1. Base de datos

```bash
cp .env.example .env
```

En `.env`, cambie `MSSQL_SA_PASSWORD`. SQL Server exige 8 caracteres o más, con mayúscula, minúscula, número y símbolo. Después:

```bash
docker compose up -d
docker compose ps          # debe decir "healthy" tras unos 20 segundos
```

Los datos quedan en el volumen `sqlserver-data`: sobreviven a `docker compose down`, pero no a `docker compose down -v`.

### 2. Secretos de la API

Los secretos van en [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), fuera del repositorio:

```bash
# Cadena de conexión (use la contraseña de su .env)
dotnet user-secrets set "ConnectionStrings:Farmacia" "Server=localhost,14330;Database=Farmacia;User Id=sa;Password=<su contraseña>;TrustServerCertificate=True" --project src/Farmacia.Api

# Clave de firma del JWT: 32 bytes aleatorios en base64
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
dotnet user-secrets set "Jwt:SigningKey" "<el resultado del comando anterior>" --project src/Farmacia.Api

# Opcional: licencia de MediatR Community (sin ella funciona, pero registra un aviso en el log)
dotnet user-secrets set "MediatR:LicenseKey" "<su clave>" --project src/Farmacia.Api
```

> Para generar la clave, use el comando de Node (o `RandomNumberGenerator.GetBytes` en PowerShell 7). En Windows PowerShell 5.1, `RandomNumberGenerator.Fill` no existe, y una línea mal copiada puede generar una clave de puros ceros. La API la rechaza al arrancar, pero conviene saberlo.

### 3. Esquema y datos de prueba

```bash
dotnet tool restore        # instala dotnet-ef (versión fijada en dotnet-tools.json)
dotnet ef database update --project src/Farmacia.Infrastructure --startup-project src/Farmacia.Api
```

Al arrancar en Development, `DevelopmentSeeder` crea los siguientes datos solo si no existen (es idempotente):

- la farmacia "Droguería de Desarrollo";
- los tres roles;
- un usuario por rol: `dueno@farmacia.dev`, `cajero@farmacia.dev` y `bodega@farmacia.dev`, y el administrador de la plataforma `admin@farmacia.dev` (sin farmacia), con la contraseña de `appsettings.Development.json`;
- el proveedor "Distribuidora de Desarrollo".

### 4. Frontend

```bash
cd web
npm install
```

## Correr la aplicación

```bash
dotnet run --project src/Farmacia.Api      # API en http://localhost:5228
cd web && npm run dev                      # web en http://localhost:5173
```

Abra http://localhost:5173. La documentación interactiva de la API está en http://localhost:5228/scalar (solo en Development); ahí puede iniciar sesión con el endpoint de login y probar el resto con el token.

Otra opción es `src/Farmacia.Api/Farmacia.Api.http`, que tiene ejemplos listos para ejecutar desde Visual Studio o VS Code (con la extensión REST Client).

## Depurar

- **Visual Studio:** abra `Farmacia.slnx`, elija `Farmacia.Api` como proyecto de inicio y presione F5. Se abre `/scalar` y puede poner breakpoints en controllers, handlers o el dominio.
- **VS Code:** con C# Dev Kit, use "Run and Debug" sobre `Farmacia.Api`.
- **Frontend:** use las herramientas del navegador. Con TanStack Query, la pestaña de red muestra cada llamada `/api/...`.

> Si `dotnet build` o `dotnet ef` fallan porque un archivo está en uso, la API sigue corriendo y bloquea sus DLL. Deténgala y repita el comando.

## Tests

```bash
dotnet test                                       # todo el backend
dotnet test tests/Farmacia.Domain.Tests           # un solo proyecto
dotnet test --filter CashPersistenceTests         # una sola clase
cd web && npm test                                # frontend
```

- **Docker debe estar encendido.** Los tests de Infrastructure y de la Api levantan su propio SQL Server desechable con Testcontainers y aplican las migraciones reales. No tocan su base de desarrollo.
- **Los tests de integración comparten un contenedor por ejecución.** Use datos únicos en cada test: NIT generados con `Nit.FromNumber`, números de factura aleatorios. Los tests de caja crean su propia farmacia, porque cada farmacia admite un solo turno abierto.
- **Row-Level Security también corre en los tests.** Para preparar o verificar datos directamente en la base, use `fixture.CreateContext()` (Infrastructure) o `api.CreateSeedingScope()` (Api): ven todas las farmacias. Las peticiones HTTP sí quedan limitadas a la farmacia de su token.
- **Un test debe poder fallar.** Para los tests importantes, rompa a propósito el código que protegen y confirme que fallan (prueba de mutación) antes de darlos por buenos.

## Convenciones

| Tema | Convención |
|---|---|
| Idioma | Código (clases, métodos, variables) y comentarios en inglés. Mensajes al usuario, documentación y commits en español. |
| Commits | [Conventional Commits](https://www.conventionalcommits.org/es/) en español: `feat: agregar cierre de caja`, `fix: …`, `docs: …`, `test: …` |
| Finales de línea | LF en todo el repo (`.gitattributes`). Git convierte automáticamente las migraciones que EF genera con CRLF. |
| Warnings | Son errores (`TreatWarningsAsErrors`): el código compila sin advertencias. |
| Nullable | Activado: `string?` significa que puede ser nulo y `string`, que no. |
| Dominio | Sin atributos de persistencia, sin `DateTime.Now` y sin dependencias. |
| Casos de uso | Un archivo por acción, con el command o query y su handler. Un solo `SaveChanges` por caso de uso. |
| Controllers | Delgados: solo envían commands o queries. Los Ids van en la URL y el body usa records `*Request` sin Id. Las respuestas 201 usan `CreatedResponse { id }`. |
| JSON | Los enums viajan como texto (`"Credit"`, no `2`). |
| Documentación | Se actualiza en el mismo commit que el cambio (ver [docs/README.md](README.md#cómo-mantener-esta-documentación)). |

## Problemas frecuentes

| Síntoma | Causa y solución |
|---|---|
| La API no arranca: "Connection string 'Farmacia' is missing" | Falta el user-secret de la cadena de conexión (paso 2). |
| La API no arranca: error en las opciones de `Jwt` | Falta la clave de firma, es muy corta o es degenerada. Genérela de nuevo (paso 2). |
| Error SQL 258 o timeout al conectar | El contenedor está detenido: `docker compose up -d`. Tiene `restart: unless-stopped`, así que Docker lo vuelve a levantar solo después de reiniciarse; si se creó antes de ese cambio, aplíquelo con `docker update --restart unless-stopped farmacia-sqlserver`. |
| `PendingModelChangesWarning` | Cambió el modelo y falta la migración ([cómo crearla](base-de-datos.md#migraciones)). |
| curl con tildes devuelve 400 en Git Bash | Git Bash envía el texto en otra codificación. Guarde el JSON en un archivo UTF-8 y use `--data-binary @archivo.json`. |
| `docker exec` con rutas falla en Git Bash | Git Bash reescribe las rutas. Anteponga `MSYS_NO_PATHCONV=1`. |
| `npm install` falla con ERESOLVE | No use `--legacy-peer-deps`: revise qué paquete genera el conflicto. |
