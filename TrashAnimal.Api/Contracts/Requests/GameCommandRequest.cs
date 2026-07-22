using System.Text.Json.Serialization;
using TrashAnimal;

namespace TrashAnimal.Api.Contracts.Requests;

/// <summary>
/// Base type for game command requests, discriminated by <c>kind</c> field.
/// Each subtype represents a distinct command shape with only the required payload fields.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PlayActionCommand), "action")]
[JsonDerivedType(typeof(PlayFeeshCommand), "playFeesh")]
[JsonDerivedType(typeof(PlayShinyCommand), "playShiny")]
[JsonDerivedType(typeof(ResolveTokenStealCommand), "resolveTokenSteal")]
[JsonDerivedType(typeof(CardPickCommand), "cardPick")]
[JsonDerivedType(typeof(DoubleStashCommand), "doubleStash")]
[JsonDerivedType(typeof(RecyclePickCommand), "recyclePick")]
public abstract record GameCommandRequest(int PlayerSeat);

/// <summary>
/// Submit a standard game action with no additional payload.
/// Examples: RollDie, StopRolling, EndTurn, BanditPass.
/// </summary>
public sealed record PlayActionCommand(int PlayerSeat, GameAction Action)
    : GameCommandRequest(PlayerSeat);

/// <summary>
/// Play the Feesh card to retrieve a specific card from the discard pile.
/// </summary>
public sealed record PlayFeeshCommand(int PlayerSeat, Guid CardId)
    : GameCommandRequest(PlayerSeat);

/// <summary>
/// Play the Shiny card to steal from a specific opponent.
/// </summary>
public sealed record PlayShinyCommand(int PlayerSeat, int VictimSeat)
    : GameCommandRequest(PlayerSeat);

/// <summary>
/// Resolve a token-phase steal by selecting the victim.
/// </summary>
public sealed record ResolveTokenStealCommand(int PlayerSeat, int VictimSeat)
    : GameCommandRequest(PlayerSeat);

/// <summary>
/// Pick a card in a context-dependent scenario (steal resolution, stash-trash, or bandit stash).
/// The backend determines which action applies based on GameState and TokenPhaseStep.
/// </summary>
public sealed record CardPickCommand(int PlayerSeat, Guid CardId)
    : GameCommandRequest(PlayerSeat);

/// <summary>
/// Submit the pair of cards for a double stash in token phase.
/// </summary>
public sealed record DoubleStashCommand(int PlayerSeat, IReadOnlyList<Guid> CardIds)
    : GameCommandRequest(PlayerSeat);

/// <summary>
/// Pick a replacement token when recycling.
/// </summary>
public sealed record RecyclePickCommand(int PlayerSeat, TokenAction Replacement)
    : GameCommandRequest(PlayerSeat);
