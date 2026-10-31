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

- **Una respuesta sin cuerpo es válida con cualquier código de éxito**, no solo con 204: por ejemplo, habilitar el fiado responde 201 con solo el encabezado `Location`.

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
- **El menú (`AppLayout`) muestra solo los enlaces permitidos**, con `can(user, policy)` de `permissions.ts`. Las secciones (ruta, nombre, ícono, política) están en una sola lista, `components/navigation.ts`, que usan la barra lateral y los accesos directos del inicio.

Rutas actuales (`src/router.tsx`):

| Ruta | Política | Pantalla |
|---|---|---|
| `/login` | Pública | Inicio de sesión |
| `/` | Autenticado | Inicio |
| `/vender` | `operateCash` | POS: vender en el turno propio, tiquete, ventas del turno |
| `/devoluciones` | `operateCash` | Buscar un tiquete por número, devolver por línea, comprobante |
| `/clientes`, `/clientes/:id` | `operateCash` (administrar: `manageCredit`) | Clientes y fiado: búsqueda, quién debe, cupo, suspender, movimientos |
| `/caja` | `operateCash` | Caja: abrir, movimientos, cerrar |
| `/compras`, `/compras/nueva`, `/compras/:id` | `receivePurchases` | Lista, nueva compra, borrador o detalle |
| `/catalogo`, `/catalogo/:id` | `manageCatalog` | Catálogo con stock y vencimientos; ficha del producto: datos, presentaciones, descontinuar, lotes |

## Apariencia

- **Un solo color de marca, teal, en el token `--primary`**, sobre grises con un leve tinte frío. Los tokens están en `src/index.css`, y todos los componentes de shadcn los leen, así que cambiar el color de la farmacia (por ejemplo, en la fase 3, uno por tenant) es cambiar `--primary` y sus compañeros, sin tocar componentes.
- **Tokens propios `success` y `warning`** (además de `destructive`) para devueltas, sobrantes de caja o lotes por vencer: `text-success`, `bg-warning/10`. **No se usan colores fijos** como `text-green-700`, porque no cambian con el tema ni con el modo oscuro.
- **Modo oscuro según el sistema operativo** (`prefers-color-scheme`), solo con CSS: sin botón ni estado que recordar, y sin parpadeo del tema equivocado al cargar. La variante `dark:` de Tailwind usa la misma media query.
- **Logo:** `components/Logo.tsx` (cruz de farmacia sobre un cuadro del color de marca) y el mismo dibujo en `public/favicon.svg`.
- **Layout:** barra lateral en pantallas anchas (el computador de la caja) y barra superior en pantallas angostas.

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
- **Carrito del POS (`features/sales`):**
  - Vive en el navegador y "Cobrar" envía la venta completa. Se guarda en `localStorage` (`cartStorage.ts`) con una clave por farmacia y usuario, así sobrevive a una recarga o a un corte de luz sin que otro cajero lo herede. Un carrito de hace más de 12 horas se descarta, y si los datos guardados no se pueden leer, se empieza vacío en vez de romper la caja.
  - El total del carrito es un **estimado** (precio × cantidad, sin descuentos en el mostrador). El oficial es el del tiquete que devuelve el servidor con los precios actuales del catálogo. Nunca se envían precios.
  - `summarizePayment` repite las reglas de pago de `Sale.AddPayments` solo para avisar antes de cobrar: tarjeta, billetera y transferencia pagan un monto exacto sin superar el total; el efectivo cubre el resto y da la devuelta; si el resto es 0, no se envía efectivo.
- **Pantalla POS (`/vender`):**
  - Exige un turno abierto propio; si no lo hay, explica por qué y enlaza a Caja.
  - El buscador tiene el foco al entrar y lo recupera al cerrar el tiquete (`finalFocus` del diálogo).
  - **F2 cobra** desde cualquier parte de la página y **Esc vuelve al buscador**. Esc nunca vacía el carrito, porque es demasiado fácil de presionar por error; vaciar es un botón.
  - Agregar tarjeta, Nequi o transferencia propone por defecto lo que falta, y "Exacto" llena el efectivo con lo justo. La casilla "Fórmula revisada" aparece solo si algún producto la requiere.
  - Cualquier cambio en la venta borra el error anterior (por ejemplo, "stock insuficiente" después de bajar la cantidad).
- **Pantalla de devoluciones (`/devoluciones`):**
  - Igual que Vender, exige un turno abierto propio (`RequireOwnShift`, compartido por las dos pantallas).
  - El cajero escribe el número del tiquete y ve por línea cuánto queda por devolver. Elige la cantidad con − y + (nunca más de lo que queda) y si vuelve al inventario o se descarta, y escribe el motivo.
  - El botón dice el monto ("Devolver $12.000"), así el cajero ve cuánto sale del cajón antes de confirmar. La lógica (`returnDraft.ts`) es pura y tiene sus propios tests.
  - El formulario sigue visible mientras TanStack Query vuelve a pedir el tiquete (por ejemplo, al recuperar el foco de la ventana), para no perder lo que el cajero ya marcó. Solo se reinicia si cambian el tiquete o lo ya devuelto.
- **Clientes y fiado (`/clientes`):**
  - Todos en la caja pueden buscar clientes y ver lo que deben, pero solo el dueño (`manageCredit`) ve "Nuevo cliente", la pestaña "Quién debe" y los controles del fiado (habilitar con un cupo, cambiar el cupo, suspender o reanudar). Al cajero se le explica que se lo pida al dueño. La API hace cumplir lo mismo.
  - El estado del fiado se resume en palabras con `describeCredit` ("Debe $12.000 hace 3 días", "Al día", "Suspendido"), y una deuda de más de 30 días se marca como aviso (`warning`).
  - Un cliente con NIT se registra como un proveedor: se escribe sin el dígito de verificación y la pantalla lo calcula en vivo.
  - Cada cambio invalida todas las consultas `['customers', …]` (cliente, movimientos, búsqueda y deudores), así nada queda desactualizado.
  - **"Recibir abono"** (en la ficha de un cliente que debe): valor (con "Todo" = la deuda) y medio de pago; luego el comprobante imprimible con lo que queda debiendo. Sin turno propio abierto, en vez del botón se explica que hay que abrirlo en Caja.
- **Fiado en el POS:** "+ Fiado" es un medio de pago más y abre `CreditCustomerPicker`, que solo lista clientes con fiado y muestra lo disponible de cada uno. `summarizePayment` aplica las reglas del servidor: el fiado necesita cliente, una cuenta no suspendida y espacio en el cupo. El tiquete muestra el cliente.
- **Devolución de un tiquete fiado:** la pantalla avisa que primero se descuenta de la deuda; cuánto exactamente depende de la deuda de hoy y lo calcula el servidor. El comprobante separa "Descontado del fiado" de "Efectivo devuelto", y el título del diálogo dice cuánto efectivo entregar.
- **Catálogo (`/catalogo`):**
  - La lista muestra por producto sus presentaciones con precio, el stock y el próximo vencimiento. Marca en rojo las unidades en lotes vencidos y como aviso lo que vence en 90 días o menos. Los descontinuados se ven solo con "Mostrar descontinuados".
  - El stock se expresa en palabras de mostrador con `formatStock` ("3 × Caja x30 + 5 tabletas"): se guarda en unidades base, pero se lee en la presentación más grande y lo que queda suelto. `daysUntil` cuenta días entre fechas sin crear `Date` con zona horaria.
  - "Nuevo producto" pide los datos y la primera presentación (la API exige al menos una), y lleva a la ficha.
  - La ficha (`/catalogo/:id`) tiene el formulario de datos (con categoría, que se puede crear ahí mismo), las presentaciones editables una por una (cada fila con su "Guardar"), "Descontinuar"/"Reactivar" y los lotes con su vencimiento y costo.
  - Las claves de React están acotadas: el formulario de datos se reinicia solo si cambian los datos del producto, y cada presentación solo si cambia ella. Así, guardar un precio no borra lo que se estaba editando en otra parte de la ficha.
- **Tiquete y comprobante impresos:** `PrintDialog` (en `components/`) muestra el tiquete o el comprobante de devolución con el botón Imprimir; los dos llevan la clase `ticket-print`. `window.print()` con estilos `@media print` en `index.css` para impresora térmica de 80 mm, sin drivers ni librerías. Mientras hay un tiquete abierto, la app se quita con `display: none` (no solo se oculta, para que el rollo no avance papel en blanco), el diálogo pierde su centrado y todo se imprime en negro. El encabezado dice "Farmacia" mientras la API no exponga el nombre y el NIT de la farmacia.
- **Accesibilidad:** cada campo tiene su `<Label htmlFor>`, los errores usan `role="alert"` y los resultados `role="status"` o `aria-live`.
- **Componentes de `components/ui/`:** los genera la CLI de shadcn (`npx shadcn add <componente>`). Tienen una regla de oxlint propia en `.oxlintrc.json`.

## Dependencias

- **`shadcn` va en `devDependencies`**, porque es la CLI; la aplicación solo importa su CSS al compilar.
- **`npm audit --omit=dev` debe quedar limpio.** El audit completo muestra un aviso bajo de `deepmerge` dentro de la CLI de shadcn, que se revisará en la fase 5.
- **Nunca ejecute `npm audit fix --force`:** instalaría shadcn 1.0.0 y rompería el proyecto.
- **No use `--legacy-peer-deps`:** esconde conflictos de versiones en vez de resolverlos.
