using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class ZenMasterSpecialsMode : NativeQaMode
{
    private const ushort Technique = 860, SweepTheFloor = 869, SpinningFlower = 870;
    private const byte FixtureLevel = 99, FixtureRank = 50;
    private static readonly ushort[] Roots = { 2, 17, Technique, SweepTheFloor, SpinningFlower };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-zen-master-specials";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(PlayerProgression.MaxLevel)},y={CombatFixtureLoadout.GroundY}," +
                "healthC=3000,healthM=3000,innerStrC=2500,innerStrM=2500 WHERE charId=110",
            $"UPDATE char_abilities SET level={FixtureRank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true, loadAbilityAncestors: false, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new ushort[] { SpinningFlower, SweepTheFloor },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_CONDITIONAL_THROW_QA"] = "1",
            [QaLoadout.ZenTechniqueMode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "860,869,870",
            ["NativeCloseableRows"] = 0,
            ["BehaviorTestRows"] = "860,869,870",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(50)
        },
        readyMessages: new[]
        {
            "Level 99 diagnostic (ReqCharLevel=99 for Zen Master), rank 50 unarmed with Melee Dummy 3 m west (46/100 memory; default Self Defense melee influence = +29%).",
            "Step A (Technique 860 passive). Inspect Status on login, run '?qaunload 860' (46 -> 44 memory: Viral Defense 183 drops by 198, Aikido Damage 572 drops by 99), then '?qaload 860' restoring exact login Status. This disposable mode uses the seeded level 99 for the ordinary Technique load qualification.",
            "Step B (Zen Master interlock specials). Enter ordinary unarmed Close Combat on Melee Dummy (keep default Self Defense, do not switch styles). Press slot 1 (Spinning Flower 870): expect 40 IS, 10s/grp688 5s reuse, 419 physical dmg (base 325 + 29%), Powerless 24.6 s (State 96) + 25% Combat Tactics (162) -10 for 15 s. Select Melee Dummy, '?combatdepth offbalance 30', press slot 2 (Sweep The Floor 869, CastRequiredStates=4 Off-Balance): expect 40 IS, 10s/grp687 5s reuse, 402 physical dmg (base 312 + 29%), Disarmed 20 s (State 231)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var database = context.OwnedDatabase!;
        await actor.EnterWorldAsync();
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(FixtureLevel, self.Attributes["Level"][0], "The logged-in fixture has stored diagnostic level 99");
        var stats = new (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[]
        {
            (Technique, 0, new (ushort, int)[] { (183, 198), (572, 99) })
        };
        var proof = new List<object>();
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, stats, 0, remove: false);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Single(row => AbilityCatalog.IdOf(row.Descriptor) == Technique);
        foreach (bool unload in new[] { true, false })
        {
            string packet = unload ? "80 b3 5c 03" : "80 b2 5c 03";
            var ack = BytePattern.Parse(packet);
            // Native chat uses this same dev bridge, which submits its normal 80 ae record using the owned slot/rank.
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendChat(unload ? "?qaunload 860" : "?qaload 860");
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame => ack.IsMatch(frame.Bytes),
                $"Technique native {(unload ? "unload" : "load")} acknowledgement");
            await StockAbilityProfile.CheckPassivesAsync(actor, proof, stats, cursor, remove: unload);
            long stored = await database.LongAsync($"SELECT is_loaded FROM char_abilities WHERE char_id=110 AND slot={archive.Slot}");
            Check.Equal(unload ? 0L : 1L, stored, "Technique load state commits in the disposable archive");
        }
        Check.Equal(0, actor.World.Problems.Count, "Technique loadout framing");
        return Array.Empty<byte>();
    }
}
