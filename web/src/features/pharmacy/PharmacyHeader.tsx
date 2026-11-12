import { headerLines } from './api'
import { usePharmacy } from './usePharmacy'

/** The top of every printed ticket and voucher: who sold it (name and NIT), and where to find them. */
export function PharmacyHeader() {
  const pharmacy = usePharmacy()
  const [name, ...rest] = headerLines(pharmacy.data)

  return (
    <>
      <div className="font-semibold">{name}</div>
      {rest.map((line) => (
        <div key={line} className="text-xs">
          {line}
        </div>
      ))}
    </>
  )
}
