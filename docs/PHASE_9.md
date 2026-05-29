# Phase 9 — Identity & multi-tenancy (Keycloak-core: registration, users, per-tenant secrets)

**Goal:** replace the single shared "operator" credential with a real, multi-tenant
identity system backed by a **self-hosted Keycloak** IdP. Users **register and sign in
through Keycloak** (OpenID Connect); every user belongs to a **tenant**; LLM secrets and
runs are **scoped per tenant**. The API becomes an **OIDC resource server** (validates
Keycloak-issued JWTs); the Web becomes an **OIDC client** (authorization-code flow).

> Chosen mechanism: **self-host Keycloak** (decided 2026-05-29). It runs in `docker-compose`
> beside the existing Postgres, so the stack stays **self-contained and offline-demoable**;
> realms/organizations model tenants natively; registration, password reset, and social/OAuth
> login are built in; it is open-source (fits the repo's OSS-framework direction). External
> cloud OIDC (Google/Entra/Auth0) was rejected as the *core* because it breaks the offline +
> CI-without-external-services guarantee this repo relies on — those federate **into** Keycloak
> in 9.7 instead. See Open questions for the trade-offs.

This phase delivers the "Full multi-tenant" direction from the Horizon-1 backlog
(`docs/architecture/MIGRATION_BACKLOG.md` M5) and supersedes the Phase 8 single-operator,
hand-rolled symmetric JWT (`Api/Auth/JwtAuthExtensions.cs`).

---

## Why this phase

Phase 8 shipped bearer auth, but it is single-operator and hand-rolled:

| Symptom | Root cause (Phase 8) |
| --- | --- |
| No sign-up; one shared password | `POST /auth/token` hardcodes `user == "operator"`; password from `Auth:Bearer:OperatorPassword` |
| No per-user identity / audit | JWT `sub` is the literal string `"operator"`; no user store |
| Symmetric HS256 secret in app config | `JwtAuthExtensions` signs with a shared secret, no key rotation / JWKS |
| LLM keys are global | `app_config` keys (`Anthropic:ApiKey`, …) are one global set, not per-tenant |
| Runs aren't isolated | `pipeline_runs` / `orchestrations` have no `tenant_id` |

Keycloak removes the hand-rolled auth surface and brings registration, RBAC, token rotation
(RS256 + JWKS), and a tenant model for free.

---

## Architecture

```
docker-compose          : + keycloak service (own DB schema in the existing Postgres)
realm "agentic"          : registration ON; clients (web = code flow, api = bearer-only);
                           realm roles (admin, member); tenants via Organizations/groups
Api  (resource server)   : JwtBearer{ Authority=KC realm, Audience=api } — validates RS256 via
                           JWKS; authz by realm role + `tenant` claim; drops /auth/token + HS256
Web  (OIDC client)        : OpenIdConnect auth-code flow → redirect to KC for login/registration;
                           replaces the custom LoginOverlay POST; tokens in the auth cookie
Application/Identity      : ITenantContext (from `tenant` claim), ITenantSecretStore
Infrastructure            : app_config + runs keyed by tenant_id; KC realm bootstrap (import JSON)
```

**Tenant model.** A tenant = a Keycloak **Organization** (KC 25+) or group, surfaced as a
`tenant` claim via a protocol mapper. `ITenantContext` reads it; every repo query and every
`app_config` read filters by it. First member of a new tenant gets the `admin` role; later
members get `member`.

**Why this is offline-safe.** Keycloak is a container, not a cloud service — `docker compose up`
brings it up locally; CI spins the same container. The `MockLlmClient` demo path is unchanged.
A `Auth:Mode = operator | keycloak` flag keeps the Phase 8 path for environments without KC
(e.g. a fast unit-test run) until cut-over completes.

---

## Acceptance criteria

| # | Criterion | How verified |
| --- | --- | --- |
| 1 | `docker compose up` starts Keycloak with the `agentic` realm pre-imported | KC admin console reachable; realm present |
| 2 | A visitor can self-register via Keycloak and land in a tenant | Register on KC page → user in realm; first user = tenant admin |
| 3 | API validates KC RS256 tokens via JWKS; rejects others with `401` | curl with KC token → `200`; bad/HS256 token → `401` |
| 4 | JWT carries `sub`, `tenant`, realm `roles` | Decode the KC access token |
| 5 | Cross-tenant access denied | tenant-A token vs tenant-B run → `403` |
| 6 | LLM secrets stored + read per tenant | Set Anthropic key as tenant A; tenant B still unset |
| 7 | Web signs in through KC (auth-code flow), not the custom POST | Network panel shows redirect to `/realms/agentic/protocol/openid-connect/auth` |
| 8 | Offline demo + CI pass (KC container in CI; Mock LLM) | `dotnet test` green; demo registers + runs against `MockLlmClient` |
| 9 | Phase 8 operator path still works behind `Auth:Mode=operator` until cut-over | toggle flag → old flow boots |
| 10 | README + this doc updated; phase table ticks Phase 9 | `git log` |

---

## Sub-tasks (staged — each is its own PR)

### 9.1 — Keycloak in the stack + realm bootstrap (foundation, additive)  ⏳ first increment
- Add a `keycloak` service to `docker-compose.yml` (uses the existing Postgres, own schema).
- Author `infra/keycloak/agentic-realm.json`: realm `agentic`, **registration enabled**, clients
  `agentic-web` (confidential, auth-code) + `agentic-api` (bearer-only), realm roles
  `admin`/`member`, a `tenant` protocol mapper, and a seeded admin.
- `docs/SETUP.md` + README: how to bring KC up and reach the admin console.
- No app code changes yet — additive infra, fully revertible.

### 9.2 — API as OIDC resource server
- Replace `AddJwtAuth` HS256 wiring with `AddJwtBearer{ Authority, Audience, MetadataAddress }`
  pointed at the realm (RS256 + JWKS).
- Delete `POST /auth/token` (KC issues tokens); keep it behind `Auth:Mode=operator` until 9.x cut-over.
- Authorization policies: `Admin` / `Member` from realm roles; a tenant-match requirement.

### 9.3 — Web as OIDC client
- Add `AddAuthentication().AddOpenIdConnect()` (auth-code flow) to the Web host; cookie holds tokens.
- Replace the custom `LoginOverlay` POST with a KC redirect; "Register" links to the KC registration page.
- Forward the access token as the bearer for `HttpPipelineClient` (replaces `AuthSession` localStorage JWT).

### 9.4 — Tenant context + per-tenant secret scoping
- `ITenantContext` from the `tenant` claim; add `tenant_id` to `app_config`; key reads/writes by tenant.
- Migrate Phase 8 global keys → a `default` tenant. `/settings` per-tenant; only `admin` writes.

### 9.5 — Data isolation
- Add `tenant_id` to `pipeline_runs` + `orchestrations`; filter all repo queries; backfill `default`.

### 9.6 — Onboarding UX
- Tenant creation/selection on first sign-in; Web user menu shows tenant + roles; admin-only controls gated.

### 9.7 — Federated social/external login (optional)
- Configure Google / Entra ID as **Keycloak identity providers** (federated *into* KC) — KC-native,
  no app code. Gated so offline/CI skip it.

### 9.8 — Docs sweep + cut-over
- Flip default `Auth:Mode=keycloak`, remove the Phase 8 operator path, update README + this doc.

---

## Backout

9.1 is additive (a container + a realm import) and touches no app code — revert by dropping the
compose service. 9.2/9.3 are the cut-over; the `Auth:Mode=operator|keycloak` flag keeps the Phase 8
path bootable until KC is verified in staging, then 9.8 removes it.

## Open questions

1. **Tenant = realm vs Organization/group.** Realm-per-tenant gives the hardest isolation but heavy
   ops at scale; Keycloak **Organizations** (one realm, multi-tenant) is the modern fit and the
   plan's default. Revisit if strict per-tenant realm config is needed.
2. **Keycloak version + hosting.** Pin a KC image tag; dev/CI via compose. Production hosting (its own
   Container App vs managed) is a deploy-time decision, tracked with the Phase 8 deploy work.
3. **CI cost.** A KC container adds ~20–30 s to CI startup. Acceptable; the `operator` mode keeps
   pure-unit test runs KC-free.
4. **Demo without Docker.** Keep `Auth:Mode=operator` as the no-KC fallback for constrained demo
   machines / Codespaces, documented as the offline path.
5. **Thesis priority.** This is infrastructure, not the multi-agent thesis core — sequence it against
   Horizon 0 (live KC1–KC5 bench, v1.0 tag) before committing the full 9.2–9.8 build.
