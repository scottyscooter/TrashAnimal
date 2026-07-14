using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrashAnimal.Api.Sessions;
using TrashAnimal.Api.Startup.Options;
using TrashAnimal.Api.Updates;

namespace TrashAnimal.Api.Tests.Helpers;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for <see cref="Program"/> that:
/// <list type="bullet">
///   <item>Replaces the real <see cref="SignalRGameUpdatePublisher"/> with a no-op stub so tests
///   do not require a SignalR backplane.</item>
///   <item>Replaces the in-memory session repository with <see cref="TestableGameSessionRepository"/>
///   so individual tests can pre-register sessions with controlled game state or sequenced dice.</item>
/// </list>
/// The real <c>appsettings.json</c> is loaded by the host builder; options validation passes
/// on those defaults so no extra configuration is needed here.
/// Shared via <c>IClassFixture&lt;TrashApiTestFactory&gt;</c> across a test class to amortise
/// the in-process web-app startup cost. Tests use unique <see cref="Guid"/> game IDs to avoid
/// cross-test interference in the shared repository.
/// </summary>
public sealed class TrashApiTestFactory : WebApplicationFactory<Program>
{
    public TestableGameSessionRepository SessionRepository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // IEnumerable<int> binding appends config values on top of the C# default initialiser
            // when appsettings.json is found on the content root path. PostConfigure runs last in
            // the options pipeline and resets the collection to a clean 4-value set so the
            // StartupValidator does not reject the host.
            services.PostConfigure<GameApplicationServiceOptions>(opts =>
                opts.StartingHandCounts = [3, 4, 5, 6]);

            services.RemoveAll<IGameUpdatePublisher>();
            services.AddScoped<IGameUpdatePublisher, StubGameUpdatePublisher>();

            services.RemoveAll<IGameSessionRepository>();
            services.AddSingleton<IGameSessionRepository>(SessionRepository);
        });
    }
}
