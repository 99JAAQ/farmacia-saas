# 0013. Cuentas por pagar por factura, fuera de la caja, solo por el dueño

- **Estado:** Aceptada
- **Fecha:** 2026-11-08

## Contexto

La farmacia compra a crédito a laboratorios y distribuidoras: cada factura tiene un plazo (30, 60 días) y se paga en una o varias partes, casi siempre por transferencia. El dueño necesita saber qué debe, a quién y qué está vencido. Las compras ya registraban si eran a crédito y su fecha de vencimiento (`PaymentTerm.Credit`, `DueDate`), pero no lo pagado.

## Decisión

- **El pago se aplica a una factura**, no al saldo del proveedor. Así trabajan los proveedores: su estado de cuenta lista facturas, cada una con su vencimiento. (Es lo contrario del fiado, donde el abono va al saldo del cliente: ver ADR 0010.)
- **Los pagos son hijos del agregado `Purchase`** (`SupplierPayment`). Una factura confirmada no se edita, así que su total es fijo y el saldo es ese total menos lo pagado. Solo una compra **confirmada y a crédito** tiene saldo; una de contado se pagó al recibirla.
- **Lo pagado se guarda como columna (`Purchase.AmountPaid`)** y cada pago la actualiza. Así cada pago modifica la fila de la compra y su `RowVersion` impide que dos pagos simultáneos superen el saldo.
- **El dinero sale de fuera de la caja:** transferencia, efectivo de la caja fuerte o cheque. La transferencia y el cheque llevan su número, para conciliar con el extracto bancario. El turno de caja no se toca; si algún día se paga del cajón, el cajero registra un egreso en Caja, como hoy.
- **Solo el dueño registra pagos** (política nueva `PaySuppliers`). El bodeguero sigue recibiendo las compras.

## Alternativas consideradas

- **Pagos al saldo del proveedor (como el fiado):** es más simple, pero se pierde qué factura está pagada y cuál vencida, que es justo lo que el proveedor cobra.
- **Un agregado aparte para la cuenta por pagar:** no hace falta. El total de la factura ya vive en `Purchase` y no cambia; otro agregado solo duplicaría el saldo.
- **Calcular lo pagado sumando los pagos:** se descartó por concurrencia. Registrar un pago solo insertaría la fila del pago, sin modificar la de la compra, y EF solo verifica el `RowVersion` al actualizar esa fila: dos pagos a la vez pasarían los dos. Un test de integración lo demuestra (con esa versión, se pagan $120.000 sobre una factura de $90.000).
- **Pagos desde el cajón con egreso automático:** mezcla las compras con la caja del mostrador y exige tener un turno abierto para pagarle a un proveedor.

## Consecuencias

- **El saldo se calcula con las líneas de la factura**, así que `Purchase` debe cargarse completo, como lo hace su repositorio (líneas y pagos).
- **Una factura está vencida desde el día siguiente a su `DueDate`** si todavía se debe algo, con la fecha de Colombia.
- **Sin anulación de pagos por ahora:** un pago mal registrado se corregiría con un ajuste cuando haga falta.
