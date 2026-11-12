# 0017. Tiempo real con SignalR: avisos delgados por farmacia, enviados después del commit

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

En la farmacia trabajan varias personas a la vez: el cajero vende, el bodeguero recibe mercancía y el dueño mira cómo va el día. Hasta ahora cada pantalla mostraba los datos del momento en que se abrió. Por ejemplo, el bodeguero no veía bajar el stock mientras el cajero vendía. La fase 4 agrega tiempo real con tres usos:

- un panel del día para el dueño;
- alertas de stock bajo y de vencimientos;
- pantallas que se refrescan solas.

## Decisión

- **SignalR, con un hub (`/hubs/pharmacy`) y un grupo por farmacia.** El grupo sale del claim `pharmacy_id` del token firmado, nunca del cliente, así que una farmacia solo oye sus propios cambios. El hub no tiene métodos que el cliente pueda llamar: solo envía avisos.
- **Quién se conecta:** la política `UsePharmacy`, la misma regla que la de respaldo. Exige un usuario de una farmacia sin contraseña temporal; el administrador de la plataforma y los anónimos quedan fuera.
- **El token viaja en la URL solo para el hub.** El navegador no puede enviar cabeceras al abrir un WebSocket, así que el cliente de SignalR manda `?access_token=`. La API lo acepta únicamente en `/hubs/pharmacy`; en la API REST un token en la URL se ignora. Un test lo comprueba.
- **Avisos delgados:** un aviso es un tema y, a veces, un Id (`{ topic: "stock", id: <producto> }`), sin datos. El navegador vuelve a pedir por REST lo que le interesa (TanStack Query invalida esas consultas). Así hay una sola fuente de verdad, y los permisos se aplican donde siempre: un cajero nunca recibe costos, porque el aviso no los lleva.
- **Se envían después del commit:** el caso de uso agrega avisos a `IRealtimeNotifications`, un búfer por request. `PublishRealtimeChangesBehavior`, un pipeline de MediatR, los envía solo si el caso de uso terminó bien, es decir, si su transacción se confirmó. Si falló, los descarta.
- **El envío es best effort:** si SignalR falla, la venta ya guardada no falla, y el error queda en el log. El navegador refresca todo al reconectarse. El tiempo real es una comodidad; la base es la verdad.

## Alternativas consideradas

- **Mensajes con los datos completos** (la venta, el total del día): se ahorran peticiones, pero habría que filtrar por rol qué recibe cada quien y mantener dos formas de entregar los mismos datos.
- **Outbox** (guardar los avisos en una tabla en la misma transacción y enviarlos con un proceso aparte): nunca se pierde un aviso. Es más robusto, pero también más trabajo, y aquí perder un aviso solo retrasa un refresco. Puede llegar con la sincronización offline (fase 6).
- **Polling** (preguntar cada N segundos): es más simple, pero multiplica las peticiones por pantalla abierta y siempre llega tarde.
- **Publicar desde cada handler justo después del commit:** obliga a cada caso de uso a acordarse y a manejar los errores de envío. El pipeline lo resuelve en un solo lugar.

## Consecuencias

- **Una conexión dura más que su access token:** SignalR solo autentica al conectar. Un usuario desactivado podría seguir recibiendo avisos hasta reconectarse. Como los avisos no llevan datos, no ve nada que no pudiera ver; y al pedir los datos por REST, el token vencido o revocado ya no sirve.
- **Con varias instancias de la API** (fase 5), los grupos de SignalR necesitan un backplane, como Azure SignalR Service o Redis. Hoy hay una sola instancia.
- **Los avisos repetidos en un mismo request se envían una vez,** por ejemplo dos líneas del mismo producto.
- **Los tests de los casos de uso no envían nada** (`NoRealtimePublisher`). Los de la API usan un cliente SignalR real, y prueban el aislamiento entre farmacias y quién puede escuchar.
