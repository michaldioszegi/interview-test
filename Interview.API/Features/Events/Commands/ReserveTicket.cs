using FluentValidation;
using Interview.API.Domain.Enums;
using Interview.API.Domain.Exceptions;
using Interview.API.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

namespace Interview.API.Features.Events.Commands;

public record ReserveTicketCommand(Guid EventId, string HolderName) : IRequest<Guid>;

public class ReserveTicketCommandValidator : AbstractValidator<ReserveTicketCommand>
{
    private readonly AppDbContext _context;

    public ReserveTicketCommandValidator(AppDbContext context)
    {
        _context = context;

        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("EventId is required.")
            .MustAsync(async (eventId, cancellationToken) =>
                await _context.Events.AnyAsync(e => e.Id == eventId, cancellationToken))
            .WithMessage("Event must exist.");

        RuleFor(x => x.HolderName)
            .NotEmpty().WithMessage("Holder name is required and cannot be empty.")
            .MaximumLength(200).WithMessage("Holder name must not exceed 200 characters.");
    }
}

public class ReserveTicketCommandHandler : IRequestHandler<ReserveTicketCommand, Guid>
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReserveTicketCommandHandler> _logger;

    public ReserveTicketCommandHandler(AppDbContext context, ILogger<ReserveTicketCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> Handle(ReserveTicketCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to reserve a ticket for EventId: {EventId} for holder: {HolderName}", 
            request.EventId, request.HolderName);

        var expirationTime = DateTime.UtcNow.AddMinutes(-10);

        // Find a ticket that is Available, or is Reserved but the reservation has expired.
        var availableTicket = await _context.Tickets
            .Where(t => t.EventId == request.EventId &&
                        (t.Status == TicketStatus.Available ||
                         (t.Status == TicketStatus.Reserved && t.ReservedAtUtc <= expirationTime)))
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (availableTicket == null)
        {
            _logger.LogWarning("Reservation failed: No available tickets for EventId: {EventId}", request.EventId);
            throw new TicketUnavailableException("No tickets are available for this event.");
        }

        var selectedTicketId = availableTicket.Id;

        // Perform the update atomically.
        // It must check that the status is still Available OR still Reserved and expired.
        var rowsAffected = await _context.Tickets
            .Where(t => t.Id == selectedTicketId &&
                        (t.Status == TicketStatus.Available ||
                         (t.Status == TicketStatus.Reserved && t.ReservedAtUtc <= expirationTime)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TicketStatus.Reserved)
                .SetProperty(t => t.HolderName, request.HolderName)
                .SetProperty(t => t.ReservedAtUtc, DateTime.UtcNow),
                cancellationToken);

        if (rowsAffected == 0)
        {
            _logger.LogWarning("Reservation concurrency conflict: Ticket {TicketId} was taken by another user for EventId: {EventId}", 
                selectedTicketId, request.EventId);
            throw new TicketUnavailableException("The selected ticket was reserved by another customer.");
        }

        _logger.LogInformation("Successfully reserved TicketId: {TicketId} for EventId: {EventId} for holder: {HolderName}", 
            selectedTicketId, request.EventId, request.HolderName);

        return selectedTicketId;
    }
}
