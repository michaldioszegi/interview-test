# UI Implementation Plan - Event Ticketing Web Client

This document outlines the architecture, components, state management, and configuration for a React + TypeScript frontend client designed to interact with the event-ticketing system Web API.

---

## 1. Backend CORS Policy Setup

To allow local development tools (e.g. a Vite frontend running on `http://localhost:5173` or a Create React App on `http://localhost:3000`) to communicate with the ASP.NET Core backend API, the backend's `Program.cs` needs a Cross-Origin Resource Sharing (CORS) policy.

### Proposed Backend Change in [Program.cs](file:///home/michal/Documents/interview-test/Interview.API/Program.cs)
```csharp
// Register CORS Services
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Enable CORS middleware (before map endpoints)
app.UseCors();
```

---

## 2. Frontend Structure & Components

We propose initializing a React app inside `Interview.UI` using Vite:
```bash
npx -y create-vite@latest ./ --template react-ts
```

### Components Layout

```
src/
├── api/
│   └── client.ts          # Axios or Fetch API wrapper
├── components/
│   ├── EventCard.tsx      # Card representing an event with real-time counters
│   ├── EventList.tsx      # Main dashboard listing all events
│   ├── EventDetails.tsx   # Detailed event view + booking panel
│   ├── HoldTimer.tsx      # Active reservation countdown timer (10 mins)
│   └── Admin/
│       ├── EventForm.tsx  # CRUD create/edit event
│       └── TicketForm.tsx # CRUD create/edit ticket
├── types/
│   └── index.ts           # Shared TypeScript interfaces (Event, Ticket, Statuses)
├── App.tsx                # Routing and main application layout
└── main.tsx
```

---

## 3. Component Responsibility & Features

### A. Event Dashboard (`EventList.tsx`)
- Fetches all active events.
- Displays summary statistics for each event:
  - **Available** seats/tickets.
  - **Reserved** (active holds).
  - **Sold** tickets.
- Links to the booking detail view or the admin CRUD views.

### B. Event Booking System (`EventDetails.tsx` & `HoldTimer.tsx`)
- Fetches detailed event data by ID (`GET /api/events/{id}`).
- If a ticket is in `Available` state, displays a **"Reserve Ticket"** form requiring the user's name (`holderName`).
- Submitting the form calls `POST /api/events/{id}/reserve`.
- On successful reservation:
  - Stores the reservation locally (e.g. in React State or `localStorage` as `{ ticketId, holderName, reservedAt: Date.now() }`).
  - Renders the `HoldTimer` countdown displaying a `10:00` countdown timer.
  - Renders a **"Complete Purchase"** button.
- Clicking "Complete Purchase" calls `POST /api/tickets/{ticketId}/purchase` with body `{ holderName }` to mark the ticket as sold.
- **Hold Expiration**: When the 10-minute timer reaches zero, the UI automatically invalidates the reservation state, hides the purchase form, and refreshes the event details.

### C. Admin CRUD Views (`Admin/EventForm.tsx` & `Admin/TicketForm.tsx`)
- Form to upsert an event (`POST /api/events`).
- Form to upsert a ticket (`POST /api/tickets`).
- Essential for testing the database setup, adding new concerts/shows, and seeding additional tickets.

---

## 4. API Client Integration & Types

### TypeScript Definitions (`types/index.ts`)
```typescript
export enum TicketStatus {
  Available = 0,
  Reserved = 1,
  Sold = 2
}

export interface EventDetails {
  id: string;
  name: string;
  description?: string;
  startsAt: string;
  availableTickets: number;
  reservedTickets: number;
  soldTickets: number;
}

export interface LocalReservation {
  ticketId: string;
  eventId: string;
  holderName: string;
  reservedAt: number; // UTC timestamp
}
```

### Client Methods (`api/client.ts`)
```typescript
const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

export async function getEvent(id: string): Promise<EventDetails> {
  const res = await fetch(`${BASE_URL}/api/events/${id}`);
  if (!res.ok) throw new Error("Event not found");
  return res.json();
}

export async function reserveTicket(eventId: string, holderName: string): Promise<{ ticketId: string }> {
  const res = await fetch(`${BASE_URL}/api/events/${eventId}/reserve`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ holderName })
  });
  if (res.status === 409) throw new Error("No tickets available or reservation conflict");
  if (!res.ok) throw new Error("Failed to reserve ticket");
  return res.json();
}

export async function purchaseTicket(ticketId: string, holderName: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/api/tickets/${ticketId}/purchase`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ holderName })
  });
  if (res.status === 409) throw new Error("Purchase failed: hold expired or invalid holder name");
  if (!res.ok) throw new Error("Failed to complete purchase");
}
```