import { paymentMethodLabels } from '@/features/sales/labels'
import { PharmacyHeader } from '@/features/pharmacy/PharmacyHeader'
import { formatDateTime, formatMoney } from '@/lib/format'
import type { CreditPayment } from './api'

/** The payment voucher, printed like the ticket (the `ticket-print` class, 80 mm). */
export function CreditPaymentVoucher({ payment }: { payment: CreditPayment }) {
  return (
    <div className="ticket-print grid gap-3 font-mono text-sm">
      <div className="text-center">
        <PharmacyHeader />
        <div>Abono N.º {payment.number}</div>
        <div className="text-muted-foreground">{formatDateTime(payment.receivedAt)}</div>
      </div>

      <div className="text-center">{payment.customerName}</div>

      <dl className="grid gap-0.5 border-t border-dashed pt-2">
        <div className="flex justify-between font-semibold">
          <dt>Abono ({paymentMethodLabels[payment.method]})</dt>
          <dd className="tabular-nums">{formatMoney(payment.amount)}</dd>
        </div>
        <div className="flex justify-between">
          <dt>Queda debiendo</dt>
          <dd className="tabular-nums">{formatMoney(payment.balanceAfter)}</dd>
        </div>
      </dl>

      <div className="border-t border-dashed pt-2 text-center text-xs text-muted-foreground">
        <div>Lo recibió {payment.receivedByName}</div>
        <div>{payment.balanceAfter === 0 ? 'Quedó a paz y salvo. ¡Gracias!' : 'Gracias por su abono'}</div>
      </div>
    </div>
  )
}
