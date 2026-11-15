using hds;
namespace hds.Tests;
public class MissionContainerClaimTests
{
    private static MissionObjectState State() => new() { def = new MissionObjectDef { id = 7 }, lootOpen = true };
    private static MissionObjective Goal(int goal, uint item) => new() { goal = goal, objectId = 7, item = item, type = MissionGoalType.Search };

    [Fact]
    public void ClaimsExactWindowIdentityAndClosesBeforeStateOrGoalCallbacks()
    {
        var state = State(); var goal = Goal(1, 6799); var events = new List<string>(); byte[]? reply = null;
        Assert.True(MissionContainerClaim.Take(state, new[] { goal }, 0x1234, _ => true,
            (claims, empty) => { Assert.False(state.lootOpen); Assert.True(empty); reply = ServerPackets.BuildLootAccepted(claims); events.Add("ack"); events.Add(Convert.ToHexString(ServerPackets.BuildLootClosed())); },
            () => events.Add("opened"), _ => events.Add("goal")));
        Assert.Equal("81200C0000000000000000000100003412828F1A0000", Convert.ToHexString(reply!));
        Assert.Equal(new[] { "ack", "811A", "opened", "goal" }, events);
        Assert.False(MissionContainerClaim.Take(state, new[] { goal }, 0x1234, _ => throw new Exception("Duplicate grant"),
            (_, _) => throw new Exception("Duplicate reply"), () => { }, _ => { }));
    }
    [Fact]
    public void FullBagLeavesSelectionAndGoalRetryableWithoutRemovingUnclaimedIcon()
    {
        var state = State(); var goal = Goal(1, 6799); int completed = 0;
        MissionContainerClaim.Take(state, new[] { goal }, 1, _ => false,
            (claims, empty) => { Assert.Empty(claims); Assert.False(empty); Assert.True(state.lootOpen); },
            () => throw new Exception("Must not mark empty"), _ => completed++);
        Assert.Equal(0, completed);
        MissionContainerClaim.Take(state, new[] { goal }, 1, _ => true,
            (claims, empty) => { Assert.Single(claims); Assert.True(empty); }, () => { }, _ => completed++);
        Assert.Equal(1, completed);
    }
    [Fact]
    public void PartialClaimAcknowledgesOnlyTransferredItemAndRetryDoesNotReplayIt()
    {
        var state = State(); var first = Goal(1, 6799); var second = Goal(2, 6800);
        var completed = new List<MissionObjective>(); var granted = new List<uint>();
        MissionContainerClaim.Take(state, new[] { first, second }, 2, item => { if (item == 6800) return false; granted.Add(item); return true; },
            (claims, empty) => { Assert.False(empty); Assert.Equal(6799u, Assert.Single(claims).Item); }, () => { }, completed.Add);
        Assert.Same(first, Assert.Single(completed)); Assert.True(state.lootOpen);
        MissionContainerClaim.Take(state, new[] { second }, 2, item => { granted.Add(item); return true; },
            (claims, empty) => { Assert.True(empty); Assert.Equal(6800u, Assert.Single(claims).Item); }, () => { }, completed.Add);
        Assert.Equal(new uint[] { 6799, 6800 }, granted); Assert.False(state.lootOpen);
    }
    [Fact]
    public void PendingClaimsReserveDifferentSlotsBeforeTheSingleCheckpoint()
    {
        Assert.Equal((ushort)3, MissionContainerClaim.FreeSlot(new[] { 1, 97 }, new ushort[] { 2 }));
        Assert.Equal((ushort)0, MissionContainerClaim.FreeSlot(Enumerable.Range(1, 95), new ushort[] { 96 }));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClaimAndCloseOutputWaitsForDurableCommit(bool commit)
    {
        var q = new MessageQueue();
        using (var output = MissionOutput.Begin(q))
        {
            MissionContainerClaim.Take(State(), new[] { Goal(1, 6799) }, 1, _ => true,
                (claims, empty) => { q.addRpcMessage(ServerPackets.BuildLootAccepted(claims)); if (empty) q.addRpcMessage(ServerPackets.BuildLootClosed()); },
                () => q.addObjectMessage(new byte[] { 3 }, false), _ => q.addRpcMessage(new byte[] { 0x80, 0xa0 }));
            Assert.Empty(q.RPCMessagesQueue); Assert.Empty(q.ObjectMessagesQueue);
            if (commit) output.Commit();
        }
        Assert.Equal(commit ? 3 : 0, q.RPCMessagesQueue.Count);
        Assert.Equal(commit ? 1 : 0, q.ObjectMessagesQueue.Count);
    }
}
