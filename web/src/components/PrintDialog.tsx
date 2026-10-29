import { Printer } from 'lucide-react'
import type { ReactNode, RefObject } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'

interface Props {
  open: boolean
  title: string
  closeLabel: string
  onClose: () => void
  /** Where the focus goes when it closes (e.g. back to the search box). */
  finalFocus?: RefObject<HTMLElement | null>
  /** The printable document: it must have the `ticket-print` class (see the print styles in index.css). */
  children: ReactNode
}

/** A ticket or a voucher on screen, with a Print button for the 80 mm thermal printer. */
export function PrintDialog({ open, title, closeLabel, onClose, finalFocus, children }: Props) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="sm:max-w-sm" finalFocus={finalFocus}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        {children}
        <DialogFooter>
          <Button variant="outline" onClick={() => window.print()}>
            <Printer />
            Imprimir
          </Button>
          <Button autoFocus onClick={onClose}>
            {closeLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
