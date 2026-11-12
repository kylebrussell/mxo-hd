using hds;
using hds.shared;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace hds.Tests;

// Expected bytes from three retail player deaths (afterwhoruneo pvp, morpheuzzz pvp, afterwhoruneo killed by an Agent).
[Collection("DelayedActions")]
public class DeathTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void DeathSetsIsDeadHealthAndConditionFlags()
    {
        Assert.Equal("020002808080c1010000808001" + "00000000", Hex(ServerPackets.BuildSelfDeathUpdate()));
        Assert.Equal("020002808080c1010000808001" + "00000001", Hex(ServerPackets.BuildSelfDeathUpdate(PvpFlag.PvpBit)));
    }

    [Theory]
    [InlineData(false, 0u)]
    [InlineData(true, PvpFlag.PvpBit)]
    public void DeathClearsTemporaryCombatConditionsWhilePreservingTheChosenPvpFlag(bool pvp, uint expected)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData { isBot = true, pvpFlag = pvp };
        client.playerData.setCharID(900000123);
        client.playerData.conditionStateFlags = PvpFlag.InCombatBit | CombatInterlock.ConditionBit(CombatTactic.Speed) |
            CombatInterlock.ConditionBit(CombatTactic.Grab) | CombatInterlock.ConditionBit(CombatTactic.Power);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Health.setValue((ushort)100);
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.InnerStrengthMax.setValue((ushort)200);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("<IsBot>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, true);
        CombatHandler.HandlePlayerDeath(client);
        Assert.True(client.playerData.isDead);
        Assert.Equal(0u, client.playerData.conditionStateFlags);
        Assert.Equal(expected, PvpFlag.FlagsOf(client));
        Assert.Equal(expected, NumericalUtils.ByteArrayToUint32(client.playerInstance.ConditionStateFlags.getValue(), 1));
        Assert.Equal((ushort)0, NumericalUtils.ByteArrayToUint16(client.playerInstance.Health.getValue(), 1));
    }

    [Fact]
    public void DeathMessageIsTheRetail36Bytes()
    {
        byte[] message = ServerPackets.BuildDeathMessage();
        Assert.Equal(36, message.Length);
        Assert.StartsWith("2e67" + "0000000000000000000000" + "92000060", Hex(message));
    }

    [Fact]
    public void RespawnHealsAndClearsConditionFlags()
    {
        // Retail: 02 00 02 80 80 80 c0 28 0a 80 80 01 00 00 00 00 (MaxHealth 0x0a28)
        Assert.Equal("020002808080c0" + "280a" + "8080" + "01" + "00000000", Hex(ServerPackets.BuildSelfRespawnHeal(0x0a28)));
    }

    [Fact]
    public void RespawnFinishPicksTheLoadingArea()
    {
        Assert.Equal("80fd" + "000000000101", Hex(ServerPackets.BuildRespawnToLoadingArea()));
    }

    [Fact]
    public void HardlineExitToLoadingAreaIsTheNonDeathVariant()
    {
        // crashover2k proxy_second_pc_001_exit_to_loadingarea: client 80 fc 01, server 80 fd 00 00 00 00 01 00,
        // client 80 fe 01, then 81 07 and the Loading Area.
        Assert.Equal("80fd" + "000000000100", Hex(ServerPackets.BuildJackoutToLoadingArea()));
    }

    [Theory]
    [InlineData(1000u, 1000u, false)]
    [InlineData(1000u, 1001u, false)]   // the old check (start - now > 5) wrapped here and finished at once
    [InlineData(1000u, 1029u, false)]
    [InlineData(1000u, 1030u, true)]    // retail: 80 fd 00 00 00 00 00 00 30 s after 80 fc 02
    [InlineData(1000u, 999u, false)]
    public void JackoutFinishesAfterThirtySeconds(uint start, uint now, bool finished)
    {
        Assert.Equal(finished, TeleportHandler.JackoutFinished(start, now));
    }
}
