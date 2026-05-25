# Event Ticketing System Web API

A concurrency-safe, clean-architecture ASP.NET Core Web API representing an event-ticketing system built in .NET 10. The backend is designed with the CQRS pattern (using MediatR), request validation (using FluentValidation), unified RFC 9457 error handling, and atomic SQL updates to manage concurrency control.

---

## Technical Architecture

- **CQRS Pattern**: Decoupled commands and queries implemented using the `MediatR` library.
- **Validation Pipeline**: FluentValidation rules are run automatically inside a MediatR pipeline behavior, ensuring requests are validated before reaching handlers.
- **CORS Support**: Configured for local React/Vite client development (`http://localhost:5173`).
- **Atomic Concurrency Control**: Uses EF Core bulk updates (`ExecuteUpdateAsync`) translated to database-level SQL updates. This guarantees that if two customers attempt to reserve the same ticket simultaneously under contention, exactly one succeeds (returns `201 Created`) and the other receives a `409 Conflict` (ProblemDetails).
- **Hold Expiration**: Active ticket holds expire after 10 minutes. This logic is handled dynamically during reads, reservations, and purchases, eliminating the need for a separate background service.

---

## Local Setup & Development

### Prerequisites
- .NET 10.0 SDK

### 1. Restore & Build
Restore NuGet packages and build the solution:
```bash
dotnet restore
dotnet build
```

### 2. Run the API Project
Start the Web API server:
```bash
dotnet run --project Interview.API
```
On startup:
- The SQLite database file (`EventTicketing.db`) will be created automatically.
- Preconfigured seed data (including available, active holds, expired holds, and sold tickets) will be populated.

### 3. Explore the API (Swagger UI)
Once running, open your web browser to:
- **Swagger Documentation**: [http://localhost:5000/](http://localhost:5000/) or [https://localhost:5001/](https://localhost:5001/) (or check the terminal output for assigned ports).

---

## Running the Test Suite

The solution includes a test project (`Interview.Tests`) containing:
- **Validation Unit Tests**: Verifies name constraints, ticket existence lookup, and event lookup constraints using `FluentValidation.TestHelper`.
- **Concurrency Integration Tests**: Hosts a test server using `WebApplicationFactory`, seeds 1 available ticket, sends concurrent reservation requests using `Task.WhenAll`, and asserts that one request succeeds with `210 Created` while the other returns a unified `409 Conflict` ProblemDetails payload.

To execute all tests, run:
```bash
dotnet test
```

---

## Main Endpoint Specifications

### 1. Get Event Details
- **Method**: `GET`
- **Route**: `/api/events/{id}`
- **Response**: `200 OK` on success, `404 NotFound`. Returns active ticket statistics (expired holds are counted as available).

### 2. Reserve a Ticket
- **Method**: `POST`
- **Route**: `/api/events/{id}/reserve`
- **Body**:
  ```json
  {
    "holderName": "Alice"
  }
  ```
- **Response**: `201 Created` with the reserved `TicketId` on success, `400 BadRequest` (validation failed), or `409 Conflict` (concurrency clash / sold out).

### 3. Purchase a Ticket
- **Method**: `POST`
- **Route**: `/api/tickets/{id}/purchase`
- **Body**:
  ```json
  {
    "holderName": "Alice"
  }
  ```
- **Response**: `200 OK` on success, `400 BadRequest` (ticket does not exist), or `409 Conflict` (hold expired or reserved by a different customer).
