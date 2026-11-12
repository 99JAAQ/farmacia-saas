# 0015. Alta de farmacias por un administrador de la plataforma, y una farmacia por usuario

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

Con varias farmacias en la plataforma (fase 3), alguien tiene que crearlas, junto con su primer usuario: el dueño, que después crea a sus cajeros y bodegueros. Hasta ahora la única farmacia la creaba el seeder de desarrollo.

Había que decidir tres cosas:

- quién crea una farmacia;
- cómo ese alguien queda fuera de los datos de las farmacias;
- si un usuario puede trabajar en varias farmacias.

## Decisión

- **Un rol nuevo, `PlatformAdmin`,** crea las farmacias con su dueño. **No hay registro público.**
- **El administrador no pertenece a ninguna farmacia.** `AspNetUsers.PharmacyId` admite nulos solo para él, y su token **no lleva `pharmacy_id`**. Así:
  - **Su sesión no ve filas del negocio:** Row-Level Security no le da contexto de farmacia (ADR 0014).
  - **La política de respaldo (fallback) exige el claim `pharmacy_id`,** además de estar autenticado. Cualquier endpoint del negocio, incluso uno que solo pide "un usuario autenticado", le responde 403.
  - **Solo entra en `/api/platform/pharmacies`,** con la política `ManagePlatform`.
- **El alta crea la farmacia y su dueño en una transacción** (`PlatformPharmacies`, en Infrastructure). Si el dueño no se puede crear (contraseña débil, email usado), tampoco queda la farmacia. Para escribir una farmacia que no es la de la sesión, usa el bypass de RLS en un scope de servicios propio. Fuera de él, solo lo usa el seeder.
- **El administrador escribe una contraseña inicial** y se la entrega al dueño. No hay correo de invitación porque todavía no hay envío de correos.
- **Una farmacia por usuario.** El token sigue llevando una sola farmacia. El email es único en toda la plataforma (es el `UserName` de Identity), así que una persona con dos droguerías usa dos cuentas.

## Alternativas consideradas

- **Registro público ("Registre su farmacia"):** es lo más vistoso para un SaaS, pero exige verificar el correo, frenar bots y NIT ajenos, y probablemente un periodo de prueba con cobro. Se puede agregar después, reutilizando `PlatformPharmacies`.
- **Que el administrador sea un dueño con permisos extra:** mezclaría la plataforma con una farmacia, y el administrador vería datos de negocio que no le corresponden.
- **Varias farmacias por usuario con selector:** es más realista para cadenas, pero exige membresías con un rol por farmacia y reemitir el token al cambiar de farmacia. Hoy no hay cadenas entre los usuarios. Si aparecen, va en un ADR nuevo.
- **Invitación por correo con enlace para crear la contraseña:** es más seguro que una contraseña inicial, pero necesita un servicio de correo. Queda para cuando exista.

## Consecuencias

- **El administrador de producción** no lo crea el seeder (solo corre en Development). En la fase 5 hará falta una forma de crearlo al desplegar, por ejemplo un comando único con la contraseña desde Key Vault.
- **La contraseña inicial la conocen el administrador y el dueño.** Hasta que exista "cambiar contraseña" (paso 9d), el dueño no puede cambiarla.
- **Los mensajes de validación de Identity** (contraseña, email) se muestran en español, con `SpanishIdentityErrorDescriber`.
- **`Down` de la migración `AllowUsersWithoutPharmacy` se detiene** si hay usuarios sin farmacia, en vez de inventarles una.
