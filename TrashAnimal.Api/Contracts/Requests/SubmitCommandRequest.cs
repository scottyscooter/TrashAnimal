using TrashAnimal;

namespace TrashAnimal.Api.Contracts.Requests;

/// <summary>
/// Request body for POST /games/{gameId}/commands.
/// The <see cref="Action"/> field is the discriminator; include only the payload fields
/// required by that action — unused fields are ignored.
/// </summary>
/// <remarks>
/// Payload field usage by action:
/// <list type="bullet">
///   <item>Steal card pick (AwaitingStealCardPick) — <see cref="CardId"/></item>
///   <item>Bandit stash — <see cref="CardId"/></item>
///   <item>Stash-trash card pick — <see cref="CardId"/></item>
///   <item>Double stash submit — <see cref="CardIds"/></item>
///   <item>Recycle replacement pick — <see cref="RecycleReplacement"/></item>
///   <item>Steal victim selection (ResolveTokenSteal) — <see cref="VictimSeat"/></item>
///   <item>All other actions — no payload required</item>
/// </list>
/// </remarks>
public sealed record SubmitCommandRequest(
    int PlayerSeat,
    GameAction Action,
    Guid? CardId = null,
    IReadOnlyList<Guid>? CardIds = null,
    TokenAction? RecycleReplacement = null,
    int? VictimSeat = null);
