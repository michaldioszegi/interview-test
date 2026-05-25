using FluentValidation.TestHelper;
using Interview.API.Domain.Entities;
using Interview.API.Features.Events.Commands;
using Interview.API.Features.Tickets.Commands;
using Interview.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

using Microsoft.Data.Sqlite;

namespace Interview.Tests;

public class ValidationTests
{
    private AppDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task ReserveTicket_Validator_Fails_When_HolderName_Is_Empty()
    {
        // Arrange
        using var context = CreateDbContext();
        var eventId = Guid.NewGuid();
        context.Events.Add(new Event { Id = eventId, Name = "Rock Show", DateUtc = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var validator = new ReserveTicketCommandValidator(context);
        var command = new ReserveTicketCommand(eventId, "");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HolderName)
              .WithErrorMessage("Holder name is required and cannot be empty.");
    }

    [Fact]
    public async Task ReserveTicket_Validator_Fails_When_Event_Does_Not_Exist()
    {
        // Arrange
        using var context = CreateDbContext();
        var validator = new ReserveTicketCommandValidator(context);
        var command = new ReserveTicketCommand(Guid.NewGuid(), "Alice");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EventId)
              .WithErrorMessage("Event must exist.");
    }

    [Fact]
    public async Task ReserveTicket_Validator_Passes_When_Command_Is_Valid()
    {
        // Arrange
        using var context = CreateDbContext();
        var eventId = Guid.NewGuid();
        context.Events.Add(new Event { Id = eventId, Name = "Rock Show", DateUtc = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var validator = new ReserveTicketCommandValidator(context);
        var command = new ReserveTicketCommand(eventId, "Alice");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task PurchaseTicket_Validator_Fails_When_HolderName_Is_Empty()
    {
        // Arrange
        using var context = CreateDbContext();
        var eventId = Guid.NewGuid();
        context.Events.Add(new Event { Id = eventId, Name = "Rock Show", DateUtc = DateTime.UtcNow });
        var ticketId = Guid.NewGuid();
        context.Tickets.Add(new Ticket { Id = ticketId, EventId = eventId });
        await context.SaveChangesAsync();

        var validator = new PurchaseTicketCommandValidator(context);
        var command = new PurchaseTicketCommand(ticketId, "");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HolderName)
              .WithErrorMessage("Holder name is required and cannot be empty.");
    }

    [Fact]
    public async Task PurchaseTicket_Validator_Fails_When_Ticket_Does_Not_Exist()
    {
        // Arrange
        using var context = CreateDbContext();
        var validator = new PurchaseTicketCommandValidator(context);
        var command = new PurchaseTicketCommand(Guid.NewGuid(), "Alice");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TicketId)
              .WithErrorMessage("Ticket must exist.");
    }

    [Fact]
    public async Task PurchaseTicket_Validator_Passes_When_Command_Is_Valid()
    {
        // Arrange
        using var context = CreateDbContext();
        var eventId = Guid.NewGuid();
        context.Events.Add(new Event { Id = eventId, Name = "Rock Show", DateUtc = DateTime.UtcNow });
        var ticketId = Guid.NewGuid();
        context.Tickets.Add(new Ticket { Id = ticketId, EventId = eventId });
        await context.SaveChangesAsync();

        var validator = new PurchaseTicketCommandValidator(context);
        var command = new PurchaseTicketCommand(ticketId, "Alice");

        // Act
        var result = await validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task UpsertEvent_Validator_Fails_When_Name_Is_Empty()
    {
        // Arrange
        var validator = new UpsertEventCommandValidator();
        var command = new UpsertEventCommand(null, "", "Description", DateTime.UtcNow);

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Event name is required and cannot be empty.");
    }
}
