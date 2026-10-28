# 0007. Stock por lote como agregado aparte, con kardex y FEFO

- **Estado:** Aceptada
- **Fecha:** 2026-10-22

## Contexto

En una farmacia, el mismo producto llega en lotes distintos, cada uno con su vencimiento y su costo. La regulación sanitaria y el control de pérdidas exigen saber de qué lote salió cada unidad, vender primero lo que vence primero y no vender lotes vencidos. La farmacia tiene un solo inventario (no hay bodegas).

## Decisión

- **`Batch` (lote) es un agregado aparte de `Product`**, con la cantidad en unidades base y `RowVersion`.
- **Cada cambio de cantidad genera un `StockMovement` (kardex)**, que se guarda en la misma transacción que el lote: `Batch` lo deja en `PendingMovements` y `FarmaciaDbContext` lo guarda en el mismo `SaveChanges`.
- **`FefoAllocator` es un servicio de dominio que solo planifica** de qué lotes sale una venta. Quien vende aplica `Batch.Sell`.
- **Sin `Warehouse`:** hay un solo inventario.
- **Stock nunca negativo en línea**, pero sin un `CHECK` en la base, porque la sincronización offline podrá dejarlo negativo y marcarlo para revisar.

## Alternativas consideradas

- **El stock como un número en `Product`:** es simple, pero no permite FEFO, ni controlar vencimientos, ni conocer el costo real por lote.
- **Los lotes dentro del agregado `Product`:** cargar un producto traería todos sus lotes históricos, y dos ventas del mismo producto (aunque de lotes distintos) chocarían por concurrencia.
- **Calcular el stock sumando el kardex:** se evita guardar dos veces la información, pero cada consulta de stock recorrería todo el historial.

## Consecuencias

- **Cada unidad tiene trazabilidad**, y el margen de cada venta es real (costo del lote vendido).
- **La concurrencia es por lote:** dos cajas que venden la última unidad del mismo lote producen un 409 en la segunda.
- **La cantidad del lote y el kardex deben quedar siempre consistentes.** Eso está garantizado porque solo `Batch` cambia la cantidad, y siempre deja su movimiento.
- **Un mismo producto se puede vender saliendo de dos lotes**, y la venta deberá registrar ambos.
