# Order Processing System — Take-Home Assignment Execution Plan

## Objective

Design, implement, and test a backend system (Java, .NET, or any language) that handles order processing: creating orders, tracking status, and supporting basic order operations, including a background job that auto-transitions `PENDING` orders to `PROCESSING` every 5 minutes.

---

## Phase 0: Lock Down Assumptions

Resolve these before writing code, and document them explicitly in the README:

| Ambiguity | Decision to make |
|---|---|
| Background job semantics | Does the job run every 5 min and bulk-transition *all* currently-`PENDING` orders, or does each order transition 5 minutes after *its own* creation? Simplest literal reading = scheduled sweep every 5 min over all `PENDING` orders. |
| Multi-item orders | Needs `Order` → `OrderItem[]` one-to-many, not a flat order. Each item needs a product reference, quantity, and a **price snapshot at order time** so later price changes don't retroactively alter old orders. |
| Status transition rules | Is `PENDING → PROCESSING → SHIPPED → DELIVERED` strictly linear, or can steps be skipped via API? Decide and enforce in one place. |
| Cancel-after-cancel | Is re-cancelling an already-`CANCELLED` order idempotent (200) or an error (409)? Pick one and apply consistently. |
| Persistence | In-memory dictionary (fast to build, but reads as less rigorous) vs. embedded DB (SQLite/H2) vs. real DB. An embedded DB with migrations is a stronger signal for roughly the same effort. |

---

## Phase 1: Design Before Code

1. **Domain model**
   - `Order { id, customerId, status, createdAt, updatedAt, items: OrderItem[] }`
   - `OrderItem { productId, name, unitPrice, quantity }`
   - Decide if `Product`/`Customer` are real entities or just IDs/free text — the spec doesn't require a product catalog, don't over-build one.

2. **Status state machine**
   - Draw it explicitly: `PENDING → PROCESSING → SHIPPED → DELIVERED`, plus `PENDING → CANCELLED`.
   - Encode as a transition table in code (a dictionary/map of allowed-next-states) — not scattered if/else branches. Reviewers look for this specifically.

3. **API contract** (write this down before coding, e.g. as an OpenAPI/Swagger stub)
   - `POST /orders` — create
   - `GET /orders/{id}` — retrieve one
   - `GET /orders?status=PENDING` — list, optional filter
   - `PATCH /orders/{id}/cancel` — cancel
   - (optional) `PATCH /orders/{id}/status` — manual advance, to demo the state machine without waiting 5 minutes

4. **Response envelope**
   - Consistent error shape (e.g. `{ error: { code, message } }`) across all endpoints — decide once, reuse everywhere.

---

## Phase 2: Scaffold the Project

- New repo/folder, framework of choice (ASP.NET Core Web API / Spring Boot / Express+TS — pick whichever you're strongest in, since it's what gets interrogated in a follow-up).
- Layered structure: `Controllers` → `Services` (business rules, transition validation) → `Repositories` (persistence) → `Models`/`DTOs`.
- Wire up a DB (even SQLite/H2 file) plus a migration or seed script so the grader can run it cold.

---

## Phase 3: Implement `POST /orders` (Create)

**Positive scenarios**
- Valid customer + ≥1 item with positive quantity → order created, status `PENDING`, total computed server-side.

**Negative scenarios**
- Empty `items[]` → 400
- Item with `quantity <= 0` → 400
- Missing/malformed required fields → 400 with field-level errors
- (If modeling products) referenced product doesn't exist → 404/422

**Note**: Total = sum of `unitPrice * quantity`, computed server-side from snapshot prices — never trust a client-sent total.

---

## Phase 4: Implement `GET /orders/{id}` (Retrieve)

**Positive scenarios**
- Existing id → full order with items and current status.

**Negative scenarios**
- Nonexistent id → 404
- Malformed id (wrong type/format) → 400, not 500 — validate before hitting the DB layer.

---

## Phase 5: Implement `GET /orders` (List + Filter)

**Positive scenarios**
- No filter → all orders (pagination optional, a nice touch though not required by spec).
- Valid `status` filter → subset.

**Negative scenarios**
- Invalid/unknown status string in filter → 400 (don't silently return empty).
- Empty result set is **not** an error → 200 + `[]`.

---

## Phase 6: Implement Cancel

**Positive scenarios**
- Order in `PENDING` → `CANCELLED`.

**Negative scenarios**
- Order in `PROCESSING`/`SHIPPED`/`DELIVERED` → 409 Conflict, with a clear message (e.g. "cannot cancel order in PROCESSING state").
- Nonexistent order → 404.
- Already `CANCELLED` → apply your Phase 0 decision (idempotent 200 vs 409) consistently.
- **Race condition**: a cancel request arrives at the same moment the background job flips `PENDING → PROCESSING` — needs a guard (see Phase 7).

---

## Phase 7: Background Job (Plan This Deliberately — the Trickiest Part)

Implement as a scheduled hosted service:
- .NET → `BackgroundService` / `IHostedService`
- Spring → `@Scheduled`
- Node → `node-cron` / `setInterval`

**Positive scenario**
- Every 5 minutes, all `PENDING` orders flip to `PROCESSING`, `updatedAt` stamped.

**Negative / edge cases**
- **Concurrency**: job and a user-initiated cancel racing on the same order — use optimistic concurrency (a `version`/`rowversion` column) or a transaction that re-checks status before writing, so a cancel that lands mid-sweep isn't silently overwritten back to `PROCESSING`.
- **Job failure isolation**: one bad row shouldn't crash the whole sweep — catch per-order, or wrap the batch in a try/catch that logs and continues.
- **Overlap protection**: if a run takes longer than 5 minutes, ensure two runs don't overlap.
- **Testability**: don't hardcode the 5-minute wait into your test suite. Extract the "transition all PENDING orders" logic into a plain method you can unit-test directly, and only wire the timer around it. This is the single most interview-visible design decision in the assignment.

---

## Phase 8: Cross-Cutting Concerns

- Centralized exception/validation middleware → consistent error envelope, no raw stack traces leaking into 500 responses.
- Structured logging (order created / cancelled / transitioned events) — useful for demoing the background job worked without waiting live.
- Swagger/OpenAPI UI for manual verification and as free documentation.

---

## Phase 9: Testing (Alongside Implementation, Not After)

- **Unit tests** on the service layer: every positive/negative case above, especially the transition table (illegal transitions rejected, legal ones accepted) and the cancel guard logic.
- **Unit test the background sweep logic directly**: call the extracted method, seed 3 `PENDING` + 2 `SHIPPED` orders, assert only the 3 flip.
- **Integration tests** hitting real endpoints (WebApplicationFactory / MockMvc / supertest) for at least one happy path and one failure path per endpoint.
- **Concurrency test** (optional, strong signal): simulate cancel + job-sweep racing on the same order, assert the final state is deterministic and correct.

---

## Phase 10: Submission Polish

- README: setup/run instructions, your Phase 0 assumptions written out explicitly, API examples (curl or Postman collection), how to run tests.
- `.http` file / Postman collection or Swagger for manual poking.
- Coherent `git log` (small logical commits beat one giant commit) if the process wants to see your workflow.
- Final checklist:
  - [ ] Every endpoint has both a happy path and negative cases covered in tests.
  - [ ] No endpoint returns a raw 500 for a client-input error.
  - [ ] Status transitions are enforced in one place, not duplicated across the codebase.
  - [ ] Background job logic is unit-testable without waiting on the real timer.
  - [ ] README documents all assumptions made in Phase 0.
