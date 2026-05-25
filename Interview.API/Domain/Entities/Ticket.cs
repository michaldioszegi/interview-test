using Interview.API.Domain.Enums;

namespace Interview.API.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public TicketStatus Status { get; set; }
    public string? HolderName { get; set; }
    public DateTime? ReservedAtUtc { get; set; }
}
