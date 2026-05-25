using Interview.API.Domain.Enums;
using Interview.API.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Interview.API.Features.Events.Queries;

public record GetEventQuery(Guid Id) : IRequest<EventDetailsResponse?>;

public record EventDetailsResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime DateUtc,
    int AvailableTickets,
    int ReservedTickets,
    int SoldTickets
);

public class GetEventQueryHandler : IRequestHandler<GetEventQuery, EventDetailsResponse?>
{
    private readonly AppDbContext _context;
    private readonly ILogger<GetEventQueryHandler> _logger;

    public GetEventQueryHandler(AppDbContext context, ILogger<GetEventQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EventDetailsResponse?> Handle(GetEventQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving event details for EventId: {EventId}", request.Id);

        var ev = await _context.Events
            .Include(e => e.Tickets)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (ev == null)
        {
            _logger.LogWarning("Event not found for EventId: {EventId}", request.Id);
            return null;
        }

        var expirationLimit = DateTime.UtcNow.AddMinutes(-10);
        int available = 0;
        int reserved = 0;
        int sold = 0;

        foreach (var ticket in ev.Tickets)
        {
            if (ticket.Status == TicketStatus.Available)
            {
                available++;
            }
            else if (ticket.Status == TicketStatus.Reserved)
            {
                if (ticket.ReservedAtUtc == null || ticket.ReservedAtUtc <= expirationLimit)
                {
                    available++; // Expired reservation is considered Available
                }
                else
                {
                    reserved++;
                }
            }
            else if (ticket.Status == TicketStatus.Sold)
            {
                sold++;
            }
        }

        return new EventDetailsResponse(
            ev.Id,
            ev.Name,
            ev.Description,
            ev.DateUtc,
            available,
            reserved,
            sold
        );
    }
}
