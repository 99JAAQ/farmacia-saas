# 0008. Caja por turnos con un solo turno abierto y conteo a ciegas

- **Estado:** Aceptada
- **Fecha:** 2026-10-27

## Contexto

La farmacia tiene una sola caja y varios cajeros que se turnan durante el día. Hay ventas en efectivo, egresos menores (domicilios, retiros del dueño) y, a veces, faltantes. El dueño necesita saber en qué turno ocurrió un faltante.

## Decisión

- **`CashSession` (turno) es un agregado, y `CashMovement` es su hijo.** Hay un solo turno abierto por farmacia, garantizado por un índice único filtrado (`WHERE Status = Open`).
- **Solo quien abrió el turno opera en él**, ni siquiera el dueño. **Cierra quien lo abrió o el dueño**, por si queda un turno olvidado.
- **Conteo a ciegas:** mientras el turno está abierto, la API no le envía el esperado al cajero, y sí al dueño. Al cerrar, el cajero escribe lo que contó y solo entonces ve el esperado y la diferencia.
- **Al cerrar se guardan el esperado y el contado** como un documento histórico.
- **El turno no acumula las ventas.** Al cerrar, el caso de uso calcula el efectivo neto de las ventas del turno (`ICashSalesReader`).

## Alternativas consideradas

- **Contar la caja una sola vez al final del día:** es más rápido en el cambio de turno, pero un faltante queda sin responsable.
- **Que cualquier cajero opere el turno abierto:** es más flexible, pero el efectivo de varias personas se mezcla en un mismo cajón.
- **Mostrar el esperado mientras se cuenta:** es más cómodo, pero el cajero tiende a "contar" hasta que le da, y el control pierde sentido.
- **Sumar cada venta en el turno:** cada venta actualizaría la misma fila, así que las ventas pelearían por concurrencia, y las ventas offline sincronizadas tarde quedarían por fuera.

## Consecuencias

- **Cada cajero responde solo por su turno.**
- **Si alguien cubre al cajero (el almuerzo, por ejemplo), hay que cerrar un turno y abrir otro.** Toma unos minutos más.
- **La regla del conteo a ciegas vive en el servidor (`CashSessionReader`).** Ocultarla solo en la pantalla no serviría: el número se vería en la pestaña de red del navegador.
- **La diferencia de un turno no cambia si después se corrige una venta.** Las correcciones posteriores se verán en los reportes, no en el cierre.
