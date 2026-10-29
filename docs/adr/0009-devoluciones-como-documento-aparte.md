# 0009. Devoluciones como documento aparte, con reintegro en efectivo y reingreso al lote

- **Estado:** Aceptada
- **Fecha:** 2026-10-29

## Contexto

En el mostrador, un cliente devuelve a veces una venta completa o parte de ella (1 de 3 cajas), el mismo día o días después. El dinero se le devuelve, y el producto vuelve al inventario o, si se abrió, se dañó o salió de la cadena de frío, ya no se puede vender. Para el dueño importan tres cosas: no devolver dos veces lo mismo, que la caja cuadre y que el kardex explique cada unidad.

## Decisión

- **La devolución es su propio agregado, `SaleReturn`, con su propio consecutivo.** La venta (`Sale`) nunca se modifica: un tiquete es un documento cerrado. Lo que queda por devolver se calcula como lo vendido menos las devoluciones anteriores de ese tiquete.
- **Se devuelve por línea y cantidad, en la presentación vendida** (cajas si se vendieron cajas).
- **Por cada línea se elige: vuelve al inventario o se descarta.** Lo que vuelve entra al mismo lote del que salió, con un movimiento `CustomerReturn` en el kardex. Si la línea salió de varios lotes, se llena primero el que vence más tarde (el inverso de FEFO), así que devolver todo deja los lotes como estaban antes de la venta. A un lote vencido no se reingresa: hay que descartar. Lo descartado no toca el stock.
- **El reintegro es siempre en efectivo, por el precio pagado** (copiado del tiquete, IVA incluido). El dinero sale **del turno abierto de quien hace la devolución**, no del turno de la venta.
- **La hace el cajero con su turno abierto, con motivo obligatorio.** Las devoluciones se ven en el cierre de caja.
- **Un cambio de producto es una devolución más una venta nueva.**

## Alternativas consideradas

- **Marcar la venta como devuelta, o editar sus líneas:** pierde el documento original, y dos devoluciones parciales serían difíciles de seguir.
- **Devolver por el mismo medio de pago** (tarjeta, Nequi): es más exacto contablemente, pero en el mostrador lo habitual es devolver efectivo. Se puede agregar después.
- **Aprobación del dueño en el momento:** frena la atención cuando no está. El motivo obligatorio y el cierre de caja dejan el control a la vista.
- **Elegir a mano el lote al reingresar:** es más fiel a la caja física, pero más lento, y el cajero rara vez lee el lote impreso.

## Consecuencias

- **Nunca se devuelve más de lo vendido**, porque cuenta lo devuelto antes. Dos devoluciones simultáneas del mismo tiquete se serializan en el caso de uso (paso 4b).
- **La caja resta los reintegros:** esperado = base + ingresos − egresos + efectivo neto de ventas − reintegros del turno.
- **El reintegro puede salir de un turno distinto al de la venta**, así que el cierre de un turno muestra devoluciones de ventas de otros días. Es lo que pasa físicamente en el cajón.
- **Las unidades descartadas no dejan movimiento en el kardex** (salieron con la venta y no vuelven). Su rastro está en la devolución; un reporte de bajas las podrá sumar.
