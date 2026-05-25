using FluentValidation;
using Interview.API.Domain.Enums;
using Interview.API.Domain.Exceptions;
using Interview.API.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

namespace Interview.API.Features.Tickets.Commands;

public record PurchaseTicketCommand(Guid TicketId, string HolderName) : IRequest<Guid>;

public class PurchaseTicketCommandValidator : AbstractValidator<PurchaseTicketCommand>
{
    private readonly AppDbContext _context;

    public PurchaseTicketCommandValidator(AppDbContext context)
    {
        _context = context;

        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("TicketId is required.")
            .MustAsync(async (ticketId, cancellationToken) =>
                await _context.Tickets.AnyAsync(t => t.Id == ticketId, cancellationToken))
            .WithMessage("Ticket must exist.");

        RuleFor(x => x.HolderName)
            .NotEmpty().WithMessage("Holder name is required and cannot be empty.")
            .MaximumLength(200).WithMessage("Holder name must not exceed 200 characters.");
    }
}

public class PurchaseTicketCommandHandler : IRequestHandler<PurchaseTicketCommand, Guid>
{
    private readonly AppDbContext _context;
    private readonly ILogger<PurchaseTicketCommandHandler> _logger;

    public PurchaseTicketCommandHandler(AppDbContext context, ILogger<PurchaseTicketCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> Handle(PurchaseTicketCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to purchase TicketId: {TicketId} for holder: {HolderName}", 
            request.TicketId, request.HolderName);

        var expirationLimit = DateTime.UtcNow.AddMinutes(-10);

        // Perform atomic update.
        // It must check that the status is Reserved, the holder name matches,
        // and the reservation has not expired.
        var rowsAffected = await _context.Tickets
            .Where(t => t.Id == request.TicketId &&
                        t.Status == TicketStatus.Reserved &&
                        t.HolderName == request.HolderName &&
                        t.ReservedAtUtc > expirationLimit)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TicketStatus.Sold)
                .SetProperty(t => t.ReservedAtUtc, (DateTime?)null),
                cancellationToken);

        if (rowsAffected == 0)
        {
            _logger.LogWarning("Purchase failed: TicketId: {TicketId} is not reserved by holder: {HolderName} or the hold has expired", 
                request.TicketId, request.HolderName);
            throw new TicketUnavailableException("The ticket cannot be purchased. It must be actively reserved by the same holder.");
        }

        _logger.LogInformation("Successfully purchased TicketId: {TicketId} for holder: {HolderName}", 
            request.TicketId, request.HolderName);

        return request.TicketId;
    }
}
