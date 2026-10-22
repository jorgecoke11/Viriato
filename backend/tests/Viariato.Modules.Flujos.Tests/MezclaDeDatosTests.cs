using System.Text.Json;
using Viariato.Modules.Flujos.Esquemas;
using Xunit;

namespace Viariato.Modules.Flujos.Tests;

public sealed class MezclaDeDatosTests
{
    private static JsonElement Mezclar(string? base_, string? encima) => JsonDocument.Parse(MezclaDeDatos.Mezclar(base_, encima) ?? "null").RootElement;

    [Fact]
    public void WhatTheTopOneHasWins_AndWhatItLacksComesFromTheBase()
    {
        var r = Mezclar("""{"ticker":"AAPL","mercado":"NYSE"}""", """{"ticker":"MSFT"}""");

        Assert.Equal("MSFT", r.GetProperty("ticker").GetString());
        Assert.Equal("NYSE", r.GetProperty("mercado").GetString());
    }

    [Fact]
    public void ObjectsMergeAllTheWayDown_ListsAreReplacedWhole()
    {
        var r = Mezclar("""{"dir":{"calle":"Mayor","cp":"31001"},"tags":["a","b","c"]}""", """{"dir":{"cp":"28001"},"tags":["z"]}""");

        Assert.Equal("Mayor", r.GetProperty("dir").GetProperty("calle").GetString());
        Assert.Equal("28001", r.GetProperty("dir").GetProperty("cp").GetString());
        Assert.Equal(["z"], r.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList());
    }

    [Fact]
    public void AnExplicitNull_OnTop_EmptiesTheField()
    {
        Assert.Equal(JsonValueKind.Null, Mezclar("""{"nota":"x"}""", """{"nota":null}""").GetProperty("nota").ValueKind);
    }

    [Fact]
    public void WithNothingOnOneSide_TheOtherIsTakenAsIs()
    {
        Assert.Null(MezclaDeDatos.Mezclar(null, " "));
        Assert.Equal("""{"a":1}""", MezclaDeDatos.Mezclar(null, """{"a":1}"""));
        Assert.Equal("""{"a":1}""", MezclaDeDatos.Mezclar("""{"a":1}""", null));
        Assert.Equal("Ñandú", Mezclar("""{"a":1}""", """{"b":"Ñandú"}""").GetProperty("b").GetString());
    }
}
