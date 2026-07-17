using Microsoft.AspNetCore.SignalR;

namespace TrashAnimal.Api.Hubs;

/// <summary>
/// SignalR hub that clients connect to on the Lobby page to receive push notifications.
/// Unlike <see cref="GameHub"/>, the pushed payload carries the full lobby state directly —
/// there is no hidden-information constraint for a lobby's seat list, so no notify-then-refetch
/// round trip is needed.
/// </summary>
/// <remarks>
/// <b>Client connection protocol:</b>
/// <list type="number">
///   <item>Connect to <c>/hubs/lobby</c> using the SignalR client library.</item>
///   <item>Register a handler for the <see cref="LobbyUpdatedEvent"/> event name.</item>
///   <item>Call <see cref="JoinLobbyAsync"/> with the <c>lobbyId</c> to start receiving
///         notifications for that lobby.</item>
///   <item>On receiving <c>LobbyUpdated</c>, update local state directly from the payload's
///         <c>Lobby</c> field — no REST re-fetch required (but <c>GET /lobbies/{lobbyId}</c>
///         remains available as a polling fallback / initial load).</item>
///   <item>Call <see cref="LeaveLobbyAsync"/> when the player navigates away or the lobby starts.</item>
/// </list>
/// </remarks>
public sealed class LobbyHub : Hub
{
    /// <summary>
    /// The name of the server-to-client event pushed after each successful join/start.
    /// Clients must register a handler for this exact string.
    /// </summary>
    public const string LobbyUpdatedEvent = "LobbyUpdated";

    /// <summary>
    /// Called by a client after connect to subscribe to updates for a specific lobby.
    /// Adds the caller's connection to the SignalR group for that lobby.
    /// </summary>
    public async Task JoinLobbyAsync(Guid lobbyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(lobbyId));
    }

    /// <summary>
    /// Called by a client to stop receiving updates for a specific lobby.
    /// Note: this only removes the SignalR group subscription — it does not release the caller's
    /// seat. There is no seat-release/leave-lobby endpoint in this slice; a player who joins and
    /// abandons the tab pre-start keeps their seat, which can leave a lobby permanently short of
    /// a full roster. This is an accepted MVP limitation, not an oversight.
    /// </summary>
    public async Task LeaveLobbyAsync(Guid lobbyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNameFor(lobbyId));
    }

    /// <summary>
    /// Returns the SignalR group name for a given lobby.
    /// Used by <see cref="TrashAnimal.Api.Updates.SignalRLobbyUpdatePublisher"/> to target the right group.
    /// </summary>
    internal static string GroupNameFor(Guid lobbyId) => $"lobby:{lobbyId}";
}
