# 0018. Los avisos de tiempo real salen de lo que se guardó, no de cada caso de uso

- **Estado:** Aceptada
- **Fecha:** 2026-11-12
- **Modifica a:** [0017](0017-tiempo-real-con-avisos-delgados.md)

## Contexto

El ADR 0017 decidió que cada caso de uso agregara sus avisos a `IRealtimeNotifications`. Al extenderlo a todos, resultó que **27 casos de uso escriben en la base**. Cada uno tendría que recordar qué temas avisar, y un olvido no se nota: la pantalla afectada simplemente queda desactualizada hasta que se recarga, sin errores ni tests que fallen.

## Decisión

- **Un interceptor de EF Core (`RealtimeChangesInterceptor`) anota los avisos a partir de lo que cada `SaveChanges` escribió.** Antes de guardar, mira las entidades agregadas, modificadas o borradas. Si el guardado tuvo éxito, las traduce a avisos en el búfer del request. Si falló, no anota nada.
- **Lo que no cambia del ADR 0017:** `PublishRealtimeChangesBehavior` sigue enviando el búfer solo si el caso de uso terminó bien. Así, un `SaveChanges` exitoso dentro de una transacción que después se deshace tampoco avisa.
- **`RealtimeChangeMap` dice qué aviso produce cada entidad,** con el Id que le sirve al navegador:
  - `Batch` → `stock` del producto;
  - `SaleUnit` → `catalog` del producto;
  - `CreditAccount` → `credit` del cliente;
  - `CashMovement` → `cash` del turno.

  Las entidades hijas cuyo padre ya avisa (`SaleItem`, `StockMovement`, …) y la contabilidad interna (`DocumentCounter`, `RefreshToken`) están en el mapa explícitamente como "nada".
- **Un test recorre el modelo de EF y falla si una entidad no está en el mapa:** agregar una entidad obliga a decidir qué avisa.
- **Los casos de uso no cambian,** y `IRealtimeNotifications.Add` sigue disponible para un aviso que no venga de la base.

## Alternativas consideradas

- **Avisos explícitos en cada caso de uso (lo del ADR 0017):** son más visibles al leer el handler, pero dependen de acordarse, y el olvido es silencioso.
- **Eventos de dominio:** el dominio diría qué pasó ("venta registrada") en vez de qué tabla cambió. Es más expresivo, pero exige agregar eventos a todos los agregados para algo que hoy solo sirve para refrescar pantallas. Si en la fase 6 la sincronización los necesita, los avisos pueden derivarse de ellos.

## Consecuencias

- **Lo que no pasa por `SaveChanges` no avisa,** por ejemplo `ExecuteUpdate`. Hoy solo se usa para revocar refresh tokens, que no se ven en ninguna pantalla.
- **Abrir un turno, crear un cliente o editar un producto también avisan.** Un test que escuche desde antes recibirá esos avisos además del que busca.
- **Los avisos repetidos en un request se envían una vez** (el búfer deduplica).
