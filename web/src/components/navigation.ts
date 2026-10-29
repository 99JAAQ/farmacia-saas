import { House, Pill, ShoppingCart, Truck, Wallet, type LucideIcon } from 'lucide-react'
import { can, type Policy } from '@/features/auth/permissions'
import type { User } from '@/lib/api/types'

export interface Section {
  to: string
  label: string
  description: string
  icon: LucideIcon
  /** Who sees it; undefined = every signed-in user. The API enforces it anyway. */
  policy?: Policy
}

/** The app's sections, in menu order. The sidebar and the home page's shortcuts both come from here. */
const sections: Section[] = [
  { to: '/', label: 'Inicio', description: 'Resumen del día', icon: House },
  { to: '/vender', label: 'Vender', description: 'Cobrar con el lector de código de barras', icon: ShoppingCart, policy: 'operateCash' },
  { to: '/caja', label: 'Caja', description: 'Abrir, registrar movimientos y cerrar el turno', icon: Wallet, policy: 'operateCash' },
  { to: '/compras', label: 'Compras', description: 'Recibir facturas de proveedores', icon: Truck, policy: 'receivePurchases' },
  { to: '/catalogo', label: 'Catálogo', description: 'Productos y presentaciones', icon: Pill, policy: 'manageCatalog' },
]

export function sectionsFor(user: User | null): Section[] {
  return sections.filter((section) => section.policy === undefined || can(user, section.policy))
}
