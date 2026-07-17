namespace TrashAnimal.Api.Lobbies;

/// <summary>
/// One occupied seat in a <see cref="LobbyEntry"/>. <see cref="ClientToken"/> is never exposed
/// to other clients — it is returned only to the owning client and is the sole authorization
/// mechanism for the admin-only start action and for reconnection.
/// </summary>
public sealed record LobbySeat(int SeatIndex, string Nickname, string ClientToken);
