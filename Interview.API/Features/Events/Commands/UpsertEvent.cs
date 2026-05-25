using FluentValidation;
using Interview.API.Domain.Entities;
using Interview.API.Infrastructure.Persistence;
using MediatR;

namespace Interview.API.Features.Events.Commands;

public record UpsertEventCommand(
    Guid? Id,
    string Name,
    string? Description,
    DateTime DateUtc
) : IRequest<Guid>;

public class UpsertEventCommandValidator : AbstractValidator<UpsertEventCommand>
{
    public UpsertEventCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Event name is required and cannot be empty.")
            .MaximumLength(200).WithMessage("Event name must not exceed 200 characters.");
    }
}

public class UpsertEventCommandHandler : IRequestHandler<UpsertEventCommand, Guid>
{
    private readonly AppDbContext _context;

    public UpsertEventCommandHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(UpsertEventCommand request, CancellationToken cancellationToken)
    {
        Event? ev = null;

        if (request.Id.HasValue)
        {
            ev = await _context.Events.FindAsync(new object[] { request.Id.Value }, cancellationToken);
        }

        if (ev == null)
        {
            ev = new Event
            {
                Id = request.Id ?? Guid.NewGuid()
            };
            _context.Events.Add(ev);
        }

        ev.Name = request.Name;
        ev.Description = request.Description;
        ev.DateUtc = request.DateUtc;

        await _context.SaveChangesAsync(cancellationToken);

        return ev.Id;
    }
}
