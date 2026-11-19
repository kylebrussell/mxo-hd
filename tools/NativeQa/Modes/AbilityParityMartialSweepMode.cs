using HeadlessClient;
using MxoHd.PacketResearch;
using hds;

namespace NativeQa.Modes;

public sealed class AbilityParityMartialSweepMode : NativeQaMode
{
    public override string Name => "ability-parity-martial-sweep";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 101, 132, 133, 269, 264, 270, 410, 263, 533, 426, 271, 272, 406, 268, 411, 412, 532, 534, 265, 535 },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0" },
        readyMessages: new[]
        {
            "Stay at the fixture start. Martial Near, Middle and Outside are 2.5, 3.5 and 4.5 m west. Add the requested actions to the stock hotbar from Actions.",
            "At a Hardline or Load Area, compare Aikido Prowess 269 and Karate Expertise 264 unloaded and loaded. Record passive records, Status and max HP.",
            "With the matching Master or Grandmaster parents loaded, cast Karate Master 1065, Karate Grandmaster 1066 and Aikido Grandmaster 1069 upgrades in succession. Record replacement, self-only max HP changes of +90, +110 and +150, and all three combat modifiers.",
            "Cast Iron Body 271 and Serene Calm 272. Record active and expired melee/ballistic resistance (+44/+48 for 20 s), and melee/ballistic/viral/thrown defense (+57/+51/+54/+49 for 40 s).",
            "From the fixture start with Martial Outside selected, use Swirling Ki Summon 268 in Karate and Ki Burst 406 in Aikido. Expected area hits: Near only for 268, Near and Middle for 406. Verify both accuracy debuffs for 268 and note any Enraged proc for 406.",
            "Interlock Martial Near and reselect the style after Close Combat. Use Grab to make the target Off Balance before Aerial Takedown 411 and Tomo Nage 412. Compare caster resistance for 411 with the target resistance procs for 412.",
            "In Karate, use Speed to Daze the target before Machinegun Kick 532 and Power to Stagger it before Ki-Charged Punch 265. Wrist Throw 534 needs no tactic state. Record Stun and independent immunity together, then try the next move after immunity expires. Use Berserker Attack 535 and record its caster-only buff.",
            "Save the full outgoing ability RPCs, on/off modifier records, target HP and rendered moves. Grouped pass can close seven rows after review. See docs/ABILITY-PARITY-QA.md."
        },
        extraQaMetadata: new() { ["MartialSweepNativeStatus"] = "Pending", ["NativeCloseableRows"] = 7 },
        testMobFixture: "ability-parity-martial-sweep.csv", loadAbilityAncestors: true);

    internal async Task VerifyHeadlessAsync(HeadlessPlayer actor, ServerInstance server)
    {
        await actor.EnterWorldAsync();
        var near = await actor.WaitForViewNamedAsync("Martial Near");
        var middle = await actor.WaitForViewNamedAsync("Martial Middle");
        var outside = await actor.WaitForViewNamedAsync("Martial Outside");
        Check.That(new[] { near, middle, outside }.All(view => BitConverter.ToUInt16(view.Attributes["Health"]) == 30000),
            "All three martial targets are alive and full before the sweep");

        static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));
        void Cast(ushort id, ushort view) => actor.SendRpc($"80 b9 {Le(id)} {Le(view)}");
        async Task Modifier(ushort id, ushort stat, int value, float seconds, bool remove = false)
        {
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Any(record =>
                ManageBonusRecord.TryDecode(record.Bytes, out var modifier) && modifier!.SourceId == id &&
                modifier.TargetId == stat && modifier.SignedValue == value && modifier.Duration == seconds &&
                modifier.AddRemoveType == (remove ? 1 : 0)),
                $"modifier {id}/{stat} {value} for {seconds}s (remove={remove})",
                remove ? TimeSpan.FromSeconds(45) : HeadlessPlayer.DefaultTimeout);
        }
        await Modifier(269, 997, 5, 0);
        await Modifier(264, 997, 7, 0);
        foreach (var (id, stat, value, seconds) in new (ushort, ushort, int, float)[]
        {
            (1065, 106, 90, 0), (1066, 106, 110, 0), (1069, 106, 150, 0),
            (271, 180, 44, 20), (272, 1009, 57, 40)
        })
        {
            Cast(id, outside.ViewId);
            await Modifier(id, stat, value, seconds);
        }
        await Modifier(1065, 106, 90, 0, remove: true);
        await Modifier(1066, 106, 110, 0, remove: true);

        foreach (var (id, style, hits) in new (ushort Id, ushort Style, ushort[] Hits)[]
        {
            (268, 132, new[] { near.ViewId }), (406, 101, new[] { near.ViewId, middle.ViewId })
        })
        {
            Cast(style, 0);
            await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {Le(style)} 00 00");
            int cursor = server.DebugLog().Length;
            Cast(id, outside.ViewId);
            await actor.WaitUntilAsync(() =>
            {
                string log = server.DebugLog()[cursor..];
                return hits.All(view => log.Contains($"[ABILITY] {id} lands on view {view} for ", StringComparison.Ordinal));
            }, $"caster-centred landed targets for {id}", HeadlessPlayer.DefaultTimeout);
            string landed = server.DebugLog()[cursor..];
            foreach (var view in new[] { middle.ViewId, outside.ViewId }.Except(hits))
                Check.That(!landed.Contains($"[ABILITY] {id} lands on view {view} for ", StringComparison.Ordinal),
                    $"{id} does not land outside its caster-centred radius");
            Console.WriteLine($"  {id} hit views {string.Join(", ", hits)} with Martial Outside selected");
        }
        await Modifier(271, 180, 44, 20, remove: true);
        await Modifier(272, 1009, 57, 40, remove: true);
        Console.WriteLine("  passives, three class upgrade records and replacement, and both defense buff expiries verified");
    }
}
