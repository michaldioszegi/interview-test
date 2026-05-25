namespace Interview.API.Domain.Exceptions;

public class TicketUnavailableException : Exception
{
    public TicketUnavailableException(string message) : base(message)
    {
    }
}
