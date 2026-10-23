export function FullScreenLoader({ label = 'Cargando…' }: { label?: string }) {
  return (
    <main className="flex min-h-svh items-center justify-center bg-muted" aria-busy="true">
      <p className="text-sm text-muted-foreground" role="status">
        {label}
      </p>
    </main>
  )
}
