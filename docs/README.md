# Documentación de Farmacia SaaS

Esta carpeta explica cómo está construida la aplicación y por qué. Está escrita para quien llega al equipo de desarrollo.

## Por dónde empezar

| Si necesita… | Lea |
|---|---|
| Entender el sistema en 15 minutos | [Arquitectura](arquitectura.md) |
| Saber dónde está cada cosa y dónde poner algo nuevo | [Estructura de carpetas](estructura-carpetas.md) |
| Correr el proyecto, depurarlo y ejecutar los tests | [Desarrollo](desarrollo.md) |
| Desplegar en Azure: la demo, el pipeline y la configuración inicial | [Despliegue](despliegue.md) |
| Entender el negocio: lotes, FEFO, compras, caja | [Dominio](dominio.md) |
| Ver las tablas, relaciones e índices | [Base de datos](base-de-datos.md) |
| Entender el login, los roles y el aislamiento entre farmacias | [Seguridad](seguridad.md) |
| Trabajar en la aplicación React | [Frontend](frontend.md) |
| Saber por qué se tomó una decisión de diseño | [Decisiones de arquitectura (ADR)](adr/README.md) |

## Estado del proyecto

| Fase | Contenido | Estado |
|---|---|---|
| 1. Base | Estructura, dominio, EF Core, auth JWT con roles, React con login | Completa |
| 2. Núcleo | Compras, inventario por lotes, caja, POS, devoluciones, fiado, cuentas por pagar | Completa |
| 3. Multi-tenant | Varias farmacias, Row-Level Security, administrador de la plataforma, usuarios | Completa |
| 4. Tiempo real | SignalR: pantallas en vivo, panel del día, alertas de stock y vencimientos | Completa |
| 5. Calidad e infra | Docker, CI/CD con GitHub Actions, tests end to end, despliegue en Azure | Completa: la demo está publicada |
| 6. Offline-first | El POS vende sin internet y sincroniza después | Pendiente |
| 7. Presentación | Demo desplegada, diagramas, video | Pendiente |

## Cómo mantener esta documentación

- **La documentación cambia en el mismo commit que el código.** Si cambia una regla de negocio, una tabla o una convención, actualice el documento correspondiente en ese mismo commit.
- **Las decisiones importantes van en un ADR nuevo.** Un ADR no se reescribe: si una decisión cambia, se crea otro que reemplaza al anterior.
- **Los diagramas están en [Mermaid](https://mermaid.js.org/):** son texto, así que GitHub y VS Code los dibujan, y los cambios se revisan como cualquier otro cambio.
