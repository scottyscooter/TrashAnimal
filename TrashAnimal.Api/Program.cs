using System.Text.Json.Serialization;
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

var app = builder.Build();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
