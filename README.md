# Subscript

**Subscript — Agentic Coding Experiment with Freebuff & DeepSeek**

An experiment in agentic coding: building a SaaS subscription management system using
Freebuff and DeepSeek, exploring AI-driven code generation, project structure, and
implementation workflows.

The system covers customers, plans, subscriptions, invoices, payments, and automated
recurring billing.

## Contents

- [Stack](#stack)
- [Solution Layout](#solution-layout)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [API](#api)
- [Admin Console](#admin-console-nextjs)
  - [Pages](#pages)
  - [Sessions and CSRF](#sessions-and-csrf)
  - [BFF Proxy](#bff-proxy)
  - [Data Fetching](#data-fetching)
- [Automated Billing](#automated-billing)
- [Business Rules](#business-rules-v1)
- [Demo Data](#demo-data)
- [Database Migrations](#database-migrations)
- [Tests](#tests)
- [Configuration](#configuration)
- [MCP Tools (Agents)](#mcp-tools-agents)

## Stack

- .NET 10 / ASP.NET Core Web API
- PostgreSQL 17 + EF Core 10
- RabbitMQ 4 with a transactional outbox and an in-process consumer
- Next.js 15 Admin + BFF (session cookie ↔ browser, API key ↔ API)
- xUnit + Testcontainers
- Docker Compose

## Solution layout

```
src/
  Subscript.Api/                 # Controllers, API-key auth, middleware, DI composition
  Subscript.Application/         # Use cases, DTOs, paging, billing orchestration
  Subscript.Domain/              # Entities, status transitions, billing date rules
  Subscript.Infrastructure/      # EF Core, migrations, RabbitMQ worker, outbox, clock
tests/
  Subscript.Domain.UnitTests/
  Subscript.Application.UnitTests/
  Subscript.IntegrationTests/
apps/
  admin/                         # Next.js admin console + BFF
docker/
  docker-compose.yml             # API + PostgreSQL + RabbitMQ
  Dockerfile.api
```

## Requirements

- .NET SDK 10
- Docker (PostgreSQL, RabbitMQ, and Testcontainers)
- Node.js 20+ (admin app)

## Quick start

1. Start the API, PostgreSQL, and RabbitMQ with Compose:

   ```bash
   docker compose -f docker/docker-compose.yml up -d --build
   ```

   The Compose project name and container names are pinned in `docker/docker-compose.yml`
   (`name: subscript`), so the services are always `subscript-api`, `subscript-postgres`,
   and `subscript-rabbitmq` no matter which directory you run the command from.

   | Service | Host port | In Compose |
   |---------|-----------|------------|
   | API | `8080` | `8080` |
   | PostgreSQL | `5433` | `5432` |
   | RabbitMQ | `5673` (UI `15673`) | `5672` (UI `15672`) |

   The host ports intentionally avoid `5432`/`5672`, which local Postgres.app/Homebrew
   services commonly occupy; inside Compose the API still uses `postgres:5432` and
   `rabbitmq:5672`. If you point the API at another PostgreSQL on `5432`, expect
   `role "subscript" does not exist`.

   In `Development` the API applies migrations and seeds demo data on startup:

   - API: `http://localhost:8080`
   - Swagger: `http://localhost:8080/swagger`
   - Health (Postgres + RabbitMQ): `http://localhost:8080/health`

   To run the API on the host instead of Compose, use
   `dotnet run --project src/Subscript.Api`; it serves on `http://localhost:5226` (see
   `Properties/launchSettings.json`), with Swagger at `/swagger` and health at `/health`.

2. Restore the EF Core CLI used for migrations:

   ```bash
   dotnet tool restore
   ```

3. Run the admin console:

   ```bash
   cd apps/admin
   cp .env.example .env.local
   npm install
   npm run dev
   ```

   `API_BASE_URL` must point at the port the API actually listens on:
   `http://localhost:8080` when the API runs in Compose, `http://localhost:5226` when it
   runs on the host. The admin never calls the API from the browser; the BFF proxy
   (`src/app/api/backend/[...path]/route.ts`) forwards `/api/backend/{path}` to
   `{API_BASE_URL}/api/v1/{path}` on the server, so a stale port makes every dashboard
   request fail with a connection error even though the API is healthy.

   Open `http://localhost:3000`. The example credentials are `admin` / `change-me`;
   set `SESSION_PASSWORD`, `ADMIN_USERNAME`, `ADMIN_PASSWORD`, `API_BASE_URL`, and
   `API_KEY` in `.env.local` before using it outside local development.

Direct API calls use the API key (against either origin):

```http
X-Api-Key: dev-api-key-change-me
```

## API

All routes are versioned under `/api/v1`, return `application/problem+json` on errors,
and accept page parameters (`page`, `pageSize`, max 200) on collection endpoints.

| Method | Route | Notes |
|--------|-------|-------|
| GET/POST | `/customers` | Paged, `search`, `includeDeleted` |
| GET/PUT | `/customers/{id}` | Update details |
| POST | `/customers/{id}/deactivate` | Soft delete |
| GET/POST | `/plans` | Paged, `includeDeleted` |
| GET/PUT/DELETE | `/plans/{id}` | Delete = soft deactivate |
| GET/POST | `/subscriptions` | Paged, `status`, `customerId` filters |
| GET | `/subscriptions/{id}` | Include `includeDeleted` if needed |
| POST | `/subscriptions/{id}/cancel` | Immediate cancellation |
| POST | `/subscriptions/{id}/change-plan` | Effective at next billing date |
| DELETE | `/subscriptions/{id}` | Soft delete |
| GET | `/customers/{id}/subscriptions` | Paged |
| GET | `/invoices` | Paged, `status`, `subscriptionId` filters |
| GET | `/invoices/{id}` | |
| POST | `/invoices/{id}/cancel` | Only `Open` invoices |
| GET | `/subscriptions/{id}/invoices` | Paged |
| POST | `/payments` | Record succeeded/failed payment |
| GET | `/payments` | Paged, `invoiceId`, `status` filters |
| GET | `/invoices/{id}/payments` | Payment history |
| POST | `/billing/run` | Manual billing trigger (dev/ops) |

## Admin console (Next.js)

`apps/admin` is a separate Next.js 15 App Router application. It contains the admin UI
and a small BFF: the browser only talks to Next.js, and Next.js calls the .NET API with
the server-side API key.

```text
Browser
  │  fetch("/api/backend/customers")
  ▼
Next.js route handler (apps/admin/src/app/api/backend/[...path]/route.ts)
  │  session + CSRF check, adds X-Api-Key
  ▼
Subscript.Api  →  /api/v1/customers
  │
  ▼
PostgreSQL
```

All browser requests are same-origin, so the admin app does not need CORS.

### Pages

| Route | Purpose |
|-------|---------|
| `/login` | Sign in with the configured admin credentials |
| `/` | Dashboard: counts, recent subscriptions, open invoices, manual billing run |
| `/customers` | Search, paging, create, deactivate |
| `/customers/{id}` | Edit customer, start/cancel subscriptions, schedule plan changes |
| `/plans` | Create, edit, deactivate plans |
| `/subscriptions` | Status filter, paging, cancellation |
| `/invoices` | Status/subscription filters, record payment, cancel |
| `/invoices/{id}` | Invoice details, payment history, record succeeded/failed payment |

The `(dashboard)` directory is a route group: it does not appear in the URL, but its
layout protects every page inside it.

### Sessions and CSRF

1. `POST /api/auth/login` compares the submitted credentials with `ADMIN_USERNAME` and
   `ADMIN_PASSWORD`.
2. On success, `iron-session` stores `isAuthenticated`, `username`, and a random
   `csrfToken` in the encrypted, `httpOnly` `subscript_admin_session` cookie
   (`sameSite=lax`, `secure` in production).
3. The dashboard layout checks the session on each render and redirects to `/login`
   when it is missing.
4. `/api/auth/me` returns the current CSRF token. Before every non-GET request,
   `src/lib/api.ts` fetches it and sends it as `x-csrf-token`; the BFF compares it with
   the session and returns `403` on a mismatch. Fetching it per mutation keeps it valid
   after signing in again.
5. `POST /api/auth/logout` destroys the session.

This is a single shared admin account for an internal console; there are no per-user
roles or database-backed users in v1.

### BFF proxy

`src/app/api/backend/[...path]/route.ts`:

- Requires an authenticated session (`401` otherwise).
- Rejects non-GET/HEAD requests without a matching CSRF token (`403`).
- Maps `/api/backend/{path}` to `/api/v1/{path}`, preserving the query string, method,
  and JSON body.
- Adds `X-Api-Key` on the server and returns the upstream status and content type.
- Uses `cache: "no-store"`; the browser never receives `API_KEY` or `API_BASE_URL`.

### Data fetching

The login page and dashboard layout are server components. Dashboard pages are client
components (`"use client"`) that load data after mount through `api()`, usually with
`Promise.all` for related collections. Collection endpoints use the paged envelope
`{ items, page, pageSize, totalCount, totalPages }`, so searching, filtering, and paging
happen in the API. Mutations call `api()` again and then reload the current view.

Admin configuration is read only on the server from `apps/admin/.env.local`:

| Variable | Purpose |
|----------|---------|
| `SESSION_PASSWORD` | Encrypts the session cookie; at least 32 characters |
| `ADMIN_USERNAME` | Shared admin username |
| `ADMIN_PASSWORD` | Shared admin password |
| `API_BASE_URL` | .NET API origin: `http://localhost:8080` (Compose) or `http://localhost:5226` (`dotnet run`) |
| `API_KEY` | Must match `Authentication:ApiKey`; never exposed to the browser |

See [Quick start](#quick-start) for the local run commands. For a production build,
run `npm ci && npm run build` in `apps/admin`.

## Automated billing

Billing is idempotent and runs through RabbitMQ:

1. `BillingScheduler` enqueues a `BillingDue` record in `outbox_messages` on startup and
   then every `Billing:SchedulerIntervalSeconds` (default 60).
2. `OutboxDispatcher` publishes pending outbox records to the durable
   `subscript.billing` queue and marks them processed only after the broker accepts them.
3. `BillingWorker` consumes the command and calls `BillingService`, which finds due
   `Active`/`Trialing` subscriptions, creates one invoice per period, and advances the
   subscription window.

Safety nets:

- Unique `(SubscriptionId, PeriodStart, PeriodEnd)` invoice index prevents double billing.
- `POST /api/v1/billing/run` remains available for manual runs.
- Consumer failures are logged and the message is rejected; outbox publishing retries.
- Billing can be disabled for API tests with `Messaging:Enabled=false` and
  `Billing:SchedulerEnabled=false`.

If RabbitMQ is unavailable the API still serves requests; health turns unhealthy and
the worker/dispatcher retry with backoff.

## Business rules (v1)

- Single-tenant, USD only
- Customer email globally unique (case-insensitive), including soft-deleted customers
- One `Active`/`Trialing` subscription per customer (partial unique index, includes soft-deleted rows)
- Trial = 30 days; calendar month/year billing with end-of-month anchoring
- Manual cancel → immediate `Cancelled`
- Payment failure → immediate `Expired` (no grace or retry)
- Payments recorded manually (no PSP in v1); only one successful payment per invoice
- Plan change deferred to the next billing date, no proration
- Invoice price/name/billing period snapshotted at generation time
- Invoice GUID PK; `InvoiceNumber` reserved for future GİB integration
- BFF session cookie ↔ browser; API key (server environment) ↔ API

## Demo data

When `Database:SeedOnStartup` is true (Development by default), an empty database is
seeded with three plans (Starter/Growth/Scale), three customers, and three
subscriptions: one due for billing (its first invoice is generated by the scheduler),
one in trial, and one cancelled.

## Database migrations

```bash
dotnet ef migrations add <Name> \
  --project src/Subscript.Infrastructure \
  --startup-project src/Subscript.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/Subscript.Infrastructure \
  --startup-project src/Subscript.Api
```

## Tests

```bash
dotnet test
```

- Domain unit tests cover billing date math and status transitions.
- Application tests run services against EF Core InMemory: uniqueness, trial conversion,
  idempotent billing, plan changes, payments, and invoice cancellation.
- Integration tests use Testcontainers PostgreSQL and `WebApplicationFactory` to cover
  the HTTP contract, paging, billing runs, payments, and authorization.

Admin checks:

```bash
cd apps/admin
npm ci
npm run build
```

CI runs the .NET build/tests and the admin production build.

## Configuration

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL |
| `ConnectionStrings:RabbitMq` | RabbitMQ AMQP URI |
| `Authentication:ApiKey` | API key expected in `X-Api-Key` |
| `Database:ApplyMigrationsOnStartup` | Apply EF migrations at boot |
| `Database:SeedOnStartup` | Insert demo data when the database is empty |
| `Billing:SchedulerEnabled` | Enable the background scheduler |
| `Billing:SchedulerIntervalSeconds` | Scheduler interval (default 60) |
| `Messaging:Enabled` | Enable outbox dispatcher + worker |
| `Messaging:QueueName` | Billing queue (default `subscript.billing`) |

Admin environment variables live in `apps/admin/.env.local`; see
`apps/admin/.env.example`. Never commit that file or real credentials.

## MCP tools (agents)

Agent tooling is configured in `.agents/mcp.json`. That file holds a GitHub personal
access token and the database password, so it is **gitignored** and never committed —
every developer keeps their own local copy.

Copy the template and fill in the secrets:

```bash
cp .agents/mcp.example.json .agents/mcp.json
```

Then export the two secrets (or paste them into your local `mcp.json`):

| Variable | Purpose |
|----------|---------|
| `GITHUB_PERSONAL_ACCESS_TOKEN` | Authenticates `ghcr.io/github/github-mcp-server` |
| `POSTGRES_CONNECTION_STRING` | e.g. `postgresql://postgres:<password>@localhost:5433/subscript` |

The example also ships `/absolute/path/to/Subscript` for the filesystem server's root;
replace it with your checkout path.

Connected servers and their capabilities:

| Server | Tools | Purpose |
|--------|-------|---------|
| `github` | 46 | Repos, files, commits, pull requests, issues, releases, code search |
| `filesystem` | 14 | Read/write files, scoped to the repository root |
| `postgres` | 13 | Query and inspect the database, bulk CSV load, perf metrics |
| `docker` | 8 | MCP gateway management (find/add/remove servers, profiles) |

Note that the `postgres` profile is configured read-write, so `modify` and
`bulk_load_csv` can change the `subscript` database. Point it at a disposable database
when experimenting.
