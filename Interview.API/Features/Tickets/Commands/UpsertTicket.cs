using FluentValidation;
using Interview.API.Domain.Entities;
using Interview.API.Domain.Enums;
using Interview.API.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Interview.API.Features.Tickets.Commands;

public record UpsertTicketCommand(
    Guid? Id,
    Guid EventId,
    TicketStatus Status,
    string? HolderName,
    DateTime? ReservedAtUtc
) : IRequest<Guid>;

public class UpsertTicketCommandValidator : AbstractValidator<UpsertTicketCommand>
{
    private readonly AppDbContext _context;

    public UpsertTicketCommandValidator(AppDbContext context)
    {
        _context = context;

        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("EventId is required.")
            .MustAsync(async (eventId, cancellationToken) =>
                await _context.Events.AnyAsync(e => e.Id == eventId, cancellationToken))
            .WithMessage("Event must exist.");

        RuleFor(x => x.Id)
            .MustAsync(async (id, cancellationToken) =>
            {
                if (!id.HasValue)
                {
                    return true;
                }
                return await _context.Tickets.AnyAsync(t => t.Id == id.Value, cancellationToken);
            })
            .WithMessage("Ticket must exist when updating an existing ticket.");

        RuleFor(x => x.HolderName)
            .NotEmpty().WithMessage("HolderName is required when ticket is reserved or sold.")
            .When(x => x.Status == TicketStatus.Reserved || x.Status == TicketStatus.Sold);
    }
}

public class UpsertTicketCommandHandler : IRequestHandler<UpsertTicketCommand, Guid>
{
    private readonly AppDbContext _context;

    public UpsertTicketCommandHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(UpsertTicketCommand request, CancellationToken cancellationToken)
    {
        Ticket? ticket = null;

        if (request.Id.HasValue)
        {
            ticket = await _context.Tickets.FindAsync(new object[] { request.Id.Value }, cancellationToken);
        }

        if (ticket == null)
        {
            ticket = new Ticket
            {
                Id = request.Id ?? Guid.NewGuid()
            };
            _context.Tickets.Add(ticket);
        }

        ticket.EventId = request.EventId;
        ticket.Status = request.Status;
        ticket.HolderName = request.HolderName;
        ticket.ReservedAtUtc = request.ReservedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);

        return ticket.Id;
    }
}
