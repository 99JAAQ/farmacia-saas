# 0006. JWT propio con refresh token rotado en cookie HttpOnly

- **Estado:** Aceptada
- **Fecha:** 2026-10-22

## Contexto

La API la consume una SPA en React y, más adelante, el POS offline. Se necesita una sesión que no obligue a iniciar sesión cada pocos minutos, pero que limite el daño si un token se roba. Los usuarios y sus contraseñas los maneja ASP.NET Core Identity.

## Decisión

- **La API emite su propio JWT** (HS256, 15 minutos) con los claims `sub`, `email`, `name`, `pharmacy_id` y `role`. El cliente lo guarda **solo en memoria**.
- **Refresh token de 64 bytes aleatorios**, guardado como SHA-256, válido por 7 días y **rotado en cada uso**. Viaja **solo** en la cookie `farmacia_refresh` (`HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`).
- **Si alguien reutiliza un refresh token ya rotado, se revoca toda su familia.**
- **Identity solo en su núcleo** (usuarios, roles, hash y bloqueo), en el mismo `DbContext`.

## Alternativas consideradas

- **`MapIdentityApi` de ASP.NET Core:** sus tokens no son JWT estándar y deciden por nosotros el formato y la duración.
- **Un proveedor externo** (Entra ID, Auth0, Keycloak): agrega costo y dependencia externa, y es demasiado para usuarios que crea el dueño de la farmacia. Puede reconsiderarse en el multi-tenant.
- **El access token en `localStorage`:** sobrevive a recargar la página, pero cualquier script inyectado (XSS) lo leería.
- **Cookies de sesión tradicionales:** funcionan para la web, pero son menos cómodas para un POS de escritorio y obligan a manejar CSRF en todos los endpoints.

## Consecuencias

- **Un token robado sirve como mucho 15 minutos**, y un refresh robado se detecta al primer uso doble.
- **JavaScript no puede leer el refresh token**, así que un XSS no puede robarlo.
- **Al recargar la página**, el frontend recupera la sesión llamando a `/api/auth/refresh`, porque la cookie viaja sola.
- **El cliente necesita un refresh compartido (*single-flight*).** Si diez peticiones reciben 401 a la vez, deben compartir un solo refresh; de lo contrario, la API vería un refresh reutilizado y revocaría la sesión.
- **En desarrollo, el proxy de Vite pone la web y la API en el mismo origen**, para que la cookie `SameSite=Strict` funcione sin CORS. En producción deben servirse desde el mismo sitio.
