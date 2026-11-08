using hds;
using hds.shared;

namespace hds.Tests;

// Shares the static DelayedActions timer queue: running due actions in one class would fire the other's.
[Collection("DelayedActions")]
public class AbilityBuffTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void ModifierRecordsMatchRetail()
    {
        // Overclock (abilities_useoverclocked): 80 bc 15 00 42 01 00 47 01 00 00 ac 00 2b 00 00 00 00 00 00 a0 41
        Assert.Equal("80bc1500420100470100" + "00ac002b000000" + "00" + "0000a041",
            Hex(AbilityBuffs.BuildModifier(0x15, 0x142, 327, 172, 43, false, 20f)));
        // Consistent Technique ending (entilsar lastday2): ... 06 00 00 00 b4 00 eb 00 00 00 01 00 00 a0 41
        Assert.Equal("80bc15009d0100060000" + "00b400eb000000" + "01" + "0000a041",
            Hex(AbilityBuffs.BuildModifier(0x15, 0x19d, 6, 180, 235, true, 20f)));
        // Force Enhancer's negative value (crashover2k proxy_024): 6d 00 c3 fe ff ff
        Assert.Equal("80bc1500e200007f0100" + "006d00c3feffff" + "00" + "0000f041",
            Hex(AbilityBuffs.BuildModifier(0x15, 0xe2, 383, 109, -317, false, 30f)));
    }

    [Fact]
    public void ExclusiveSwitchMatchesRetail()
    {
        // Evade Combat on (evadecombat.txt) and Corpsman off (crashover2k proxy_024)
        Assert.Equal("80bd05d900000000000001", Hex(AbilityBuffs.BuildExclusive(217, true)));
        Assert.Equal("80bd054404000000000000", Hex(AbilityBuffs.BuildExclusive(1092, false)));
    }

    [Fact]
    public void EvadeRecordsMatchRetail()
    {
        // evadecombat.txt: 80 bc 19 00 88 00 00 d9 00 00 00 70 02 00 00 00 00 00 00 00 00 00
        //                  80 bc 1d 00 89 00 00 70 02 00 00 00 00 00 00 00 00 00 00 00 00 00
        var records = AbilityBuffs.BuildRecords(217, AbilityBuffs.Retail[217], new ushort[] { 0x88, 0x89 }, false);
        Assert.Equal("80bc19008800" + "00d9000000" + "7002" + "00000000" + "00" + "00000000", Hex(records[0]));
        Assert.Equal("80bc1d008900" + "0070020000" + "0000" + "00000000" + "00" + "00000000", Hex(records[1]));
    }

    [Fact]
    public void MovementUpdatesMatchRetail()
    {
        // Hyper-Speed ending (entilsar lastday2 21:56:07): 02 00 02 80 88 00 00 00 00 80 80 80 02 00 00
        Assert.Equal("020002808800000000808080020000", Hex(AbilityBuffs.BuildMovementUpdate(0f, 0)));
        // Hyper-Sprint (luizoe old sniffer Unknown): 02 00 02 80 88 9a 99 19 3e 80 90 4e 00 b0 19 31 46 12 02 02 .. 04
        Assert.Equal("0200028088" + "9a99193e" + "8090" + "4e00" + "b0" + "19314612" + "02" + "02" + "0004",
            Hex(AbilityBuffs.BuildMovementBuffUpdate(AbilityBuffs.Retail[184], 0x4e, 2)));
    }

    [Fact]
    public void HyperSpeedOwnViewEndsWithRetailDissemblingType()
    {
        // ... b0 56 06 00 28 03 02 00 10, and no 00 00 of its own: the packet closes the object section.
        Assert.EndsWith("b056060028" + "03" + "020010", Hex(ServerPackets.BuildHyperSpeedUpdate(0x13a, 3)));
    }

    [Fact]
    public void RetailCostsAndDurations()
    {
        Assert.Equal(40, AbilityCosts.Cost(12));     // Hyper-Speed
        Assert.Equal(25, AbilityCosts.Cost(184));    // Hyper-Sprint
        Assert.Equal(40, AbilityCosts.Cost(217));    // Evade Combat
        Assert.Equal(11, AbilityCosts.Cost(329));    // Logic Blast 3.0
        Assert.Equal(20, AbilityCosts.Cost(77));     // Repair RSI 1.0: no retail measurement
        Assert.Equal(20f, AbilityBuffs.Retail[327].Seconds); // Overclock
        Assert.Equal(600f, AbilityBuffs.Retail[12].Seconds); // Hyper-Speed
        Assert.Equal(0f, AbilityBuffs.Retail[217].Seconds);  // Evade Combat: until replaced
    }

    private static WorldClient NewClient()
    {
        WorldClient client = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        // Only the queue and player data, and a last flush far in the future so FlushQueue always defers:
        // the tests read what was queued.
        client.playerData = new ClientData();
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private static List<string> Rpcs(WorldClient client)
    {
        List<string> list = new List<string>();
        foreach (SequencedMessage m in client.messageQueue.RPCMessagesQueue)
        {
            // Queued RPCs carry their one-byte length first
            list.Add(Hex(m.content).Substring(2));
        }
        client.messageQueue.RPCMessagesQueue.Clear();
        client.messageQueue.ObjectMessagesQueue.Clear();
        return list;
    }

    [Fact]
    public void TimedBuffComesOffWithTheSameRecords()
    {
        WorldClient client = NewClient();
        Assert.True(AbilityBuffs.Apply(client, 327));
        List<string> on = Rpcs(client);
        Assert.Single(on);
        Assert.StartsWith("80bc1500" + "0001" + "00470100", on[0]);
        Assert.Contains((ushort)327, client.playerData.activeBuffs.Keys);

        // The expiry runs on the shared timer
        DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds() + 21);
        Assert.DoesNotContain((ushort)327, client.playerData.activeBuffs.Keys);
        List<string> off = Rpcs(client);
        Assert.Single(off);
        Assert.Equal(on[0].Substring(0, 34) + "01" + on[0].Substring(36), off[0]);
    }

    [Fact]
    public void FamilyMemberReplacesTheOther()
    {
        WorldClient client = NewClient();
        AbilityBuffs.Apply(client, 1092); // Corpsman
        Rpcs(client);
        AbilityBuffs.Apply(client, 1089); // Team Healer
        List<string> rpcs = Rpcs(client);
        // Corpsman's three records off, its switch off, Team Healer's switch on and four records on
        Assert.Equal(9, rpcs.Count);
        Assert.All(rpcs.GetRange(0, 3), r => Assert.Equal("01", r.Substring(34, 2)));
        Assert.Equal("80bd054404000000000000", rpcs[3]);
        Assert.Equal("80bd054104000000000001", rpcs[4]);
        Assert.Equal(new ushort[] { 1089 }, client.playerData.activeBuffs.Keys);
    }

    [Fact]
    public void ReusedBuffRefreshesAndOldExpiryDoesNothing()
    {
        WorldClient client = NewClient();
        AbilityBuffs.Apply(client, 6);
        double now = TimeUtils.GetSimTimeSeconds();
        AbilityBuffs.Apply(client, 6);
        Rpcs(client);
        // Both expiries are due; only the second application's removes the buff, once
        DelayedActions.RunDue(now + 25);
        Assert.Single(Rpcs(client));
        Assert.Empty(client.playerData.activeBuffs);
    }

    [Fact]
    public void NonBuffAbilitiesAreIgnored()
    {
        WorldClient client = NewClient();
        Assert.False(AbilityBuffs.Apply(client, 198)); // Cheap Shot
        Assert.Empty(Rpcs(client));
    }

    [Fact]
    public void EvadeShieldOwnView()
    {
        // evadecombat.txt: EvadeShieldHealth ff (own-view attribute 30) when Evade Combat goes on
        Assert.Equal("020002808080800" + "4ff", Hex(AbilityBuffs.BuildEvadeShieldUpdate(0xff)));
    }

    [Fact]
    public void EvadeTogglesOffAndStartsItsCooldownThen()
    {
        WorldClient client = NewClient();
        Assert.True(AbilityBuffs.CooldownStartsOnUse(198));
        Assert.False(AbilityBuffs.CooldownStartsOnUse(217));
        Assert.False(AbilityBuffs.IsToggledOn(client, 217));

        AbilityBuffs.Apply(client, 217);
        List<string> on = Rpcs(client);
        Assert.Equal("80bd05d900000000000001", on[0]);
        Assert.True(AbilityBuffs.IsToggledOn(client, 217));

        AbilityBuffs.Remove(client, 217);
        List<string> off = Rpcs(client);
        Assert.Contains("80bd05d900000000000000", off);
        // entilsar lastday2 21:57:09: 80 bd 01 d9 00 04 00 00 f0 41 01 (30 s)
        Assert.Contains("80bd01d900040000f04101", off);
        Assert.True(AbilityCooldowns.IsCoolingDown(client.playerData, 217, TimeUtils.GetSimTimeSeconds()));
        DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds() + 31);
        Rpcs(client);
    }
}
