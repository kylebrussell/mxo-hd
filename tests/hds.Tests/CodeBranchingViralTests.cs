using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using NativeQa;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class CodeBranchingViralTests
{
    [Fact]
    public void CodeBranchingRank50BonusesAndViralResistanceDeltaAreExact()
    {
        var plan = NativeQaModes.RequireKnown("ability-parity-code-branching-viral").Plan;
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

        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (328, 50) });
        Assert.Equal(5, AbilityPassives.Total(client, 1143));
        Assert.Equal(5, AbilityPassives.Total(client, 1027));
        Assert.Equal(10, AbilityPassives.Total(client, 1142));
        Assert.Equal(10, AbilityPassives.Total(client, 996));
        Assert.Equal(10, AbilityPassives.Total(client, 1148));

        // Ancestor 95 Transmit Virus gives +15% (33 pts -> 590); loading 328 raises total to +25% (55 pts -> 556).
        Assert.Equal((ushort)590, TypedAbilityResistance.Amount(640, 50, 50, 33));
        Assert.Equal((ushort)556, TypedAbilityResistance.Amount(640, 50, 50, 55));
    }
}
