using System.Net;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// Verifies that the per-session <see cref="System.Threading.SemaphoreSlim"/> lock prevents
/// concurrent requests from corrupting a <see cref="GameSession"/>. All concurrent commands are
/// serialised by the lock, so the session revision must exactly equal the number of commands that
/// the engine accepted — there must be no lost or double-counted increments.
/// </summary>
public sealed class ConcurrencyGuardTests : IClassFixture<TrashApiTestFactory>
{
    private readonly GameApiClient _client;

    public ConcurrencyGuardTests(TrashApiTestFactory factory)
    {
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task ConcurrentCommands_OnSameGame_DoNotCorruptRevision()
    {
        // Arrange: create a game; all clients see revision 0 to start.
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        // Act: fire 5 concurrent RollDie commands for player 0.
        // The session lock serialises execution, but successive rolls may or may not be
        // in the allowed set depending on how the engine responds after the first one.
        const int concurrentRequestCount = 5;
        var tasks = Enumerable
            .Range(0, concurrentRequestCount)
            .Select(_ => _client.RollDieAsync(gameId, playerSeat: 0))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert: every response must be a well-formed HTTP response (no 500s or crashes).
        foreach (var (status, _) in results)
        {
            Assert.True(
                status is HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity,
                $"Unexpected status {status}; expected 200 or 422.");
        }

        // Assert: the final revision must equal the number of commands the engine accepted.
        // If the lock was absent and two commands raced on a single revision increment,
        // the final revision would be lower than the success count.
        var (viewStatus, view) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Equal(HttpStatusCode.OK, viewStatus);

        var successCount = results.Count(r => r.Status == HttpStatusCode.OK && r.Body?.Succeeded == true);
        Assert.Equal(successCount, view!.Revision);
    }

    [Fact]
    public async Task ConcurrentCommands_OnSameGame_GameStateIsValid()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var tasks = Enumerable
            .Range(0, 8)
            .Select(_ => _client.RollDieAsync(gameId, playerSeat: 0))
            .ToList();

        await Task.WhenAll(tasks);

        // The game must still be in a defined, readable state after concurrent load.
        var (status, view) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(view);
        Assert.NotNull(view.View);
        Assert.True(Enum.IsDefined(view.View.State), $"Game state {view.View.State} is not a defined enum value.");
    }
}
