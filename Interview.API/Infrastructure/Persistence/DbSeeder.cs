using Interview.API.Domain.Entities;
using Interview.API.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Interview.API.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, bool reset = false)
    {
        if (reset)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        if (await context.Events.AnyAsync())
        {
            return; // Database is already seeded
        }

        var rockConcertId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var techConfId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var comedyShowId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var events = new List<Event>
        {
            new Event
            {
                Id = rockConcertId,
                Name = "Rock Concert",
                Description = "An awesome live rock concert experience.",
                DateUtc = DateTime.UtcNow.AddDays(30),
                Tickets = new List<Ticket>
                {
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Available },
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Available },
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Available },
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Available },
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Available }
                }
            },
            new Event
            {
                Id = techConfId,
                Name = "Tech Conference 2026",
                Description = "A futuristic gathering of developers.",
                DateUtc = DateTime.UtcNow.AddDays(60),
                Tickets = new List<Ticket>
                {
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Available },
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Reserved, HolderName = "Alice", ReservedAtUtc = DateTime.UtcNow.AddMinutes(-5) }, // Active reservation
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Sold, HolderName = "Bob" } // Sold ticket
                }
            },
            new Event
            {
                Id = comedyShowId,
                Name = "Comedy Show",
                Description = "A night full of laughter and joy.",
                DateUtc = DateTime.UtcNow.AddDays(15),
                Tickets = new List<Ticket>
                {
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Available },
                    new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Reserved, HolderName = "Charlie", ReservedAtUtc = DateTime.UtcNow.AddMinutes(-15) } // Expired reservation (15 mins ago)
                }
            }
        };

        await context.Events.AddRangeAsync(events);
        await context.SaveChangesAsync();
    }
}
