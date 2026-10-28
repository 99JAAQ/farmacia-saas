# 0005. SQL Server en Docker, EF Core con Fluent API y tests con Testcontainers

- **Estado:** Aceptada
- **Fecha:** 2026-10-22

## Contexto

El despliegue será en Azure (fase 5), donde SQL Server (Azure SQL) es la opción natural. En desarrollo, la máquina ya tiene un SQL Server instalado en el puerto 1433. Los tests deben probar lo que hace SQL Server de verdad: índices únicos, transacciones, concurrencia, intercalaciones.

## Decisión

- **SQL Server 2022 en Docker para desarrollo** (`docker-compose.yml`), en el puerto **14330** del host. La contraseña va en `.env`.
- **EF Core con Fluent API** en `Infrastructure/Persistence/Configurations`. El dominio no tiene atributos de persistencia.
- **Migraciones de EF Core versionadas en el repositorio.**
- **Los tests de integración usan Testcontainers:** un SQL Server desechable por ejecución, con las migraciones reales aplicadas.

## Alternativas consideradas

- **PostgreSQL:** es igual de capaz y gratuito, pero Azure SQL encaja mejor con el resto del stack de Microsoft y con el despliegue planeado.
- **El SQL Server instalado en la máquina:** cada desarrollador lo tendría configurado distinto, y los tests ensuciarían la base de desarrollo.
- **EF Core InMemory o SQLite para los tests:** son rápidos, pero no reproducen el comportamiento de SQL Server (índices filtrados, `rowversion`, intercalaciones sin tildes). Darían falsa confianza.
- **Atributos de datos en las entidades** (`[MaxLength]`, `[Key]`): acoplarían el dominio a EF Core.

## Consecuencias

- **Arrancar la base es un solo comando** (`docker compose up -d`), igual en todas las máquinas.
- **Los tests de integración prueban la base real.** Por ejemplo, detectaron que el índice de "un turno abierto" necesita su filtro.
- **Docker es obligatorio** para desarrollar y para correr toda la suite.
- **Los tests de integración tardan unos segundos más**, porque cada ejecución arranca un contenedor.
