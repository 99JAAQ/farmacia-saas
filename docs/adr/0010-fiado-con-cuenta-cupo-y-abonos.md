# 0010. Fiado con cuenta por cliente, cupo y abonos al saldo

- **Estado:** Aceptada
- **Fecha:** 2026-10-30
- **Modifica a:** [0009](0009-devoluciones-como-documento-aparte.md), en el reintegro de una venta fiada

## Contexto

La farmacia le fía a clientes de confianza: se llevan el producto y pagan después, en partes. Hoy eso vive en un cuaderno. El dueño necesita tres cosas: que nadie se pase de lo que él autorizó, saber cuánto le debe cada cliente y desde cuándo, y que la caja cuadre cuando entran abonos.

## Decisión

- **El dueño habilita el fiado por cliente registrado y le fija un cupo.** Se crea una `CreditAccount` (agregado) con el cupo, el saldo y, desde cuándo debe el cliente sin interrupción (`DebtSince`). Una cuenta suspendida no recibe fiados, pero sí abonos.
- **El saldo se guarda y cada cambio queda en un `CreditEntry`** (fiado, abono, devolución) con el saldo resultante, en la misma transacción. Es el mismo patrón del lote y su kardex.
- **"Fiado" es un medio de pago más** (`PaymentMethod.Credit`) y se combina con los otros: como la tarjeta, paga un monto exacto, sin devuelta. El cajero no puede pasarse del cupo.
- **Los abonos van al saldo, no a tiquetes específicos** (como el cuaderno). Cada abono (`CreditPayment`) tiene su propio consecutivo y comprobante, y lo recibe el cajero en su turno: en efectivo entra al cajón; también se acepta tarjeta, billetera o transferencia. Un abono no puede superar la deuda.
- **Sin intereses ni plazos.** El dueño ve quién debe, cuánto y desde cuándo.
- **Cambia el ADR 0009:** si se devuelve algo de una venta fiada, primero se descuenta de la deuda (hasta lo que se fió en ese tiquete y sin dejar el saldo negativo). Solo el resto se devuelve en efectivo.

## Alternativas consideradas

- **Calcular el saldo sumando los movimientos** en vez de guardarlo: no habría un saldo que se pudiera desincronizar, pero el cupo no se podría proteger bajo concurrencia sin bloqueos. Con el saldo guardado, el `RowVersion` de la cuenta hace que, si dos cajas le fían a la vez al mismo cliente, la segunda reciba un 409 en vez de pasarse del cupo.
- **Abonos imputados a cada tiquete:** es más preciso para saber qué factura está pagada, pero más lento en el mostrador y casi nunca se usa en una droguería.
- **Que el dueño pueda autorizar pasarse del cupo:** queda pendiente si hace falta; hoy el dueño puede subir el cupo.
- **Devolver siempre en efectivo, también lo fiado:** le entregaría al cliente dinero que nunca pagó.

## Consecuencias

- **Los abonos usan el contador de documentos** (`NumberedDocument.CreditPayment`), igual que los tiquetes y las devoluciones.
- **El efectivo esperado del turno suma los abonos en efectivo** y resta solo la parte de las devoluciones que se reintegró en efectivo.
- **`DebtSince` es la fecha desde la que el cliente debe sin interrupción**, no la del tiquete impago más viejo (eso exigiría imputar los abonos). Es suficiente para saber a quién llamar.
- **Hasta que el fiado esté conectado a la venta, la venta rechaza el medio de pago `Credit`.** De lo contrario, un pago "fiado" sin cuenta sería una venta gratis.
