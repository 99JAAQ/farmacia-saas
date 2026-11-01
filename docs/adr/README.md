# Decisiones de arquitectura (ADR)

Un ADR (*Architecture Decision Record*) registra una decisión importante: el contexto en que se tomó, qué se decidió, qué alternativas se descartaron y qué consecuencias trae. Sirve para que alguien que llega después entienda **por qué** el código es así antes de cambiarlo.

| # | Decisión | Estado |
|---|---|---|
| [0001](0001-clean-architecture-y-cqrs.md) | Clean Architecture con CQRS (MediatR) y repositorios por agregado | Aceptada |
| [0002](0002-ids-guid-v7-en-el-dominio.md) | Ids Guid v7 generados en el dominio | Aceptada |
| [0003](0003-pharmacyid-desde-el-inicio.md) | `PharmacyId` en toda entidad desde el inicio, tomado del JWT | Aceptada |
| [0004](0004-mediatr-community.md) | MediatR 14 en su edición Community | Aceptada |
| [0005](0005-sql-server-ef-core-testcontainers.md) | SQL Server en Docker, EF Core con Fluent API y tests con Testcontainers | Aceptada |
| [0006](0006-jwt-propio-y-refresh-en-cookie.md) | JWT propio con refresh token rotado en cookie HttpOnly | Aceptada |
| [0007](0007-lotes-kardex-y-fefo.md) | Stock por lote como agregado aparte, con kardex y FEFO | Aceptada |
| [0008](0008-caja-por-turnos-con-conteo-a-ciegas.md) | Caja por turnos con un solo turno abierto y conteo a ciegas | Aceptada |
| [0009](0009-devoluciones-como-documento-aparte.md) | Devoluciones como documento aparte, con reintegro en efectivo y reingreso al lote | Aceptada; modificada por 0010 |
| [0010](0010-fiado-con-cuenta-cupo-y-abonos.md) | Fiado con cuenta por cliente, cupo y abonos al saldo | Aceptada |
| [0011](0011-catalogo-desactivar-y-precio-actual.md) | Catálogo: los productos se desactivan, no se borran, y solo se guarda el precio actual | Aceptada |
| [0012](0012-inventario-bajas-y-ajustes-por-lote.md) | Inventario: bajas y ajustes por lote, y un kardex que dice quién hizo cada movimiento | Aceptada |

## Cómo escribir uno nuevo

1. Copie [la plantilla](plantilla.md) como `NNNN-titulo-corto.md`, con el número siguiente.
2. Explique el contexto como para alguien que no estuvo en la conversación.
3. Agréguelo a la tabla de arriba en el mismo commit del cambio que documenta.
4. **No reescriba un ADR aceptado.** Si la decisión cambia, cree uno nuevo que diga "Reemplaza a NNNN" y cambie el estado del anterior a "Reemplazada por NNNN".
