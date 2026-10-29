import { createBrowserRouter, Navigate } from 'react-router'
import { AppLayout } from '@/components/AppLayout'
import { LoginPage } from '@/features/auth/LoginPage'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { RequireRole } from '@/features/auth/RequireRole'
import { CashPage } from '@/features/cash/CashPage'
import { NewPurchasePage } from '@/features/purchases/NewPurchasePage'
import { PurchaseDetailPage } from '@/features/purchases/PurchaseDetailPage'
import { PurchasesPage } from '@/features/purchases/PurchasesPage'
import { ReturnsPage } from '@/features/returns/ReturnsPage'
import { SellPage } from '@/features/sales/SellPage'
import { CatalogPage } from '@/pages/CatalogPage'
import { HomePage } from '@/pages/HomePage'

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    // Everything below needs a session.
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <HomePage /> },
          {
            element: <RequireRole policy="manageCatalog" />,
            children: [{ path: 'catalogo', element: <CatalogPage /> }],
          },
          {
            element: <RequireRole policy="operateCash" />,
            children: [
              { path: 'caja', element: <CashPage /> },
              { path: 'vender', element: <SellPage /> },
              { path: 'devoluciones', element: <ReturnsPage /> },
            ],
          },
          {
            element: <RequireRole policy="receivePurchases" />,
            children: [
              { path: 'compras', element: <PurchasesPage /> },
              { path: 'compras/nueva', element: <NewPurchasePage /> },
              { path: 'compras/:id', element: <PurchaseDetailPage /> },
            ],
          },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
])
