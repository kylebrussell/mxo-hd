using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class AtRestMotionFlushTests
{
    private static readonly FieldInfo Pending = typeof(WorldClient).GetField("flushPending", BindingFlags.NonPublic | BindingFlags.Instance)!;

    [Theory]
    [InlineData((ushort)454, (byte)0x60)]
    [InlineData((ushort)130, (byte)0x5d)]
    [InlineData((ushort)279, (byte)0x40)]
    public void TheOwnMotionIsFlushedWhenTheCastLands(ushort ability, byte action)
    {
        var caster = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        caster.playerData = new ClientData();
        caster.playerData.setCharID(110);
        caster.playerData.setEntityId(110);
        caster.playerInstance = new PlayerCharacter();
        caster.messageQueue = new MessageQueue();
        caster.viewMan = new ViewManager();
        caster.Alive = true;
        // A just-flushed client: FlushQueue inside the throttle window marks the queue pending instead of sending.
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(caster, long.MaxValue);
        caster.viewMan.addView(6114, 12).viewCreated = true;
        var previous = WorldServer.Clients;
        WorldServer.Clients = new Dictionary<string, WorldClient>();
        try
        {
            Assert.False((bool)Pending.GetValue(caster)!);
            StealthShieldAbilities.PlayThrow(caster, 6114, ability, action);
            var own = Assert.Single(caster.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>()).content;
            Assert.Equal(action, own[7]);
            // The caster's queue was flushed at landing, not left for an unrelated later flush.
            Assert.True((bool)Pending.GetValue(caster)!);
        }
        finally { WorldServer.Clients = previous; }
    }
}
