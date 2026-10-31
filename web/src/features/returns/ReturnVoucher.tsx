import { formatDateTime, formatMoney } from '@/lib/format'
import type { SaleReturn } from './api'

/** The return voucher, printed like the ticket (the `ticket-print` class, 80 mm). */
export function ReturnVoucher({ saleReturn }: { saleReturn: SaleReturn }) {
  return (
    <div className="ticket-print grid gap-3 font-mono text-sm">
      <div className="text-center">
        <div className="font-semibold">Farmacia</div>
        <div>Devolución N.º {saleReturn.number}</div>
        <div>Del tiquete N.º {saleReturn.saleNumber}</div>
        <div className="text-muted-foreground">{formatDateTime(saleReturn.returnedAt)}</div>
      </div>

      <table className="w-full">
        <tbody>
          {saleReturn.items.map((item) => (
            <tr key={item.id} className="align-top">
              <td className="py-0.5 pr-2">
                {item.quantity} × {item.productName}
                <div className="text-xs text-muted-foreground">
                  {item.saleUnitName} a {formatMoney(item.unitPrice)}
                </div>
              </td>
              <td className="py-0.5 text-right tabular-nums">{formatMoney(item.refund)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <dl className="grid gap-0.5 border-t border-dashed pt-2">
        {saleReturn.creditedToAccount > 0 && (
          <div className="flex justify-between">
            <dt>Descontado del fiado</dt>
            <dd className="tabular-nums">{formatMoney(saleReturn.creditedToAccount)}</dd>
          </div>
        )}
        <div className="flex justify-between font-semibold">
          <dt>Efectivo devuelto</dt>
          <dd className="tabular-nums">{formatMoney(saleReturn.cashRefund)}</dd>
        </div>
      </dl>

      <div className="border-t border-dashed pt-2 text-center text-xs text-muted-foreground">
        <div>Motivo: {saleReturn.reason}</div>
        <div>Lo atendió {saleReturn.returnedByName}</div>
      </div>
    </div>
  )
}
