import { createBrowserRouter, Navigate } from 'react-router'
import { AlertsPage } from '@/features/inventory/AlertsPage'
import { UsersPage } from '@/features/users/UsersPage'
import { PlatformPage } from '@/features/platform/PlatformPage'
import { ChangePasswordPage } from '@/features/auth/ChangePasswordPage'
import { PharmacyPage } from '@/features/pharmacy/PharmacyPage'
import { AppLayout } from '@/components/AppLayout'
import { LoginPage } from '@/features/auth/LoginPage'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { RequireRole } from '@/features/auth/RequireRole'
import { CashPage } from '@/features/cash/CashPage'
import { CatalogPage } from '@/features/catalog/CatalogPage'
import { ProductDetailPage } from '@/features/catalog/ProductDetailPage'
import { CustomerDetailPage } from '@/features/customers/CustomerDetailPage'
import { CustomersPage } from '@/features/customers/CustomersPage'
import { NewPurchasePage } from '@/features/purchases/NewPurchasePage'
import { PurchaseDetailPage } from '@/features/purchases/PurchaseDetailPage'
import { PurchasesPage } from '@/features/purchases/PurchasesPage'
import { ReturnsPage } from '@/features/returns/ReturnsPage'
import { SellPage } from '@/features/sales/SellPage'
import { HomePage } from '@/pages/HomePage'

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    // Everything below needs a session.
    element: <RequireAuth />,
    children: [
      // Full screen, outside the layout: with a temporary password it is the only screen the user can use.
      { path: 'cambiar-clave', element: <ChangePasswordPage /> },
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <HomePage /> },
          {
            element: <RequireRole policy="manageCatalog" />,
            children: [
              { path: 'catalogo', element: <CatalogPage /> },
              { path: 'catalogo/:id', element: <ProductDetailPage /> },
            ],
          },
          {
            element: <RequireRole policy="operateCash" />,
            children: [
              { path: 'caja', element: <CashPage /> },
              { path: 'vender', element: <SellPage /> },
              { path: 'devoluciones', element: <ReturnsPage /> },
              { path: 'clientes', element: <CustomersPage /> },
              { path: 'clientes/:id', element: <CustomerDetailPage /> },
            ],
          },
          {
            element: <RequireRole policy="managePharmacy" />,
            children: [{ path: 'farmacia', element: <PharmacyPage /> }],
          },
          {
            element: <RequireRole policy="manageInventory" />,
            children: [{ path: 'alertas', element: <AlertsPage /> }],
          },
          {
            element: <RequireRole policy="manageUsers" />,
            children: [{ path: 'usuarios', element: <UsersPage /> }],
          },
          {
            element: <RequireRole policy="managePlatform" />,
            children: [{ path: 'plataforma', element: <PlatformPage /> }],
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
