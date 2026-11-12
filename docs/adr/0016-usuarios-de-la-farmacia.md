# 0016. Usuarios de la farmacia: desactivar en vez de borrar, contraseña temporal obligatoria y nunca sin dueño

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

Con varias farmacias en la plataforma (ADR 0015), cada dueño administra a su gente: contrata cajeros y bodegueros, algunos se van y otros olvidan la contraseña. Todavía no hay envío de correos, así que no hay enlaces de invitación ni de recuperación. Además, ventas, compras, turnos de caja y kardex guardan quién hizo cada cosa.

## Decisión

- **El dueño administra los usuarios de su farmacia** (política `ManageUsers`). Los crea con uno de los tres roles de farmacia, les cambia el rol, los desactiva o reactiva, y les restablece la contraseña. Nunca puede crear un administrador de la plataforma.
- **Desactivar, nunca borrar:** `AspNetUsers.IsActive`. Un usuario desactivado no inicia sesión y no renueva la que tiene, porque se revocan todos sus refresh tokens y el refresh revisa además que siga activo. **El access token que ya tiene sigue sirviendo hasta 15 minutos.** Se aceptó esa ventana para no consultar la base en cada petición.
- **Contraseña temporal con cambio obligatorio:** quien crea el usuario (el dueño, o el administrador para el primer dueño) escribe una contraseña temporal y se la dice. El usuario queda con `MustChangePassword`, y su token lleva el claim `pwd_change`.
  - Toda política con nombre, y también la de respaldo, exige que ese claim no esté (`PasswordChangedRequirement`). Mientras tanto, la API solo le deja usar `/api/auth/me`, `change-password`, `refresh` y `logout`.
  - **Lo exige la API, no solo la pantalla:** el dueño conoce la contraseña temporal y no debe poder trabajar con la cuenta de otro.
- **Cambiar la propia contraseña** (`POST /api/auth/change-password`) pide la actual, cierra todas las demás sesiones del usuario y continúa en una nueva, ya sin la marca. Restablecer una contraseña también cierra sus sesiones y desbloquea la cuenta.
- **Varios dueños, pero nunca cero:** no se puede desactivar ni quitarle el rol al último dueño activo. Nadie se desactiva, cambia su propio rol ni restablece su propia contraseña: para eso está otro dueño, o "Cambiar contraseña".
- **Un usuario de otra farmacia responde 404.** Las tablas de Identity no están bajo RLS (ADR 0014), así que `UserAccounts` filtra cada consulta por la farmacia del token.

## Alternativas consideradas

- **Corte inmediato en cada petición** (verificar el security stamp en cada request): corta al instante, pero agrega una consulta por request o una caché con su propia invalidación. Para una farmacia, 15 minutos es aceptable.
- **Borrar usuarios:** rompería las referencias de ventas, turnos y kardex, o exigiría borrar la historia de quién hizo qué.
- **Contraseña temporal sin cambio obligatorio:** es más simple, pero el dueño conocería la contraseña de cada empleado indefinidamente.
- **Un solo dueño por farmacia:** es más simple, pero en la práctica los socios o familiares comparten la administración.

## Consecuencias

- **Un cambio de rol llega al token del usuario en el siguiente refresh,** a lo sumo en 15 minutos.
- **Dos dueños que se desactivan entre sí al mismo tiempo** podrían dejar la farmacia sin dueño activo, porque la regla se verifica sin bloquear. Es improbable. Si pasa, el administrador de la plataforma tendría que reactivar a uno (no hay pantalla para eso todavía).
- **El email es único en toda la plataforma** (es el `UserName` de Identity). Un cajero que trabajó en otra farmacia necesita otro email. El mensaje de conflicto no dice dónde está en uso.
- **Las pantallas (paso 9e) deben llevar al usuario a "Cambiar contraseña"** cuando `user.mustChangePassword` es verdadero, porque cualquier otra pantalla recibiría 403.
