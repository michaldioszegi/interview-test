using FluentValidation;
using Interview.API.Features.Events.Commands;
using Interview.API.Features.Events.Queries;
using Interview.API.Features.Tickets.Commands;
using Interview.API.Infrastructure.Behaviors;
using Interview.API.Infrastructure.Persistence;
using Interview.API.Middleware;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register EF Core DbContext (SQLite)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "DataSource=EventTicketing.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Register MediatR & Pipeling Behavior for validation
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// Register FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Unified Error Handling with RFC 9457 ProblemDetails
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// Enable Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Event Ticketing API", Version = "v1" });
});

// Configure CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty; // Swagger UI as the root
    });
}

app.UseExceptionHandler(); // Trigger the configured IExceptionHandler

// Minimal API Endpoints

// GET /api/events/{id} - Get event details and ticket counts
app.MapGet("/api/events/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var query = new GetEventQuery(id);
    var response = await mediator.Send(query);
    return response != null ? Results.Ok(response) : Results.NotFound();
})
.WithName("GetEvent");

// POST /api/events/{id}/reserve - Reserve one available ticket
app.MapPost("/api/events/{id:guid}/reserve", async (Guid id, ReserveRequest request, IMediator mediator) =>
{
    var command = new ReserveTicketCommand(id, request.HolderName);
    var ticketId = await mediator.Send(command);
    return Results.Created($"/api/tickets/{ticketId}", new { TicketId = ticketId });
})
.WithName("ReserveTicket");

// POST /api/tickets/{id}/purchase - Purchase a reserved ticket
app.MapPost("/api/tickets/{id:guid}/purchase", async (Guid id, PurchaseRequest request, IMediator mediator) =>
{
    var command = new PurchaseTicketCommand(id, request.HolderName);
    var ticketId = await mediator.Send(command);
    return Results.Ok(new { TicketId = ticketId });
})
.WithName("PurchaseTicket");



// Database seeding on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var resetDb = configuration.GetValue<bool>("SeedSettings:ResetDatabase");
    await DbSeeder.SeedAsync(context, reset: resetDb);
}

app.Run();

// DTOs for requests

public record ReserveRequest(string HolderName);
public record PurchaseRequest(string HolderName);

// Expose Program class for integration testing
public partial class Program { }
