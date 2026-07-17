using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using TrashAnimal.Api.Application;
using TrashAnimal.Api.Hubs;
using TrashAnimal.Api.Lobbies;
using TrashAnimal.Api.Sessions;
using TrashAnimal.Api.Startup;
using TrashAnimal.Api.Startup.Options;
using TrashAnimal.Api.Updates;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

// Logging — structured log levels are configured in appsettings.json.
builder.Logging.AddConsole();

builder.Services.RegisterOptions();
builder.Services.RegisterValidators();

// CORS — allows the browser client (a different origin/port in dev) to call the REST API
// and negotiate the SignalR hub. Allowed origins are configured via CorsOptions:AllowedOrigins.
var corsOptions = builder.Configuration.GetSection(nameof(CorsOptions)).Get<CorsOptions>() ?? new CorsOptions();
builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(corsOptions.AllowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials())); // required for SignalR's negotiate/websocket handshake

// Services
builder.Services.AddSingleton<IGameSessionRepository, InMemoryGameSessionRepository>();
builder.Services.AddScoped<IGameUpdatePublisher, SignalRGameUpdatePublisher>();
builder.Services.AddScoped<GameApplicationService>();

builder.Services.AddSingleton<ILobbyRepository, InMemoryLobbyRepository>();
builder.Services.AddScoped<ILobbyUpdatePublisher, SignalRLobbyUpdatePublisher>();
builder.Services.AddScoped<LobbyApplicationService>();

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

app.UseCors(FrontendCorsPolicy);

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");
app.MapHub<LobbyHub>("/hubs/lobby");

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
