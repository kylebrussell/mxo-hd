using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class DeathPresentationTests
{
    [Fact]
    public void ExportedClientTablesDefineTheBuiltinConstantsAndActualCorpseAssets()
    {
        using var evidence = System.Text.Json.JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("character_movement_evidence.json")));
        var data = evidence.RootElement;
        Assert.Equal("Prone", data.GetProperty("stance_names")[DeathPresentation.Prone].GetString());
        Assert.Equal("Stand", data.GetProperty("stance_names")[DeathPresentation.Standing].GetString());
        Assert.Equal("Idle", data.GetProperty("action_names")[DeathPresentation.Idle].GetString());
        Assert.Equal("Resurrect", data.GetProperty("action_names")[DeathPresentation.Resurrect].GetString());
        Assert.DoesNotContain(data.GetProperty("action_names").EnumerateArray(), a => a.GetString() == "Die");
        Assert.Contains(data.GetProperty("corpse_transitions").EnumerateArray(), a => a.GetProperty("name").GetString() == "Stand to Prone");
    }

    [Fact]
    public void CorpseUsesProneIdleWithoutInventingAPlayerCurrentState()
    {
        byte[] own = DeathPresentation.Build(true, 9, DeathPresentation.Prone, DeathPresentation.Idle,
            DeathPresentation.NoAbility, 1, 2, 3, 0x12345678);
        Assert.Equal(StringUtils.hexStringToBytes("020001280940050000100000803f000000400000404078563412"), own);
        byte[] other = DeathPresentation.Build(false, 9, DeathPresentation.Prone, DeathPresentation.Idle,
            DeathPresentation.NoAbility, 1, 2, 3);
        Assert.Equal(StringUtils.hexStringToBytes("012a094005000010000000803f0000004000004040"), other);
    }

    [Theory]
    [InlineData(325)] [InlineData(326)] [InlineData(375)] [InlineData(416)]
    public void ResurrectionUsesBuiltin63AndItsAbilityContextInsteadOfNamedEmote25(ushort ability)
    {
        byte[] own = DeathPresentation.Build(true, 10, DeathPresentation.Standing, DeathPresentation.Resurrect,
            ability, 0, 0, 0, 1);
        Assert.Equal(26, own.Length);
        Assert.Equal(new byte[] { 2, 0, 1, 0x28, 10, 0x40, 0, 0x3f }, own[..8]);
        Assert.Equal(ability, BitConverter.ToUInt16(own, 8));
        Assert.Equal(1u, BitConverter.ToUInt32(own, 22));
    }

    [Theory]
    [InlineData("resurrect")] [InlineData("newdeath")] [InlineData("world")] [InlineData("disconnect")]
    [InlineData("duelstand")] [InlineData("newduel")]
    public void DelayedCorpseCannotCrossLifeOrWorldTransitions(string transition)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData(); c.playerData.isDead = true; c.playerData.setOnWorld(true);
        c.playerInstance = new PlayerCharacter(); c.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("<IsBot>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(c,true);
        c.Alive = true;
        if (transition.StartsWith("duel") || transition == "newduel") { c.playerData.isDead = false; c.playerData.duelDeath = true; }
        DeathPresentation.AfterInterlockTeardown(c);
        if (transition == "resurrect") c.playerData.isDead = false;
        if (transition == "duelstand") c.playerData.duelDeath = false;
        if (transition == "newduel") c.playerData.lifeGeneration++;
        if (transition == "newdeath") c.playerData.lifeGeneration++;
        if (transition == "world") { c.playerData.setOnWorld(false); c.playerData.setOnWorld(true); }
        if (transition == "disconnect") c.Alive = false;
        DelayedActions.RunDue(double.MaxValue);
        Assert.Equal((byte)0, c.playerData.emoteCounter);
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, true)] [InlineData(false, true, true)]
    public void BothDeathKindsAreCorpseStates(bool dead, bool duel, bool expected)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData { isDead = dead, duelDeath = duel };
        Assert.Equal(expected, DeathPresentation.IsCorpse(c));
    }
}
