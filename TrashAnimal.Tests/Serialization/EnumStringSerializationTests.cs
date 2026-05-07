using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace TrashAnimal.Tests.Serialization;

public sealed class EnumStringSerializationTests
{
    private static readonly JsonSerializerOptions StringEnumOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData(GameAction.RollDie, "RollDie")]
    [InlineData(GameAction.StopRolling, "StopRolling")]
    [InlineData(GameAction.PlayShiny, "PlayShiny")]
    [InlineData(GameAction.ResolveTokenSteal, "ResolveTokenSteal")]
    [InlineData(GameAction.EndTurn, "EndTurn")]
    public void GameAction_SerializesAsString(GameAction action, string expectedJson)
    {
        var json = JsonSerializer.Serialize(action, StringEnumOptions);
        Assert.Equal($"\"{expectedJson}\"", json);
    }

    [Theory]
    [InlineData("RollDie", GameAction.RollDie)]
    [InlineData("StopRolling", GameAction.StopRolling)]
    [InlineData("EndTurn", GameAction.EndTurn)]
    public void GameAction_DeserializesFromString(string json, GameAction expected)
    {
        var result = JsonSerializer.Deserialize<GameAction>($"\"{json}\"", StringEnumOptions);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(GameState.RollPhase, "RollPhase")]
    [InlineData(GameState.TokenPhase, "TokenPhase")]
    [InlineData(GameState.GameEnded, "GameEnded")]
    public void GameState_SerializesAsString(GameState state, string expectedJson)
    {
        var json = JsonSerializer.Serialize(state, StringEnumOptions);
        Assert.Equal($"\"{expectedJson}\"", json);
    }

    [Theory]
    [InlineData("RollPhase", GameState.RollPhase)]
    [InlineData("GameEnded", GameState.GameEnded)]
    public void GameState_DeserializesFromString(string json, GameState expected)
    {
        var result = JsonSerializer.Deserialize<GameState>($"\"{json}\"", StringEnumOptions);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(TokenAction.StashTrash, "StashTrash")]
    [InlineData(TokenAction.DoubleStash, "DoubleStash")]
    [InlineData(TokenAction.Steal, "Steal")]
    [InlineData(TokenAction.Recycle, "Recycle")]
    public void TokenAction_SerializesAsString(TokenAction action, string expectedJson)
    {
        var json = JsonSerializer.Serialize(action, StringEnumOptions);
        Assert.Equal($"\"{expectedJson}\"", json);
    }

    [Theory]
    [InlineData("StashTrash", TokenAction.StashTrash)]
    [InlineData("Recycle", TokenAction.Recycle)]
    public void TokenAction_DeserializesFromString(string json, TokenAction expected)
    {
        var result = JsonSerializer.Deserialize<TokenAction>($"\"{json}\"", StringEnumOptions);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(CardName.Shiny, "Shiny")]
    [InlineData(CardName.Feesh, "Feesh")]
    [InlineData(CardName.Doggo, "Doggo")]
    [InlineData(CardName.MmmPie, "MmmPie")]
    public void CardName_SerializesAsString(CardName card, string expectedJson)
    {
        var json = JsonSerializer.Serialize(card, StringEnumOptions);
        Assert.Equal($"\"{expectedJson}\"", json);
    }

    [Theory]
    [InlineData("Shiny", CardName.Shiny)]
    [InlineData("Doggo", CardName.Doggo)]
    public void CardName_DeserializesFromString(string json, CardName expected)
    {
        var result = JsonSerializer.Deserialize<CardName>($"\"{json}\"", StringEnumOptions);
        Assert.Equal(expected, result);
    }
}
