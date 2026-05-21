using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using TrashAnimal.Api.Application;
using TrashAnimal.Api.Hubs;
using TrashAnimal.Api.Sessions;
using TrashAnimal.Api.Startup;
using TrashAnimal.Api.Updates;

var builder = WebApplication.CreateBuilder(args);

// Logging — structured log levels are configured in appsettings.json.
builder.Logging.AddConsole();

builder.Services.RegisterOptions();
builder.Services.RegisterValidators();

// Services
builder.Services.AddSingleton<IGameSessionRepository, InMemoryGameSessionRepository>();
builder.Services.AddScoped<IGameUpdatePublisher, SignalRGameUpdatePublisher>();
builder.Services.AddScoped<GameApplicationService>();

// Controllers + JSON: all enums must serialize as strings across all endpoints.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Minimal API routes also respect this serializer policy.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// OpenAPI document generation — served only in Development to avoid exposing the spec in production.
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Serves the raw OpenAPI document at /openapi/v1.json
    app.MapOpenApi();

    // Serves the Scalar interactive API browser at /scalar/v1
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
