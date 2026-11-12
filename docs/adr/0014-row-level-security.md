# 0014. Row-Level Security de SQL Server como segunda línea de aislamiento entre farmacias

- **Estado:** Aceptada
- **Fecha:** 2026-11-12
- **Complementa a:** [0003](0003-pharmacyid-desde-el-inicio.md)

## Contexto

En la fase 3 el sistema pasa a atender varias farmacias en la misma base. Hasta ahora, el aislamiento tenía una sola línea de defensa (ADR 0003):

- el `PharmacyId` sale del JWT firmado;
- cada método de repositorio lo recibe y filtra por él.

Esto funciona, y los tests de la API lo prueban con dos farmacias. Pero depende de que **cada consulta nueva** se escriba bien: un reporte, una lectura con SQL a mano o un `Include` mal armado bastarían para mostrarle a una farmacia los datos de otra. En un software que guarda ventas, deudas de clientes y costos, ese error es el más grave posible.

## Decisión

Agregar una segunda línea de defensa **dentro de SQL Server**, con Row-Level Security (RLS):

- **Una función de predicado,** `tenancy.fn_TenantAccess(@PharmacyId)`. Deja pasar una fila cuando su `PharmacyId` es el de la sesión (`SESSION_CONTEXT(N'PharmacyId')`), o cuando la sesión salta el aislamiento (`SESSION_CONTEXT(N'TenantBypass') = 1`).
- **Una política, `tenancy.PharmacyIsolation`,** aplicada sobre las 23 tablas con `PharmacyId` y sobre `Pharmacies` (por su `Id`). Cada tabla lleva tres predicados:
  - **`FILTER`:** `SELECT`, `UPDATE` y `DELETE` solo ven las filas de la farmacia de la sesión, aunque la consulta no lleve `WHERE`.
  - **`BLOCK AFTER INSERT`:** no se puede insertar una fila de otra farmacia.
  - **`BLOCK AFTER UPDATE`:** no se puede pasar una fila a otra farmacia.
- **`TenantSessionInterceptor`** (un `DbConnectionInterceptor` de EF Core) fija las dos claves cada vez que se abre una conexión, **siempre las dos y como solo lectura** (`@read_only = 1`):
  - **Request con token:** `PharmacyId = <la del token>`, `TenantBypass = 0`.
  - **Request anónimo (login, refresh):** `PharmacyId = NULL`, `TenantBypass = 0`, así que no ve filas del negocio. **Falla cerrado.**
  - **Bypass:** `TenantBypass = 1`. Lo activa `TenantDatabaseScope.BypassIsolation()`, y solo lo usan el seeder de desarrollo, la preparación de los tests y, más adelante, el alta de farmacias. `SecurityConventionTests` falla si un controller o un caso de uso lo recibe.
- **Los repositorios siguen filtrando por `PharmacyId`.** RLS no reemplaza esa primera línea: la respalda. Además, el filtro explícito ayuda al optimizador a usar los índices `(PharmacyId, …)`.
- **Las tablas de Identity quedan fuera de RLS,** incluida `AspNetUsers` aunque tenga `PharmacyId`. El login busca al usuario por email **antes** de saber a qué farmacia pertenece, y el refresh solo conoce el hash de su token. Ponerlas bajo RLS obligaría a saltar el aislamiento en los endpoints anónimos más atacados y en cada refresh. El bypass dejaría de ser una excepción fácil de auditar. Los usuarios se protegen en el código, con tests de dos farmacias.
- **Las migraciones futuras** que creen una tabla con `PharmacyId` deben llamar a `migrationBuilder.AddTenantIsolation("Tabla")`. `RowLevelSecurityTests.EveryTenantTable_IsCoveredByThePolicy` falla si alguna queda fuera.

## Alternativas consideradas

- **Filtros globales de consulta de EF Core** (`HasQueryFilter`): son más simples y no dependen de SQL Server. Pero viven en la aplicación: no cubren SQL escrito a mano, se desactivan con `IgnoreQueryFilters()` y no impiden insertar filas de otra farmacia. RLS lo cubre todo, porque lo aplica la base misma.
- **Solo los repositorios (como antes):** funciona hoy, pero no protege contra el error humano en una consulta nueva.
- **Una base de datos (o un esquema) por farmacia:** es el aislamiento más fuerte, pero multiplica las migraciones, las conexiones y el costo en Azure. Para farmacias pequeñas, una base compartida con RLS es el punto medio habitual.
- **RLS también en `AspNetUsers`:** descartado por lo explicado arriba (el login no conoce la farmacia).

## Consecuencias

- **Una consulta que olvide filtrar no filtra datos:** devuelve solo la farmacia del token o, sin token, nada. Un error en una escritura con otra farmacia falla con el error 33504 de SQL Server (responde 500: es un defecto, no un caso de uso).
- **Una ida y vuelta más a la base cada vez que se abre una conexión,** para fijar el contexto. Es despreciable para una farmacia.
- **El pool de conexiones es seguro:** SQL Server limpia el `SESSION_CONTEXT` al reutilizar una conexión del pool (`sp_reset_connection`). Un test lo comprueba con un pool de una sola conexión.
- **La inyección SQL no puede cambiar el contexto,** porque las claves son de solo lectura. Hay un test por clave. Si la clave del bypass se dejara sin fijar, un SQL inyectado podría ponerla en 1: la mutación lo demuestra.
- **RLS no protege contra quien tenga credenciales de la base.** Puede abrir su propia conexión y fijar `TenantBypass = 1`, o desactivar la política si es dueño de la base. La protección contra eso es otra: los secretos en Key Vault y que la API use un usuario de base de datos sin permisos de DDL ni de `ALTER ANY SECURITY POLICY` (fase 5). Hoy la API se conecta como `sa`.
- **Las claves foráneas no pasan por RLS:** SQL Server las valida contra todas las filas. Una venta podría referenciar un producto de otra farmacia si el código lo permitiera. Eso lo siguen impidiendo los repositorios, que solo cargan entidades de la farmacia del token.
- **`SCHEMABINDING = ON`** impide cambiar el tipo de una columna `PharmacyId`, o borrar una tabla cubierta, sin antes quitarla de la política (`RemoveTenantIsolation`).
- **Una migración que modifique datos** (no solo el esquema) debe hacerlo con el bypass, o no verá ninguna fila.
