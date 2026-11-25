using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using NativeQa;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class EnhancedPotencyViralTests
{
    [Fact]
    public void EnhancedPotencyRank50BonusesAndViralResistanceDeltaAreExact()
    {
        var plan = NativeQaModes.RequireKnown("ability-parity-enhanced-potency-viral").Plan;
        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.True(mem <= AbilityMemory.ForLevel(50), $"Memory {mem} exceeds level-50 cap");

        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(110);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.isBot = true;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)50);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);

        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (422, 50) });
        Assert.Equal(10, AbilityPassives.Total(client, 1142));
        Assert.Equal(10, AbilityPassives.Total(client, 1139));
        Assert.Equal(5, AbilityPassives.Total(client, 1146));
        Assert.Equal(5, AbilityPassives.Total(client, 1148));

        // 5% of (4*50 + 20) = 11 viral resistance points -> 640 -> 623 (17 resisted).
        Assert.Equal((ushort)623, TypedAbilityResistance.Amount(640, 50, 50, 11));
    }
}
