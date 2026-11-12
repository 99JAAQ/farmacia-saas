# 0020. Despliegue en Azure con planes gratuitos, identidad administrada y sin contraseñas

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

El proyecto necesita una demo pública para el portafolio del autor, que hoy busca empleo y no tiene presupuesto. Más adelante, la farmacia real podría usarlo a diario. Hay que decidir dónde corre, cuánto cuesta y cómo se protegen los secretos y la base.

## Decisión

- **Demo en planes gratuitos, en Azure:**

  | Pieza | Servicio |
  |---|---|
  | App (API y frontend, ADR 0019) | Azure Container Apps, plan Consumption: cupo gratis mensual de vCPU, memoria y peticiones; escala a cero |
  | Base de datos | Oferta gratuita de Azure SQL Database: 100.000 vCore-segundos y 32 GB al mes; si se acaba, la base **se pausa** en vez de cobrar |
  | Secretos | Azure Key Vault (centavos por operación) |
  | Logs | Log Analytics con un tope diario bajo, para quedar dentro de los 5 GB gratis al mes |
  | Imagen | GitHub Container Registry, gratis para imágenes públicas |
  | Red de seguridad | Un presupuesto de USD 5 al mes que avisa por correo al 50 % y al 100 %, real o pronosticado |

- **Sin contraseñas en ningún lado:**
  - **La app es una identidad administrada (user-assigned).** Con ella lee sus secretos de Key Vault (rol *Key Vault Secrets User*) y se conecta a SQL (`Authentication=Active Directory Managed Identity`).
  - **El servidor SQL solo acepta Microsoft Entra ID.** No hay usuario `sa` ni contraseñas de SQL.
  - **GitHub Actions entra a Azure con OIDC (federated credentials):** no guarda ningún secreto de Azure.
- **Usuario de base de datos con el mínimo:** la identidad de la app tiene solo `db_datareader` y `db_datawriter` (`infra/sql/grant-app-user.sql`). Así, con las credenciales de la app no se puede alterar el esquema ni desactivar la política de Row-Level Security. Esto cierra el pendiente del ADR 0014, en el que la API se conectaba como `sa`. Las migraciones las aplica el administrador de Entra del servidor, desde el pipeline.
- **Una réplica como máximo:** los grupos de SignalR viven en la memoria del proceso (ADR 0017). Con dos réplicas, una venta en una no avisaría a las pantallas conectadas a la otra.
- **La infraestructura como código (Bicep, `infra/main.bicep`)** se valida y despliega desde el pipeline. Nada se crea a mano en el portal, salvo lo que exige la cuenta: la suscripción y el registro de la aplicación de GitHub en Entra.

## Alternativas consideradas

- **App Service Free (F1):** tiene 60 minutos de CPU al día, sin "always on" y con límites de WebSockets. Container Apps da más cupo y escala a cero.
- **Static Web Apps + Container Apps:** dos dominios distintos (ver ADR 0019).
- **Usuario y contraseña de SQL en Key Vault:** funcionaría, pero es una contraseña más que rotar y que se puede filtrar. La identidad administrada no tiene secreto.
- **Endpoint privado para SQL:** cierra la red, pero cuesta unos USD 7 al mes. Con autenticación solo de Entra, la regla "permitir servicios de Azure" es un riesgo aceptable para la demo.
- **Azure SignalR Service o Redis como backplane:** permitirían varias réplicas, pero una farmacia no las necesita. Cuando haga falta, es un cambio de configuración (`AddAzureSignalR`).

## Consecuencias

- **La primera visita después de un rato sin uso tarda:** la app arranca desde cero en unos segundos, y la base pausada puede tardar hasta un minuto en reanudarse (`Connect Timeout=90`). Para la demo es aceptable; para la farmacia real, no.
- **Una pestaña abierta mantiene viva la réplica** (el WebSocket es una conexión activa) y consume el cupo gratis. El presupuesto avisa si empieza a costar.
- **Para la farmacia real** se cambia de plan, no de código: la app siempre encendida (`minReplicas: 1`, o App Service B1) y la base sin pausa (Azure SQL Basic). Son unos USD 20 al mes, a confirmar con la calculadora de Azure. Además, conviene tener lista la fase 6 (vender sin internet).
- **El primer administrador de la plataforma** se crea al primer arranque (`ProductionBootstrap`), con la contraseña que el pipeline genera en Key Vault. Hay que cambiarla al entrar.
