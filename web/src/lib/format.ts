const money = new Intl.NumberFormat('es-CO', {
  style: 'currency',
  currency: 'COP',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
})

/** "$ 87.000" — Colombian pesos. */
export function formatMoney(amount: number): string {
  return money.format(amount)
}

/** "2026-10-23" (DateOnly from the API) -> "23/10/2026". No Date object: a date has no time zone to shift it. */
export function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-')
  return `${day}/${month}/${year}`
}

const time = new Intl.DateTimeFormat('es-CO', { timeZone: 'America/Bogota', hour: 'numeric', minute: '2-digit' })

/** "7:05 a. m." in Colombian time, whatever time zone the computer has: the pharmacy is in Colombia. */
export function formatTime(timestamp: string): string {
  return time.format(new Date(timestamp))
}

const dateTime = new Intl.DateTimeFormat('es-CO', {
  timeZone: 'America/Bogota',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
})

/** "27/10/2026, 8:05 p. m." in Colombian time: a ticket sold at 8 p.m. is still that day, not the UTC one. */
export function formatDateTime(timestamp: string): string {
  return dateTime.format(new Date(timestamp))
}

/**
 * Today as "YYYY-MM-DD" in the computer's local time (Colombia).
 * Not toISOString(): that is UTC, so after 7 p.m. in Colombia it would already say tomorrow.
 */
export function todayLocal(): string {
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}
