# 0002. Ids Guid v7 generados en el dominio

- **Estado:** Aceptada
- **Fecha:** 2026-10-22

## Contexto

La farmacia tiene cortes de energía e internet inestable. En la fase 6, el POS debe crear ventas sin conexión y sincronizarlas después, y dos equipos sin conexión no pueden generar el mismo Id.

## Decisión

Todas las entidades usan un `Guid` versión 7 (`Guid.CreateVersion7()`), generado en el constructor de `Entity`, no por la base de datos. EF Core los configura con `ValueGeneratedNever`.

## Alternativas consideradas

- **`int IDENTITY` de SQL Server:** solo la base de datos asigna el número, así que un equipo sin conexión no puede crear registros.
- **Guid v4 (aleatorio):** sirve igual sin conexión, pero no tiene orden temporal. El v7 empieza con un timestamp, así que ordenar por Id es aproximadamente ordenar por fecha de creación.

## Consecuencias

- **Cualquier equipo crea registros sin coordinarse** con el servidor.
- **El Id se conoce antes de guardar**, así que un agregado puede referenciar a sus hijos sin esperar a la base de datos.
- **Hace falta `ValueGeneratedNever`.** Sin él, EF trataría a un hijo nuevo que ya trae Id (como un `SaleUnit`) como existente, y haría un UPDATE en vez de un INSERT. `FarmaciaDbContext` lo configura para todas las entidades.
- **Hay un costo conocido:** SQL Server ordena los `uniqueidentifier` por sus últimos bytes, así que los Guid v7 no son secuenciales para el índice clustered y lo fragmentan. A escala de una farmacia es despreciable. Si en multi-tenant crece mucho, se usaría una PK *nonclustered* más un índice clustered sobre otra columna.
