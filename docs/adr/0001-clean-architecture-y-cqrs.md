# 0001. Clean Architecture con CQRS (MediatR) y repositorios por agregado

- **Estado:** Aceptada
- **Fecha:** 2026-10-22

## Contexto

La aplicación tiene reglas de negocio no triviales: costo real de una compra con bonificaciones y descuentos, FEFO, cierre de caja. Además, en la fase 6 el POS debe vender sin conexión, así que las reglas no pueden depender de la base de datos ni de HTTP. Es también un proyecto de portafolio: la estructura debe ser fácil de explicar.

## Decisión

- **Cuatro proyectos con las dependencias hacia adentro:** `Api → Infrastructure → Application → Domain`.
- **Cada caso de uso es un command o una query de MediatR**, con su handler, organizado por funcionalidad (`Purchases/ConfirmPurchase`).
- **Application accede a los datos con un repositorio por agregado** (`IPurchaseRepository`…) más `IUnitOfWork`, y no referencia EF Core.
- **Cada repositorio recibe siempre el `PharmacyId`.**

## Alternativas consideradas

- **Un solo proyecto con controllers que usen el `DbContext` directamente:** es más rápido al principio, pero mezcla las reglas con SQL y HTTP, y no permitiría reutilizar el dominio offline.
- **Application usando el `DbContext` directamente**, sin repositorios: tiene menos código, pero los handlers solo se podrían probar con una base de datos, y el aislamiento por farmacia dependería de acordarse de filtrar en cada consulta.
- **Organizar por tipo técnico** (`Commands/`, `Handlers/`, `Dtos/`): para entender una funcionalidad habría que abrir archivos en cinco carpetas.

## Consecuencias

- **Las reglas se prueban en milisegundos**, sin infraestructura: los tests de dominio y los de casos de uso usan fakes en memoria.
- **El aislamiento por farmacia está en la firma de los repositorios.**
- **Hay más archivos y más ceremonia** (una interfaz por repositorio, un command por acción) que en un CRUD simple.
- **Los fakes en memoria no reproducen todo lo de SQL Server** (intercalaciones, índices únicos). Eso se prueba en los tests de Infrastructure y de la Api contra un SQL Server real.
