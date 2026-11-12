import { createBrowserRouter, Navigate } from 'react-router'
import { AppLayout } from '@/components/AppLayout'
import { FullScreenLoader } from '@/components/FullScreenLoader'
import { ChangePasswordPage } from '@/features/auth/ChangePasswordPage'
import { LoginPage } from '@/features/auth/LoginPage'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { RequireRole } from '@/features/auth/RequireRole'
import { HomePage } from '@/pages/HomePage'

/**
 * Each section loads when it is first opened (code splitting): the first visit downloads only the login, the layout
 * and the home page. The register's computer then caches the rest; a cashier never downloads the purchases screens.
 */
const page = (load: () => Promise<{ default: React.ComponentType }>) => async () => ({ Component: (await load()).default })

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    // Everything below needs a session.
    element: <RequireAuth />,
    // Shown while the code of a lazily loaded section downloads, when the app starts on it (a reload on /catalogo).
    hydrateFallbackElement: <FullScreenLoader label="Cargando…" />,
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
              { path: 'catalogo', lazy: page(() => import('@/features/catalog/CatalogPage').then((m) => ({ default: m.CatalogPage }))) },
              { path: 'catalogo/:id', lazy: page(() => import('@/features/catalog/ProductDetailPage').then((m) => ({ default: m.ProductDetailPage }))) },
            ],
          },
          {
            element: <RequireRole policy="operateCash" />,
            children: [
              { path: 'caja', lazy: page(() => import('@/features/cash/CashPage').then((m) => ({ default: m.CashPage }))) },
              { path: 'vender', lazy: page(() => import('@/features/sales/SellPage').then((m) => ({ default: m.SellPage }))) },
              { path: 'devoluciones', lazy: page(() => import('@/features/returns/ReturnsPage').then((m) => ({ default: m.ReturnsPage }))) },
              { path: 'clientes', lazy: page(() => import('@/features/customers/CustomersPage').then((m) => ({ default: m.CustomersPage }))) },
              { path: 'clientes/:id', lazy: page(() => import('@/features/customers/CustomerDetailPage').then((m) => ({ default: m.CustomerDetailPage }))) },
            ],
          },
          {
            element: <RequireRole policy="managePharmacy" />,
            children: [{ path: 'farmacia', lazy: page(() => import('@/features/pharmacy/PharmacyPage').then((m) => ({ default: m.PharmacyPage }))) }],
          },
          {
            element: <RequireRole policy="manageInventory" />,
            children: [{ path: 'alertas', lazy: page(() => import('@/features/inventory/AlertsPage').then((m) => ({ default: m.AlertsPage }))) }],
          },
          {
            element: <RequireRole policy="manageUsers" />,
            children: [{ path: 'usuarios', lazy: page(() => import('@/features/users/UsersPage').then((m) => ({ default: m.UsersPage }))) }],
          },
          {
            element: <RequireRole policy="managePlatform" />,
            children: [{ path: 'plataforma', lazy: page(() => import('@/features/platform/PlatformPage').then((m) => ({ default: m.PlatformPage }))) }],
          },
          {
            element: <RequireRole policy="receivePurchases" />,
            children: [
              { path: 'compras', lazy: page(() => import('@/features/purchases/PurchasesPage').then((m) => ({ default: m.PurchasesPage }))) },
              { path: 'compras/nueva', lazy: page(() => import('@/features/purchases/NewPurchasePage').then((m) => ({ default: m.NewPurchasePage }))) },
              { path: 'compras/:id', lazy: page(() => import('@/features/purchases/PurchaseDetailPage').then((m) => ({ default: m.PurchaseDetailPage }))) },
            ],
          },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
])
