import { useQuery } from '@tanstack/react-query'
import { TriangleAlert, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import { formatStock } from '@/features/catalog/stock'
import { alertKeys, getStockAlerts, newlyLow, type LowStockAlert } from './alerts'

/**
 * The live notice: when a sale (on any register) leaves a product below its minimum, whoever manages the inventory
 * sees it right away, on whatever screen they are. Only crossings are announced, not the alerts already there.
 */
export function LowStockNotice() {
  const alerts = useQuery({ queryKey: alertKeys.all, queryFn: ({ signal }) => getStockAlerts(signal) })
  const previous = useRef<Set<string> | null>(null)
  const [notice, setNotice] = useState<LowStockAlert[]>([])

  useEffect(() => {
    if (!alerts.data) return
    const crossed = newlyLow(previous.current, alerts.data.lowStock)
    previous.current = new Set(alerts.data.lowStock.map((alert) => alert.product.id))
    // A notice that arrives from the server (real time): reacting to new data is what this effect is for.
    if (crossed.length > 0) setNotice((current) => [...current.filter((c) => !crossed.some((x) => x.product.id === c.product.id)), ...crossed])
  }, [alerts.data])

  if (notice.length === 0) return null

  return (
    <div role="status" className="mb-6 flex items-start gap-3 rounded-lg border border-warning/40 bg-warning/10 p-4 text-sm">
      <TriangleAlert className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden="true" />
      <div className="grid flex-1 gap-1">
        <p className="font-medium">
          {notice.length === 1 ? 'Un producto quedó por debajo de su mínimo' : `${notice.length} productos quedaron por debajo de su mínimo`}
        </p>
        <ul className="text-muted-foreground">
          {notice.map(({ product, sellableUnits }) => (
            <li key={product.id}>
              {product.name}: quedan {formatStock(sellableUnits, product.saleUnits, product.baseUnitName)}
            </li>
          ))}
        </ul>
        <Link to="/alertas" className="w-fit font-medium underline-offset-4 hover:underline" onClick={() => setNotice([])}>
          Ver alertas
        </Link>
      </div>
      <button type="button" aria-label="Cerrar el aviso" className="rounded p-1 hover:bg-warning/20" onClick={() => setNotice([])}>
        <X className="size-4" aria-hidden="true" />
      </button>
    </div>
  )
}
