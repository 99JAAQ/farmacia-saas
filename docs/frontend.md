# Frontend

Aplicación React en `web/`. Es una SPA que habla con la API por HTTP.

## Stack

| Herramienta | Para qué | Nota |
|---|---|---|
| Vite 8 | Servidor de desarrollo y compilación | Proxy de `/api` y `/health` a la API (puerto 5228) |
| React 19 + TypeScript 6 | Interfaz | El código se mantiene compatible con React Compiler (oxlint lo revisa), aunque el compilador aún no está activado |
| React Router 8 | Rutas, en modo datos (`createBrowserRouter`) | `RouterProvider` se importa de `react-router/dom`; ya no existe `react-router-dom` |
| TanStack Query 5 | Datos del servidor: caché, recarga, estados de carga y error | — |
| Tailwind 4 + shadcn/ui | Estilos y componentes | Preset `nova` sobre Base UI; `cn` viene del paquete npm `cn` |
| react-hook-form + zod 4 | Formularios con validación | `@hookform/resolvers` 5 (la 6 tiene un conflicto de dependencias) |
| Vitest 5 | Tests | Corren con `TZ=America/Bogota` |
| oxlint | Linter | `npm run lint` |

## Comandos

```bash
cd web
npm install
npm run dev       # http://localhost:5173 (la API debe estar corriendo en 5228)
npm test          # tests con Vitest
npm run lint      # oxlint
npm run build     # verificación de tipos + compilación de producción en dist/
```

## Organización por funcionalidad

El código se agrupa por lo que hace, no por tipo técnico (ver [estructura de carpetas](estructura-carpetas.md#frontend-web)). Una funcionalidad típica:

```
features/cash/
├── api.ts             → tipos de TypeScript (iguales a los DTOs de la API), claves de caché, funciones HTTP
├── labels.ts          → textos en español de los enums y helpers de presentación
├── labels.test.ts
├── CashPage.tsx       → la página (ruta)
└── …Form.tsx / …Dialog.tsx → componentes de la página
```

## Cliente HTTP (`src/lib/api/http.ts`)

Todas las llamadas pasan por `api<T>(url, options)`, que:

- **Agrega el access token**, que vive **solo en la memoria** del módulo.
- **Ante un 401, renueva la sesión una sola vez** con la cookie de refresh y reintenta. Si 10 peticiones reciben 401 a la vez, comparten **un solo** refresh (*single-flight*), porque la API trata un refresh token usado dos veces como robado.
- **Un error de red no cierra la sesión** (`ApiError.status === 0`): con internet inestable, "sin conexión" no significa "sesión cerrada".
- **Convierte las respuestas ProblemDetails en `ApiError`** con el mensaje del servidor, listo para mostrarlo.

## Sesión y rutas protegidas

- **`AuthProvider` arranca en el estado `restoring`** y llama a `/api/auth/refresh` para recuperar la sesión después de recargar la página. Mientras tanto se muestra `FullScreenLoader`.
- **`RequireAuth` es una ruta de layout:** si no hay sesión, redirige al login y recuerda la página a la que se quería ir.
- **`RequireRole policy="…"`** muestra "No tiene permiso" si el usuario no cumple la política.
- **Al cerrar sesión se limpia la caché de TanStack Query**, para que el siguiente usuario no vea datos del anterior.
- **El menú (`AppLayout`) muestra solo los enlaces permitidos**, con `can(user, policy)` de `permissions.ts`.

Rutas actuales (`src/router.tsx`):

| Ruta | Política | Pantalla |
|---|---|---|
| `/login` | Pública | Inicio de sesión |
| `/` | Autenticado | Inicio |
| `/caja` | `operateCash` | Caja: abrir, movimientos, cerrar |
| `/compras`, `/compras/nueva`, `/compras/:id` | `receivePurchases` | Lista, nueva compra, borrador o detalle |
| `/catalogo` | `manageCatalog` | Catálogo (provisional) |

## Convenciones

- **El dinero lo calcula el servidor.** Los totales de una compra (reparto de descuentos, IVA) vienen siempre de la API, que es una sola fuente de verdad para el dinero. El frontend solo hace aritmética trivial de la línea que se está digitando, como vista previa.
- **Las reglas de privacidad las aplica el servidor.** Por ejemplo, en el conteo a ciegas la pantalla muestra el esperado solo si la API lo envía; la pantalla no decide quién puede verlo.
- **Se repiten en el navegador validaciones simples, para avisar antes de enviar:** el dígito de verificación del NIT (`lib/nit.ts`, con los mismos casos de prueba que `Nit.cs`), una cantidad mínima, un vencimiento pasado. La API valida todo de nuevo.
- **Fechas:**
  - `todayLocal()` en vez de `toISOString()`, que es UTC y después de las 7 p. m. ya daría el día siguiente.
  - `formatDate` no crea un `Date` para una fecha sin hora (`DateOnly`).
  - `formatTime` muestra la hora en `America/Bogota`, sin importar la zona horaria del computador.
- **Formularios:** `useWatch` en vez de `watch()`, que es incompatible con React Compiler (oxlint lo marca). Así se podrá activar el compilador sin reescribir los formularios. Si la entrada y la salida del esquema zod difieren, se tipa `useForm<z.input<…>, unknown, z.output<…>>`.
- **Lector de código de barras:** `ProductPicker` busca al escribir (desde 2 letras, con una pausa) y busca de inmediato al presionar Enter, que es lo que hace el lector después de escribir el código. `findScannedUnit` elige la presentación exacta del código escaneado.
- **Accesibilidad:** cada campo tiene su `<Label htmlFor>`, los errores usan `role="alert"` y los resultados `role="status"` o `aria-live`.
- **Componentes de `components/ui/`:** los genera la CLI de shadcn (`npx shadcn add <componente>`). Tienen una regla de oxlint propia en `.oxlintrc.json`.

## Dependencias

- **`shadcn` va en `devDependencies`**, porque es la CLI; la aplicación solo importa su CSS al compilar.
- **`npm audit --omit=dev` debe quedar limpio.** El audit completo muestra un aviso bajo de `deepmerge` dentro de la CLI de shadcn, que se revisará en la fase 5.
- **Nunca ejecute `npm audit fix --force`:** instalaría shadcn 1.0.0 y rompería el proyecto.
- **No use `--legacy-peer-deps`:** esconde conflictos de versiones en vez de resolverlos.
