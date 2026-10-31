import { formatMoney } from '@/lib/format'
import type { CreditAccount, CreditEntryType, DocumentType } from './api'

export const documentTypeLabels: Record<DocumentType, string> = {
  CitizenshipCard: 'Cédula de ciudadanía',
  IdentityCard: 'Tarjeta de identidad',
  ForeignerId: 'Cédula de extranjería',
  Nit: 'NIT',
  Passport: 'Pasaporte',
  SpecialStayPermit: 'PEP',
  TemporaryProtectionPermit: 'PPT',
}

export const creditEntryLabels: Record<CreditEntryType, string> = {
  Sale: 'Fiado',
  Payment: 'Abono',
  Return: 'Devolución',
}

/** After this many days owing, the debt is shown as a warning: it is time to call the customer. */
export const oldDebtDays = 30

export type CreditTone = 'muted' | 'success' | 'warning' | 'destructive'

/** Theme tokens only (no fixed Tailwind colours): they follow dark mode. */
export const toneClasses: Record<CreditTone, string> = {
  muted: 'bg-muted text-muted-foreground',
  success: 'bg-success/10 text-success',
  warning: 'bg-warning/10 text-warning',
  destructive: 'bg-destructive/10 text-destructive',
}

/** A customer's credit in a few words, for lists and badges. */
export function describeCredit(credit: CreditAccount | null): { text: string; tone: CreditTone } {
  if (credit === null) return { text: 'Sin fiado', tone: 'muted' }
  if (credit.isSuspended)
    return { text: credit.balance > 0 ? `Suspendido · debe ${formatMoney(credit.balance)}` : 'Suspendido', tone: 'destructive' }
  if (credit.balance === 0) return { text: 'Al día', tone: 'success' }

  const days = credit.daysOwing ?? 0
  const since = days === 0 ? 'desde hoy' : days === 1 ? 'hace 1 día' : `hace ${days} días`
  return { text: `Debe ${formatMoney(credit.balance)} ${since}`, tone: days > oldDebtDays ? 'warning' : 'muted' }
}
