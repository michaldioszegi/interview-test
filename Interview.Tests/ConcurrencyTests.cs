using System.Net;
using System.Net.Http.Json;
using Interview.API.Domain.Entities;
using Interview.API.Domain.Enums;
using Interview.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

using Microsoft.Data.Sqlite;

namespace Interview.Tests;

public class ConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConcurrencyTests(WebApplicationFactory<Program> factory)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton(connection);

                services.AddDbContext<AppDbContext>((sp, options) =>
                {
                    var conn = sp.GetRequiredService<SqliteConnection>();
                    options.UseSqlite(conn);
                });
            });
        });
    }

    [Fact]
    public async Task Concurrent_Reservations_One_Succeeds_One_Returns_409_ProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();
        var eventId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();

            var ev = new Event
            {
                Id = eventId,
                Name = "Exclusive Concert",
                DateUtc = DateTime.UtcNow.AddDays(10),
                Tickets = new List<Ticket>
                {
                    new Ticket { Id = ticketId, Status = TicketStatus.Available }
                }
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
        }

        // Act - Send two concurrent HTTP POST requests to reserve the single ticket
        var task1 = client.PostAsJsonAsync($"/api/events/{eventId}/reserve", new { holderName = "Alice" });
        var task2 = client.PostAsJsonAsync($"/api/events/{eventId}/reserve", new { holderName = "Bob" });

        await Task.WhenAll(task1, task2);

        var response1 = await task1;
        var response2 = await task2;

        // Assert - One must succeed and one must return a 409 Conflict status
        if (response1.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorBody = await response1.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Response 1 returned BadRequest: {errorBody}");
        }
        if (response2.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorBody = await response2.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Response 2 returned BadRequest: {errorBody}");
        }
        if (response1.StatusCode == HttpStatusCode.InternalServerError)
        {
            var errorBody = await response1.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Response 1 returned InternalServerError: {errorBody}");
        }
        if (response2.StatusCode == HttpStatusCode.InternalServerError)
        {
            var errorBody = await response2.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Response 2 returned InternalServerError: {errorBody}");
        }

        var succeeded = response1.StatusCode == HttpStatusCode.Created ? response1 : response2;
        var failed = response1.StatusCode == HttpStatusCode.Conflict ? response1 : response2;

        Assert.Equal(HttpStatusCode.Created, succeeded.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);

        // Verify the failed response conforms to RFC 9457 ProblemDetails
        Assert.Equal("application/problem+json", failed.Content.Headers.ContentType?.MediaType);

        var problemDetails = await failed.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(409, problemDetails.Status);
        Assert.Equal("Conflict", problemDetails.Title);
        Assert.Contains("reserved by another customer", problemDetails.Detail);
    }

    [Fact]
    public async Task Reservation_Is_Allowed_If_Existing_Reservation_Is_Expired()
    {
        // Arrange
        var client = _factory.CreateClient();
        var eventId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();

            var ev = new Event
            {
                Id = eventId,
                Name = "Expired Hold Concert",
                DateUtc = DateTime.UtcNow.AddDays(5),
                Tickets = new List<Ticket>
                {
                    // Ticket is Reserved, but reservation was 15 minutes ago (expired)
                    new Ticket
                    {
                        Id = ticketId,
                        Status = TicketStatus.Reserved,
                        HolderName = "OldHolder",
                        ReservedAtUtc = DateTime.UtcNow.AddMinutes(-15)
                    }
                }
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
        }

        // Act - Attempt to reserve this ticket for a new holder
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/reserve", new { holderName = "NewHolder" });

        // Assert - The reservation should succeed
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Verify database state has been updated to the new holder and current time
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Tickets.FindAsync(ticketId);
            Assert.NotNull(ticket);
            Assert.Equal(TicketStatus.Reserved, ticket.Status);
            Assert.Equal("NewHolder", ticket.HolderName);
            Assert.True(ticket.ReservedAtUtc > DateTime.UtcNow.AddSeconds(-10));
        }
    }

    [Fact]
    public async Task Purchase_Ticket_Fails_If_Reservation_Expired()
    {
        // Arrange
        var client = _factory.CreateClient();
        var ticketId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();

            var eventId = Guid.NewGuid();
            var ev = new Event
            {
                Id = eventId,
                Name = "Expired Hold Event",
                DateUtc = DateTime.UtcNow.AddDays(5)
            };
            db.Events.Add(ev);

            var ticket = new Ticket
            {
                Id = ticketId,
                EventId = eventId,
                Status = TicketStatus.Reserved,
                HolderName = "Charlie",
                ReservedAtUtc = DateTime.UtcNow.AddMinutes(-15) // Expired 15 minutes ago
            };
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
        }

        // Act - Attempt to purchase the ticket
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/purchase", new { holderName = "Charlie" });

        // Assert - Purchase should return a 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(409, problemDetails.Status);
        Assert.Contains("actively reserved by the same holder", problemDetails.Detail);
    }
}
