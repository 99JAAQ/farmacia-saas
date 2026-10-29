import type { ReturnDisposition } from './api'

export const dispositionLabels: Record<ReturnDisposition, string> = {
  Restock: 'Vuelve al inventario',
  Discard: 'Se descarta',
}
