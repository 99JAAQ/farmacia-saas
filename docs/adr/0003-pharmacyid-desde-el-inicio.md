# 0003. `PharmacyId` en toda entidad desde el inicio, tomado del JWT

- **Estado:** Aceptada
- **Fecha:** 2026-10-22

## Contexto

El primer cliente es una sola farmacia, pero el producto es un SaaS: en la fase 3 varias farmacias compartirán la instalación. Agregar el tenant a tablas que ya tienen datos exige migraciones dolorosas y revisar cada consulta.

## Decisión

- **Toda entidad del negocio hereda de `TenantEntity`**, que exige un `PharmacyId`. Cada tabla tiene una FK a `Pharmacies` sin cascada.
- **El `PharmacyId` de una petición sale del claim `pharmacy_id` del JWT firmado**, mediante `ICurrentPharmacy`. Nunca viene del body ni de la URL.
- **Los repositorios reciben el `PharmacyId` en cada método.**
- **Un recurso de otra farmacia responde 404, nunca 403.**

## Alternativas consideradas

- **Agregar el tenant en la fase 3:** obligaría a una migración con datos y a revisar todas las consultas.
- **Una base de datos por farmacia:** el aislamiento es más fuerte, pero opera más caro (migraciones, respaldos y conexiones por farmacia). Se puede reconsiderar si un cliente grande lo exige.
- **Filtros globales de consulta de EF Core** (`HasQueryFilter`): son cómodos, pero el filtro queda oculto y es fácil desactivarlo sin darse cuenta. Se prefirió que el parámetro sea explícito en cada repositorio.

## Consecuencias

- **La fase 3 no necesita migrar el esquema de lo que ya existe.**
- **El aislamiento se prueba por HTTP**, con dos farmacias en los tests de la API.
- **Cada repositorio y cada test arrastran el `PharmacyId`.**
- **Los índices únicos incluyen el `PharmacyId`** (NIT de proveedor, código de barras…): dos farmacias pueden tener el mismo proveedor.
