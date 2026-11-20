using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

public class DisguiseGrantRegistrationTests
{
    private const ushort DisguiseParent = 120, DisguiseGrant = 1114;

    private static WorldClient Client(ushort rank = 12)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.worldGeneration = 1;
        client.playerData.passives.Loaded[DisguiseParent] = rank;
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, new WorldSendFence());
        return client;
    }

    [Theory]
    [InlineData(6, 6, 34)]
    [InlineData(12, 12, 37)]
    [InlineData(50, 50, 56)]
    [InlineData(99, 50, 56)]
    public void DisguiseGrantScalesWithLearnedRankAndFeedsDuration(ushort learned, ushort expectedRank, double expectedSeconds)
    {
        Assert.True(AbilityGrantRegistration.Handles(DisguiseParent, DisguiseGrant));
        Assert.False(AbilityGrantRegistration.Handles(DisguiseParent, 1111));
        Assert.Equal(expectedRank, AbilityProgression.GrantLevel(DisguiseParent, learned));
        Assert.Equal(expectedSeconds, AbilityDisguise.Seconds(expectedRank, 0));
    }

    [Fact]
    public void DisguiseGrantRegistersAtWorldReadinessAndReconcilesOnUnloadAndReload()
    {
        const ushort rank = 12;
        var client = Client(rank);

        AbilityGrantRegistration.LoadingComplete(client, new byte[] { 1 });
        byte[][] initial = client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Select(m => m.content.AsSpan(1).ToArray()).ToArray();
        Assert.Equal(2, initial.Length);
        Assert.Equal(ServerPackets.BuildAbilityLoad(DisguiseGrant, rank, AbilityProgression.HiddenSlot), initial[0]);
        Assert.Equal(AbilityGrantRegistration.Record(new(DisguiseParent, DisguiseGrant, rank), false), initial[1]);

        client.playerData.passives.Loaded.Remove(DisguiseParent);
        AbilityGrantRegistration.LoadedChanged(client);
        byte[][] afterUnload = client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Skip(2).Select(m => m.content.AsSpan(1).ToArray()).ToArray();
        Assert.Equal(2, afterUnload.Length);
        Assert.Equal(ServerPackets.BuildAbilityUnload(DisguiseGrant), afterUnload[0]);
        Assert.Equal(AbilityGrantRegistration.Record(new(DisguiseParent, DisguiseGrant, rank), true), afterUnload[1]);

        client.playerData.passives.Loaded[DisguiseParent] = rank;
        AbilityGrantRegistration.LoadedChanged(client);
        byte[][] afterReload = client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Skip(4).Select(m => m.content.AsSpan(1).ToArray()).ToArray();
        Assert.Equal(2, afterReload.Length);
        Assert.Equal(ServerPackets.BuildAbilityLoad(DisguiseGrant, rank, AbilityProgression.HiddenSlot), afterReload[0]);
        Assert.Equal(AbilityGrantRegistration.Record(new(DisguiseParent, DisguiseGrant, rank), false), afterReload[1]);
    }
}
