using TrashAnimal.Api.Contracts.Responses;

namespace TrashAnimal.Api.Updates;

/// <summary>
/// Push notification sent to all clients in a lobby's SignalR group after each successful
/// join/start. Unlike <see cref="GameUpdateEnvelope"/>, this carries the full <see
/// cref="LobbyView"/> directly — there is no hidden-information constraint for lobby state, so
/// there is no need for a notify-then-refetch round trip.
/// </summary>
public sealed record LobbyUpdateEnvelope(Guid LobbyId, int Revision, LobbyView Lobby);
