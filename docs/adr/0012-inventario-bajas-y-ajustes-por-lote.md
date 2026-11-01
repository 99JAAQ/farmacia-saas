# 0012. Inventario: bajas y ajustes por lote, y un kardex que dice quién hizo cada movimiento

- **Estado:** Aceptada
- **Fecha:** 2026-10-31

## Contexto

El catálogo ya muestra las unidades en lotes vencidos, pero no hay cómo sacarlas del inventario. Además, el conteo físico casi nunca coincide del todo con el sistema: hay unidades dañadas, faltantes y errores de digitación. Las bajas y los ajustes cambian el valor del inventario y pueden tapar un faltante, así que el dueño necesita poder revisarlos.

## Decisión

- **La baja de un lote vencido y el ajuste por conteo se hacen por lote**, que es donde vive el stock: `Batch.WriteOffExpired` (todo el lote, solo si ya venció) y `Batch.AdjustToCount` (deja el lote en lo contado, con motivo obligatorio). Ya existían en el dominio desde la fase 1.
- **Los hacen el dueño y el bodeguero**, con una política nueva, `ManageInventory`.
- **Cada movimiento del kardex guarda quién lo causó** (`StockMovement.PerformedBy`): quién recibió la compra, quién vendió, quién reingresó una devolución, quién dio de baja o ajustó. Las operaciones de `Batch` reciben el usuario, y la compra, la venta y la devolución pasan el suyo.

## Alternativas consideradas

- **Solo el dueño hace bajas y ajustes:** da más control, pero el bodeguero dependería del dueño para el trabajo diario. El kardex con usuario y motivo deja todo a la vista.
- **Una toma de inventario completa** (una sesión de conteo de toda la bodega que aplica todas las diferencias al cerrarse): es más formal, pero mucho más trabajo. Se puede construir después sobre el ajuste por lote.
- **Kardex solo con la referencia** (la venta o la compra, que ya tienen su usuario): las bajas y los ajustes, justamente los que más se auditan, quedarían sin saber quién.

## Consecuencias

- **`StockMovements.PerformedBy` admite nulos:** los movimientos anteriores a este cambio no tienen usuario.
- **Es una FK a `AspNetUsers` sin cascada.** Los tests de integración que guardan movimientos deben usar un usuario real.
- **El parámetro es opcional en `Batch`** para no cambiar los tests existentes, pero todos los llamadores del dominio (compra, venta, devolución) lo pasan, y hay tests que lo verifican.
