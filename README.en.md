# Farmacia SaaS

[![CI](https://github.com/99JAAQ/farmacia-saas/actions/workflows/ci.yml/badge.svg)](https://github.com/99JAAQ/farmacia-saas/actions/workflows/ci.yml)

Management software for pharmacies in Colombia: point of sale, shift-based cash register, batch and expiration tracking, supplier purchases and store credit. It was built for a real pharmacy and designed as a multi-tenant SaaS. Every pharmacy only sees its own data, which is enforced by SQL Server as well as by the code.

> The full documentation is in Spanish, in [`docs/`](docs/README.md). This page is a short summary.

**Live demo:** https://ca-farmacia.politemoss-136a2eb5.centralus.azurecontainerapps.io. It runs on Azure's free tiers, so the first visit after a while takes a few seconds to wake up.

## Features

- **Point of sale:**
  - works with a barcode scanner;
  - sells by box, blister or single tablet;
  - takes mixed payments (cash, card, mobile wallets, transfer, store credit);
  - prints an 80 mm thermal ticket;
  - sells first what expires first (FEFO).
- **Cash register:** several shifts a day. Each shift closes with a **blind count**: the cashier counts the cash without seeing the expected amount.
- **Returns:** by line and quantity, with a cash refund. Returned items go back to their batch or are discarded.
- **Store credit:** a credit limit per customer, payments against the balance and a statement.
- **Purchases and payables:**
  - Supplier invoices support bonus units, discounts and mixed VAT rates.
  - Each invoice line creates a batch with its real unit cost.
  - Payments are recorded per invoice, and overdue invoices are flagged.
- **Inventory:**
  - a stock ledger that records who made each movement;
  - write-offs of expired batches and adjustments after a physical count;
  - alerts for low stock and upcoming expirations.
- **Live updates:** the owner's daily dashboard (sales, gross margin, payment methods), stock levels and alerts update by themselves over SignalR.
- **Multi-tenant:** a platform administrator creates pharmacies and their owners. Each owner creates their own cashiers and stock keepers, who must change their temporary password at first login.

## Worth a look

- **Tenant isolation in two layers:**
  - every repository filters by the tenant taken from the signed JWT;
  - **SQL Server Row-Level Security** blocks any query that forgets the filter.

  Injected SQL cannot change the session context, because it is read-only. ([ADR 0014](docs/adr/0014-row-level-security.md))
- **Authentication:**
  - the API issues its own JWT;
  - the refresh token lives in an HttpOnly, SameSite=Strict cookie and rotates on every use;
  - reusing an old refresh token revokes the whole session.
- **Real time:**
  - SignalR notices carry no data;
  - they are sent only after the database transaction commits;
  - they are derived by an EF Core interceptor from what each save actually wrote, so no use case can forget to send one.
- **Concurrency:**
  - optimistic concurrency (RowVersion) on stock batches and credit limits;
  - gap-free ticket numbers through a single `MERGE … HOLDLOCK` statement.
- **Mutation testing:** every important rule was deliberately broken to prove that its tests fail.
- **Deployment:**
  - a single container serves both the API and the React app from the same origin;
  - the image is chiseled, with no shell, and runs as a non-root user;
  - database migrations run as a separate bundle before the app starts.

## Architecture

Clean Architecture, with dependencies pointing inwards: `Api → Infrastructure → Application → Domain`.

- **Domain:** has no dependencies and holds the business rules.
- **Application:** the use cases are MediatR commands and queries. They access data through one repository per aggregate.
- **Ids:** Guid v7 values generated in the domain, so the offline POS planned for a later phase can create records without collisions.

## Stack

| Area | Technologies |
|---|---|
| Backend | .NET 10, ASP.NET Core, SignalR, MediatR |
| Data | EF Core 10, SQL Server 2022 (Row-Level Security) |
| Auth | ASP.NET Core Identity, JWT |
| Frontend | React 19, TypeScript, Vite, TanStack Query, Tailwind with shadcn/ui |
| Tests | xUnit with Testcontainers, Vitest, Playwright |
| Infrastructure | Docker, GitHub Actions, Azure Container Apps, Azure SQL, Key Vault (Bicep, OIDC, managed identity) |

## Try it

You need Docker. Copy `.env.example` to `.env` and fill in the passwords, then run:

```bash
docker compose --profile app up --build
```

Open http://localhost:8080 and log in as `admin@farmacia.local`. You will be asked to change the password, and then you can create your first pharmacy.

## Tests

- **Backend:** `dotnet test` runs the domain and use-case unit tests. It also runs integration tests against a disposable SQL Server (Testcontainers) and against the whole API in memory.
- **Frontend:** `npm test` in `web/` runs the frontend tests.
- **End to end:** the Playwright tests in `e2e/` drive the production containers in a real browser.

CI runs all three on every push.
