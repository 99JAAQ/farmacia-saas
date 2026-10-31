# 0011. Catálogo: los productos se desactivan, no se borran, y solo se guarda el precio actual

- **Estado:** Aceptada
- **Fecha:** 2026-10-31

## Contexto

El catálogo cambia: llegan productos nuevos, se descontinúan otros, cambian precios y presentaciones (una caja de 30 pasa a ser de 20). Pero los productos ya tienen historia: lotes, ventas, devoluciones y kardex que los referencian.

## Decisión

- **Un producto descontinuado se desactiva (`Product.IsActive = false`), nunca se borra.** Inactivo, no se vende (lo rechaza `Sale.Register`) ni se compra; se puede reactivar. Su historia queda intacta.
- **Se guarda solo el precio actual de cada presentación.** Cada venta copia el precio del momento (`SaleItem.UnitPrice`), así que un cambio de precio aplica solo a las ventas siguientes y los tiquetes viejos no cambian.
- **Cambiar el factor de una presentación está permitido.** El stock está en unidades base y cada venta copió su factor, así que no altera el inventario ni las ventas pasadas.
- **Por ahora no se eliminan presentaciones.** Las ventas y compras las referencian; si una sobra, se le cambia el nombre o el precio. Desactivar presentaciones queda para cuando haga falta.

## Alternativas consideradas

- **Borrar si no tiene movimientos:** es más limpio para un error de digitación, pero son dos caminos distintos que explicar, y desactivar ya resuelve el caso.
- **Historial de precios con fecha y usuario:** es útil para auditar, pero hoy nadie lo pide, y las ventas ya guardan el precio que se cobró. Se puede agregar después sin cambiar lo que existe.

## Consecuencias

- **La migración `AddProductIsActive` se editó a mano:** EF ponía `defaultValue: false`, lo que habría desactivado todos los productos existentes.
- **Las búsquedas del POS y de compras deben ignorar los inactivos**, y el catálogo debe poder mostrarlos para reactivarlos.
- **Para saber cuándo cambió un precio** habrá que mirar las ventas, hasta que exista un historial.
