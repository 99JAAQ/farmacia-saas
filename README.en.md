# Farmacia SaaS

Management software for pharmacies in Colombia: batch and expiration tracking, supplier purchases, shift-based cash register and, coming next, a point of sale (POS). Built for a real pharmacy and designed as a multi-tenant SaaS: every pharmacy only sees its own data.

> The full documentation is in Spanish, in [`docs/`](docs/README.md). This page is a short summary.

## Features

- **Catalog**: products sold in several units (box, blister, tablet), each with its own conversion factor, price and barcode.
- **Purchases**: supplier invoices typed in as drafts and confirmed when the goods arrive. They support bonus units, line and invoice discounts, mixed VAT rates (0%, 5%, 19%) and a check against the printed invoice total. Confirming creates one batch per line with its real unit cost.
- **Inventory**: stock per batch with a full stock ledger (kardex) and FEFO allocation (first expired, first out).
- **Cash register**: one register, several shifts a day, with an opening float, cash in/out movements and a **blind count** at closing (the cashier does not see the expected amount before counting).
- **Roles**: owner, cashier and stock keeper, enforced by named authorization policies.

## Architecture

Clean Architecture with the dependencies always pointing inwards: `Api → Infrastructure → Application → Domain`. The domain has no dependencies and holds the business rules. Use cases are MediatR commands and queries, and they access data through one repository per aggregate. Every business entity carries a `PharmacyId`, which always comes from the signed JWT and never from the request body, so tenant isolation is built in. Ids are Guid v7 generated in the domain, so the offline POS planned for a later phase can create records without collisions.

## Stack

.NET 10 · ASP.NET Core · EF Core 10 · SQL Server 2022 · MediatR · ASP.NET Core Identity + JWT · React 19 · TypeScript · Vite · TanStack Query · Tailwind + shadcn/ui · xUnit + Testcontainers · Vitest

## Running locally

Requirements: .NET 10 SDK, Node.js 22+ and Docker. The steps are in the Spanish [README](README.md#arrancar-en-5-minutos): start SQL Server with `docker compose up -d`, set two user secrets, apply the migrations, then run `dotnet run --project src/Farmacia.Api` and `npm run dev` in `web/`.

## Tests

`dotnet test` runs the domain and use-case unit tests, plus integration tests against a disposable SQL Server (Testcontainers) and the whole API in memory. `npm test` in `web/` runs the frontend tests.
