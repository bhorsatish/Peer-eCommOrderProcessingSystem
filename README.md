# Peer-eCommOrderProcessingSystem

E-Commerce - Order Processing System

A backend service for managing e-commerce orders: creating orders, tracking status through a fulfillment lifecycle, and auto-advancing paid/COD orders via a background job that sweeps newly-placed orders every 5 minutes.

## Tech Stack

- **ASP.NET Core Web API** (.NET 10 / `net10.0`)
- **Entity Framework Core** with **SQLite** for persistence
- **xUnit** for unit tests, run against EF Core's in-memory provider
- **OpenAPI / Swagger UI** for interactive API docs in development

## Project Structure

```
src/
  peer_database/
    eCommDB.db                     # SQLite database file used at runtime
  peer_ecomm_ms/
    peer_ecomm_ms/                 # API project
      Controllers/                 # OrdersController, ProductsController, UsersController, StatusStateController
      Services/                    # OrderDetailsService, OrderStatusUpdateService (background job), AuthenticationService
      Models/                      # Orders, Products, Users, StatusStates
      Models/DTOs/                 # Response DTOs (OrderDetailsDto, ProductDetailsDto, StatusDetailsDto, UserDetailsDto)
      DBA/ECommDbContext.cs        # EF Core DbContext
      Program.cs                   # App startup / DI wiring
    peer_ecomm_ms.Tests/           # xUnit test project (Controllers + Services)
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQLite database file at `src/peer_database/eCommDB.db` (already included in the repo, pre-seeded with `Products`, `Users`, and `StatusStates` reference data)

## Running the API

```powershell
cd src/peer_ecomm_ms/peer_ecomm_ms
dotnet run
```

By default the API listens on:
- `http://localhost:5296`
- `https://localhost:7019` (when run with the `https` launch profile)

In development, Swagger UI is available at `/swagger`.

> **Note:** `Program.cs` currently points at an absolute SQLite path (`H:\eCommOrderProcessing\src\peer_database\eCommDB.db`). If you clone this repo to a different drive/path, update the `UseSqlite` connection string in `Program.cs` before running.

## Running Tests

```powershell
cd src/peer_ecomm_ms/peer_ecomm_ms.Tests
dotnet test
```

Tests use EF Core's in-memory provider (see `TestHelpers/DbContextFactory.cs`), so no database file is required to run the suite.

## Domain Model

- **Order** — `Id, UserId, ProductId, StatusId, LastUpdatedByUserId, PaymentMode`, plus lifecycle timestamps (`AddedOn`, `OrderedOn`, `PackagedOn`, `DeliveredOn`, `CancelledOn`, `CancellationPaidOn`, `LastUpdatedOn`) and `OrderedFor` / `DeliveryAddress`.
- **Product** — `Id, Name, Price, Icon`.
- **User** — `Id, UserId (username), RoleId, Password (hashed)`.
- **StatusState** — reference table of order statuses (`Id, Name, Description, Icon`), e.g. `10 = Placed`, `2 = New Order - COD`, `4 = Packaged`, `5 = Delivered`, `6 = Cancellation Requested`, `8 = Cancelled`, `9 = In Transit`.

Orders reference `Product`, `User`, and `StatusState` by ID; `GET` endpoints join across these to return an enriched `OrderDetailsDto`.

## API Endpoints

### Orders (`/orders`)
| Method | Route | Description |
|---|---|---|
| `GET` | `/orders/{id}` | Get a single order with enriched product/status/user details. 404 if not found. |
| `GET` | `/orders/user/{userId}?pageNumber=&pageSize=` | Paginated orders for a user. |
| `GET` | `/orders/status/{statusId}?pageNumber=&pageSize=` | Paginated orders filtered by status. |
| `POST` | `/orders/add` | Create a new order. |
| `PUT` | `/orders/{id}/status` | Update an order's status (also used to cancel — see below). |
| `DELETE` | `/orders/{id}` | Delete an order. |

There is no unfiltered "list all orders" endpoint by design — orders must be queried by ID, user, or status.

**Cancellation rule:** an order can only move to status `6` (Cancellation Requested) or `8` (Cancelled) while it is still `Placed`/`New Order` (`10`, `2`, `3`) or already mid-cancellation (`6`, `7`); once it reaches `Packaged` (`4`) or later, cancellation is rejected with a 400.

### Products (`/products`)
| Method | Route | Description |
|---|---|---|
| `GET` | `/products` | List all products. |
| `GET` | `/products/{id}` | Get a single product. 404 if not found. |
| `POST` | `/products/add` | Create a product. |

### Users (`/users`)
| Method | Route | Description |
|---|---|---|
| `POST` | `/users/add` | Register a new user (hashes the password). |
| `POST` | `/users/authenticate` | Authenticate with username/password. |

## Background Job

`OrderStatusUpdateService` (`Services/OrderStatusUpdateService.cs`) is a `BackgroundService` that:

1. Runs once immediately on application startup.
2. Then runs every 5 minutes via a `Timer`.
3. On each run, finds all orders with `StatusId == 10` (Placed) and advances them: `StatusId = 2` (New Order - COD) if `PaymentMode == 0`, otherwise `StatusId = 3` (New Order - Prepaid).
4. Wraps each run in try/catch so a failure is logged and doesn't crash the host or block the next scheduled run.

## Known Limitations / Assumptions

- The SQLite connection string in `Program.cs` is a hardcoded absolute path rather than a configurable one (e.g. via `appsettings.json`).
- Status transitions are enforced via numeric ID checks in `OrdersController.UpdateOrderStatus` rather than a shared, named state-machine/transition table.
- No authentication middleware guards the API — `/users/authenticate` returns a success payload but no token/session is issued or checked on subsequent requests.
