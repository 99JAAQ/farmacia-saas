import type { RefObject } from 'react'
import { PharmacyHeader } from '@/features/pharmacy/PharmacyHeader'
import { PrintDialog } from '@/components/PrintDialog'
import { formatDateTime, formatMoney } from '@/lib/format'
import type { Sale } from './api'
import { paymentMethodLabels } from './labels'

/**
 * The customer's ticket: a simple receipt with the pharmacy's consecutive number (not a DIAN electronic invoice).
 * The `ticket-print` class is what gets printed (see index.css): 80 mm wide, for a thermal printer.
 */
export function Ticket({ sale }: { sale: Sale }) {
  return (
    <div className="ticket-print grid gap-3 font-mono text-sm">
      <div className="text-center">
        <PharmacyHeader />
        <div>Tiquete N.º {sale.number}</div>
        <div className="text-muted-foreground">{formatDateTime(sale.soldAt)}</div>
      </div>

      <table className="w-full">
        <tbody>
          {sale.items.map((item) => (
            <tr key={item.id} className="align-top">
              <td className="py-0.5 pr-2">
                {item.quantity} × {item.productName}
                <div className="text-xs text-muted-foreground">
                  {item.saleUnitName} a {formatMoney(item.unitPrice)}
                </div>
              </td>
              <td className="py-0.5 text-right tabular-nums">{formatMoney(item.total)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <dl className="grid gap-0.5 border-t border-dashed pt-2">
        <Row label="Total" value={formatMoney(sale.total)} strong />
        {sale.vat > 0 && <Row label="IVA incluido" value={formatMoney(sale.vat)} />}
        {sale.payments.map((payment) => (
          <Row
            key={payment.method}
            label={paymentMethodLabels[payment.method]}
            value={formatMoney(payment.received ?? payment.amount)}
          />
        ))}
        {sale.change > 0 && <Row label="Devuelta" value={formatMoney(sale.change)} strong />}
      </dl>

      <div className="border-t border-dashed pt-2 text-center text-xs text-muted-foreground">
        {sale.customerName && <div>Cliente: {sale.customerName}</div>}
        {sale.prescriptionChecked && <div>Fórmula médica revisada</div>}
        <div>Lo atendió {sale.soldByName}</div>
        <div>Gracias por su compra</div>
      </div>
    </div>
  )
}

function Row({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={strong ? 'flex justify-between font-semibold' : 'flex justify-between'}>
      <dt>{label}</dt>
      <dd className="tabular-nums">{value}</dd>
    </div>
  )
}

interface TicketDialogProps {
  sale: Sale | null
  onClose: () => void
  title?: string
  closeLabel?: string
  /** Where the focus goes when it closes (the POS sends it back to the product search). */
  finalFocus?: RefObject<HTMLElement | null>
}

/** Shows a ticket with a Print button: right after charging, or to print an earlier one again. */
export function TicketDialog({
  sale,
  onClose,
  title = 'Venta registrada',
  closeLabel = 'Nueva venta',
  finalFocus,
}: TicketDialogProps) {
  return (
    <PrintDialog open={sale !== null} title={title} closeLabel={closeLabel} onClose={onClose} finalFocus={finalFocus}>
      {sale && <Ticket sale={sale} />}
    </PrintDialog>
  )
}
