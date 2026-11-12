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
| `/cambiar-clave` | Autenticado (fuera del layout) | Cambiar la contraseña. Obligatoria con una contraseña temporal: `RequireAuth` lleva ahí a todo usuario con `mustChangePassword`, desde cualquier ruta |
| `/` | Autenticado | Inicio |
| `/vender` | `operateCash` | POS: vender en el turno propio, tiquete, ventas del turno |
| `/devoluciones` | `operateCash` | Buscar un tiquete por número, devolver por línea, comprobante |
| `/clientes`, `/clientes/:id` | `operateCash` (administrar: `manageCredit`) | Clientes y fiado: búsqueda, quién debe, cupo, suspender, movimientos |
| `/caja` | `operateCash` | Caja: abrir, movimientos, cerrar |
| `/compras`, `/compras/nueva`, `/compras/:id` | `receivePurchases` (pagar: `paySuppliers`) | Por pagar, borradores y recientes; nueva compra; borrador o detalle con sus pagos |
| `/catalogo`, `/catalogo/:id` | `manageCatalog` (bajas, ajustes y movimientos: `manageInventory`) | Catálogo con stock y vencimientos; ficha del producto: datos, presentaciones, descontinuar, lotes, bajas y ajustes, movimientos |
| `/usuarios` | `manageUsers` | Usuarios de la farmacia: crear con contraseña temporal, cambiar el rol, desactivar o reactivar, restablecer la contraseña |
| `/plataforma` | `managePlatform` | Solo el administrador de la plataforma: las farmacias con sus dueños, y "Nueva farmacia" (con el DV del NIT en vivo y el dueño con contraseña temporal) |
| `/farmacia` | `managePharmacy` | Mi farmacia: nombre, dirección y teléfono del tiquete (el NIT solo se muestra), "Responsable de IVA", y una vista previa del encabezado |

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
- **Inventario en la ficha del producto (`features/inventory`, con `manageInventory`):**
  - Cada lote tiene "Ajustar" y, si venció, "Dar de baja". La baja pide confirmación porque no se deshace.
  - El ajuste pide lo contado y el motivo, y muestra la diferencia en vivo antes de confirmar (`describeCount`, con tests): el bodeguero ve qué va a quedar registrado.
  - "Movimientos" es el kardex: fecha, documento o tipo de movimiento (con el motivo de un ajuste), lote, quién, cambio con signo y lo que quedó del producto.
  - Todo vive bajo la clave `['products', …]`, así que una baja o un ajuste refrescan a la vez los lotes, el kardex y el stock del catálogo.
- **Cuentas por pagar (en Compras):**
  - La lista de compras empieza con **"Por pagar"**: el total adeudado, cuántas facturas están vencidas y cada factura con su vencimiento en palabras (`describeDue`: "Vencida hace 38 días" en rojo, "Vence en 5 días" como aviso), la más vencida primero.
  - El detalle de una compra a crédito confirmada muestra el total, lo pagado, el saldo, los pagos con quién los registró y, solo al dueño (`paySuppliers`), **"Registrar pago"**. El diálogo aplica las reglas del servidor antes de guardar (`paymentProblem`: no superar el saldo; transferencia y cheque con su número). El efectivo de la caja fuerte nunca envía referencia, aunque se haya escrito una antes de cambiar de medio.
- **Tiquete y comprobante impresos:** `PrintDialog` (en `components/`) muestra el tiquete o el comprobante de devolución con el botón Imprimir; los dos llevan la clase `ticket-print`. `window.print()` con estilos `@media print` en `index.css` para impresora térmica de 80 mm, sin drivers ni librerías. Mientras hay un tiquete abierto, la app se quita con `display: none` (no solo se oculta, para que el rollo no avance papel en blanco), el diálogo pierde su centrado y todo se imprime en negro. El encabezado de todo comprobante es `PharmacyHeader`: nombre, NIT, dirección y teléfono de la farmacia (`headerLines`, con tests), de `GET /api/pharmacy`.
- **Contraseñas temporales:** `can()` niega todo a un usuario con `mustChangePassword`, igual que la API, y `RequireAuth` lo lleva a `/cambiar-clave`, que es de pantalla completa como el login y solo ofrece "Salir". Al guardar, la API devuelve una sesión nueva que reemplaza a la actual (`changePassword` llama a `setSession`). `lib/password.ts` replica las reglas de Identity (8+, mayúscula, minúscula y número) para avisar antes de enviar. La contraseña temporal se escribe en un campo visible, porque el dueño se la lee al usuario.
- **Usuarios (`features/users`):** el dueño no ve acciones sobre sí mismo ("usted"): la API no le deja desactivarse ni cambiarse el rol. El rol se cambia con un selector en la fila. Desactivar pide confirmación y explica que la sesión abierta se cierra en máximo 15 minutos. `describeStatus` (con tests) avisa a quién le falta cambiar la contraseña temporal.
- **Administrador de la plataforma:** solo ve "Inicio" y "Farmacias", y bajo el logo dice "Plataforma". Cualquier otra ruta muestra "No tiene permiso".
- **Alertas (`features/inventory`, con `manageInventory`):** la página `/alertas` (bajo el mínimo, vencidos con unidades, por vencer en 90 días) y el contador junto a "Alertas" en el menú comparten la consulta `['products', 'alerts']`, así que nunca discrepan, y los avisos `stock` la refrescan en vivo. `LowStockNotice`, en el layout, muestra un aviso en cualquier pantalla cuando un producto **cruza** su mínimo (`newlyLow`, con tests: la primera carga no avisa). El mínimo se escribe en la ficha del producto en una presentación ("16 Caja x30") y se guarda en unidades base (`MinimumStockEditor`).
- **Panel del día (`features/dashboard`, en Inicio, solo el dueño con `viewDashboard`):** ventas netas como única cifra principal, tarjetas de ventas (con ticket promedio), margen bruto (con % y costo) y devoluciones; ventas por hora en columnas de una sola serie (barras de máximo 24 px, puntas de 4 px, etiqueta solo en la hora pico, tooltip por barra con mouse o teclado, "Ver como tabla"); medios de pago en barras de un solo tono; lo más vendido. Su clave está bajo `['sales', …]`, así que se actualiza en vivo con cada venta sin código propio. Color de datos: `--chart-1`, validado con el validador del skill de visualización en modo claro y oscuro (el `--primary` no pasaba: croma muy baja en claro, demasiado luminoso en oscuro). `chartHours` (con tests) muestra de 7 a 19, ampliado si hubo ventas fuera de ese horario.
- **Tiempo real (`lib/realtime`):** `useRealtime`, en el layout, abre una conexión de SignalR (`@microsoft/signalr`) para todo usuario de una farmacia que pueda trabajar. Con cada aviso, `invalidationsFor` (con tests) traduce el tema a prefijos de claves de TanStack Query: `stock` → `['products']`, `sales` → `['sales']` y `['cash-sessions']`, etc. Solo las consultas en pantalla vuelven a pedirse. Tras cualquier corte (reconexión o un inicio fallido) se invalida todo, porque pudo perderse un aviso. Si falla al conectar, renueva el token y reintenta con espera creciente, hasta 30 s. En desarrollo, el StrictMode de React monta el efecto dos veces, así que la consola muestra una vez "connection was stopped during negotiation". No pasa en producción.
- **La farmacia actual (`features/pharmacy`):** `usePharmacy` la carga una vez por sesión (el layout la pide para mostrar su nombre bajo el logo, así ya está en caché cuando se imprime un tiquete). Con varias farmacias en la plataforma, el nombre en la barra lateral deja claro en cuál se está trabajando. El administrador de la plataforma no tiene farmacia y no la pide.
- **Accesibilidad:** cada campo tiene su `<Label htmlFor>`, los errores usan `role="alert"` y los resultados `role="status"` o `aria-live`.
- **Componentes de `components/ui/`:** los genera la CLI de shadcn (`npx shadcn add <componente>`). Tienen una regla de oxlint propia en `.oxlintrc.json`.

## Dependencias

- **`shadcn` va en `devDependencies`**, porque es la CLI; la aplicación solo importa su CSS al compilar.
- **`npm audit --omit=dev` debe quedar limpio.** El audit completo muestra un aviso bajo de `deepmerge` dentro de la CLI de shadcn, que se revisará en la fase 5.
- **Nunca ejecute `npm audit fix --force`:** instalaría shadcn 1.0.0 y rompería el proyecto.
- **No use `--legacy-peer-deps`:** esconde conflictos de versiones en vez de resolverlos.
