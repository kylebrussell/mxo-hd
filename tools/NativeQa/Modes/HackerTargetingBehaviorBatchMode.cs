using System.Buffers.Binary;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class HackerTargetingBehaviorBatchMode : NativeQaMode
{
    private const ushort HackerStyle = 1006, Overclock = 327, LogicBarrage = 330, Resolve = 354, Health = 476;
    private static readonly ushort[] Loadout = Profile(2, 17, HackerStyle, Overclock, LogicBarrage, Resolve, Health).Order().ToArray();
    // Stock Logic Barrage request from PR321 7a4fc7efcf1c:1742. Other targeted casts adapt the same form.
    private const string SelectedViral = "80b94a010e000c000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string SelfViral = "80b947010000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-hacker-targeting-and-damage";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET healthC=5000,healthM=5000,innerStrC=500,innerStrM=500,y=495 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            "DELETE FROM inventory WHERE charId=110 AND slot IN (1,105); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,692,1,1,0)"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new ushort[] { HackerStyle, Resolve, Health, Overclock, LogicBarrage },
        environmentOverrides: new()
        {
            ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "327,330,354,476", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            "Step A. Run ?combatdepth foundations-sight and choose Hacker Style on page 2 slot 1. Clear selection and try slots 2 and 3. Expect a single invalid-target line for any sent request, with no cast or reuse.",
            "Step B. Select AB1 Blocked across the gap to the west. Use Destroy Resolve (slot 2). If sent, the ability cannot reach or affect the NPC. Select AB1 Clear 2 m east and use Destroy Health (slot 3). Check -520 maximum HP and -20% regeneration records for 40s.",
            "Step C. On AB1 Clear, press Logic Barrage (slot 5) with Overclock off and record its damage. When reuse clears, use Overclock (slot 4), then use Logic Barrage again. Record the +43 viral bonus and increased hit. The bonus expires after 20s."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= 100, "The four audit rows share one level-50 memory profile");
        await caster.EnterWorldAsync();
        var server = context.ActualServer!;
        caster.SendChat("?combatdepth foundations-sight");
        await caster.WaitForTextAsync("across a mapped gap");
        var blocked = await caster.WaitForViewNamedAsync("AB1 Blocked");
        var clear = await caster.WaitForViewNamedAsync("AB1 Clear");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        await StarterFixedDamageMode.SelectStyle(caster, self, HackerStyle, 172);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        foreach (ushort ability in new[] { Resolve, Health })
        {
            await StarterHackerControlTargetMode.RefusedAsync(caster, ability, Request(ability, 0));
            await StarterHackerControlTargetMode.RefusedAsync(caster, ability, Request(ability, Handle(clear) ^ 0x10000));
        }
        int cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Request(Resolve, Handle(blocked)));
        await EvidenceRetestWire.TimerAsync(caster, cursor, Resolve, AbilityCooldowns.OwnTimer, 120);
        await caster.WaitUntilAsync(() => server.DebugLog().Contains($"[ABILITY] Sight blocked ability {Resolve} on view {blocked.ViewId}"),
            "Destroy Resolve cannot cross the mapped gap", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog().Contains("[FREECAST-VIRAL] ability=354") &&
            !server.DebugLog().Contains("[EFFECTS] 354 on AB1 Blocked"), "Obstructed casts reach neither the contest nor effect execution");

        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Request(Health, Handle(clear)));
        await EvidenceRetestWire.TimerAsync(caster, cursor, Health, AbilityCooldowns.OwnTimer, 120);
        await caster.WaitUntilAsync(() => server.DebugLog().Split('\n').Any(line => line.Contains("[EFFECTS] 476 on AB1 Clear:") &&
            line.Contains("Stat 106 -520 40 s") && line.Contains("Stat 112 -20 40 s")), "Destroy Health applies both shipped effects on the clear target", HeadlessPlayer.DefaultTimeout);

        int influence = caster.World.RpcSnapshot().Select(packet => ManageBonusRecord.TryDecode(packet.Bytes, out var record) ? record : null)
            .Where(record => record is { AddRemoveType: 0, TargetId: 172 or 1031 })
            .GroupBy(record => record!.Locator).Sum(group => group.Last()!.SignedValue);
        await BarrageAsync(influence);
        await EvidenceRetestWire.TimerAsync(caster, 0, LogicBarrage, AbilityCooldowns.OwnTimer, 4, ended: true);
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(SelfViral);
        await ObserveAsync(caster, cursor, packet => Bonus(packet, Overclock, 172, 43, 20, remove: false), "Overclock adds the retained +43 modifier");
        await EvidenceRetestWire.TimerAsync(caster, cursor, Overclock, AbilityCooldowns.OwnTimer, 90);
        await BarrageAsync(influence + 43);
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(cursor)
            .Any(packet => Bonus(packet, Overclock, 172, 43, 20, remove: true)),
            "Overclock's modifier expires", TimeSpan.FromSeconds(23));
        await BarrageAsync(influence);
        Check.Equal(0, caster.World.Problems.Count, "Hacker targeting and behavior requests remain framed");
        return Array.Empty<byte>();

        async Task BarrageAsync(int percent)
        {
            int offset = server.DebugLog().Length;
            int timers = caster.World.RpcSnapshot().Count;
            caster.SendRpc(Request(LogicBarrage, Handle(clear)));
            var hit = new Regex($@"\[ABILITY\] 330 lands on view {clear.ViewId} for (\d+)");
            await caster.WaitUntilAsync(() => hit.IsMatch(server.DebugLog()[offset..]),
                "Logic Barrage hits the same clear target", HeadlessPlayer.DefaultTimeout);
            int expected = (int)(424 * (double)(float)(1 + percent / 100.0));
            Check.Equal(expected, int.Parse(hit.Match(server.DebugLog()[offset..]).Groups[1].Value),
                $"Logic Barrage uses the active viral influence ({percent}%)");
            await EvidenceRetestWire.TimerAsync(caster, timers, LogicBarrage, AbilityCooldowns.OwnTimer, 4);
            await EvidenceRetestWire.TimerAsync(caster, timers, AbilityCooldowns.VirusEnhancedDamage, AbilityCooldowns.GroupTimer, 4);
        }
    }

    private static uint Handle(ViewCreation target) => DuelPackets.Handle(target.ViewId, target.SpawnId);

    private static byte[] Request(ushort ability, uint handle)
    {
        byte[] request = Convert.FromHexString(SelectedViral);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), handle);
        return request;
    }
}
