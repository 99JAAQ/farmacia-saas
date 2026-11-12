# 0019. Un solo contenedor: la API sirve el frontend en el mismo origen

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

Para publicar la demo (fase 5) en planes gratuitos de Azure, lo natural parecía separar las piezas: el frontend en Azure Static Web Apps (gratis) y la API en Azure Container Apps (gratis hasta cierto uso). Pero eso deja **dos dominios distintos**, y la app depende de tener uno solo:

- **El refresh token viaja en una cookie `SameSite=Strict`** (ADR 0006), que es la protección contra CSRF. El navegador no la envía en peticiones de otro sitio, así que con dos dominios la sesión no se podría renovar.
- **La conexión de SignalR y las llamadas a la API** necesitarían CORS.
- Static Web Apps puede reenviar `/api` a un backend en el mismo dominio ("linked backend"), pero **solo en su plan de pago**.

## Decisión

- **Una sola imagen de Docker,** con la API en ASP.NET Core y el build de React en `wwwroot`, servida por la misma app.
  - `UseDefaultFiles` y `UseStaticFiles` sirven los archivos.
  - `MapSpa` devuelve `index.html` para las rutas del cliente (`/caja`, `/catalogo/123`), así que recargar o compartir un enlace funciona.
  - El fallback excluye `/api`, `/hubs`, `/health`, `/openapi` y `/scalar`: una ruta de API mal escrita responde 404, nunca una página.
- **`UseRouting()` se llama explícitamente después de los archivos estáticos.** Con *minimal hosting*, si no se llama, el ruteo se agrega al inicio, el fallback toma `/assets/app.js` y el middleware de archivos estáticos se salta las peticiones que ya tienen endpoint: todos los scripts volvían como `index.html`. Un test lo comprueba.
- **El `Dockerfile` tiene varias etapas:**
  - Node compila el frontend;
  - el SDK de .NET publica la API;
  - el resultado se copia a `aspnet:10.0-noble-chiseled-extra`, una imagen sin shell ni gestor de paquetes, que corre como usuario sin privilegios y trae ICU, que SqlClient necesita.
- **Las migraciones son una imagen aparte,** el target `migrator`: un *bundle* de EF Core autocontenido que aplica las migraciones y termina. El despliegue lo corre antes de la app, que nunca migra al arrancar.
- **En producción, la app:**
  - confía en `X-Forwarded-*`, porque TLS termina en el ingress de Azure (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`);
  - envía HSTS y una CSP estricta (`default-src 'self'`; estilos en línea permitidos por los atributos `style` de React);
  - crea los roles y el primer administrador de la plataforma desde la configuración (`ProductionBootstrap`, idempotente). Ese administrador entra con contraseña temporal (ADR 0016).

## Alternativas consideradas

- **Static Web Apps y Container Apps por separado:** son dos dominios, con la cookie y CORS rotos, o con el plan de pago de Static Web Apps.
- **Cambiar la cookie a `SameSite=None` y habilitar CORS:** debilita la protección contra CSRF solo para ahorrar un contenedor.
- **Un proxy inverso (nginx) delante de las dos piezas:** es otra pieza más que mantener, para lo mismo que ASP.NET Core ya hace.
- **Migrar al arrancar la app (`Database.Migrate()`):** con varias réplicas, todas intentarían migrar a la vez, y la app necesitaría permisos de DDL, justo lo que el ADR 0014 quiere quitarle.

## Consecuencias

- **Un solo despliegue y una sola versión:** el frontend y la API nunca quedan desfasados.
- **El navegador ve un solo origen,** como en desarrollo con el proxy de Vite: no hay CORS.
- **El frontend se divide por rutas (rutas perezosas):** la primera carga baja de 800 kB a 346 kB (106 kB comprimidos). Cada sección se descarga al abrirla.
- **El frontend no tiene CDN propio;** lo sirve el contenedor. Para una farmacia es suficiente. Si hiciera falta, se puede poner Azure Front Door delante.
- **`docker compose --profile app up --build` levanta el sistema completo como en producción,** en http://localhost:8080, con su propia base (`FarmaciaCompose`).
