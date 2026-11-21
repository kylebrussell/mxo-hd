using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AwakenedStyleRankRefreshMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 2300;
    private const ushort Awakened = 2, SelfDefense = CombatStyles.SelfDefense, Coder = CombatStyles.CoderStyle;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Awakened, Coder).Order().ToArray();
    // Complete Coder selection request from PR521.
    private const string StockCoder = "80b9f0030400020000000000000000000000000000000000000000000000000084d75741086b0000000000";

    public override string Name => "ability-parity-awakened-style-rank-refresh";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=IF(ability_id=2147485696,15,{Level}) WHERE char_id=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},cash=2000000,y={Y} WHERE charId=110"
    }, Array.Empty<string>(), DevCommands: true, hotbarAbilities: new[] { SelfDefense, Coder },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "2,17,1008", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay on the verified outdoor NAV stand (17000,495,2300), with no bag or world target. On login Awakened 15 grants hidden Self Defense 15. Expect [MODIFIER] source=2 stat=204 add value=+3.",
            "While Self Defense is active, open Character > Upgrade and train Awakened one rank. Confirm 15→16 for 51,200 Info. Expect [TRAINING] ability=2 level=16 cost=51200, source=2 stat=204 +3 removed and +4 added, and source=17 stat=1009 +14→+15, stat=522 +15→+16. Selection stays on Self Defense.",
            "With selection clear, switch to Coder Style on page 2 slot 2. Train Awakened one more rank, 16→17 for 57,800 Info. Coder Style stays active and 2/204 stays +4 with no remove/re-add. Type ?qaunload 1008. Self Defense falls back at rank 17: source=17 stat=159 +11, stat=1009 +16, stat=522 +17. No [VITALS] or [PVP] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Training needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
            "Training stand is on outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            rows.Keys.ToHashSet().SetEquals(Loadout) && !rows.ContainsKey(SelfDefense) && rows[Awakened].Level == 15,
            "The style is granted rather than owned, and the complete memory profile fits");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(SelfDefense, false, 15),
            "Hidden Self Defense starts at its granting rank");
        await BonusAsync(0, Awakened, 204, 3, false);
        await StyleAsync(0, SelfDefense, 15);

        int start = await UpgradeAsync(16);
        await BonusAsync(start, Awakened, 204, 3, true);
        await BonusAsync(start, Awakened, 204, 4, false);
        foreach (var modifier in CombatStyles.Modifiers(SelfDefense))
        {
            int before = Value(modifier, 15), after = Value(modifier, 16);
            if (before == after) continue;
            await BonusAsync(start, SelfDefense, modifier.Target, before, true);
            await BonusAsync(start, SelfDefense, modifier.Target, after, false);
        }
        Check.That(!HasStyleSwitch(start, SelfDefense), "Training the grant doesn't toggle off the active Self Defense style");

        int styleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockCoder);
        await StyleAsync(styleAt, Coder, Level);
        start = await UpgradeAsync(17);
        Check.That(!HasStyleSwitch(start, Coder) && !actor.World.RpcSnapshot().Skip(start).Any(packet =>
            StockAbilityProfile.Bonus(packet, Awakened, 204, 4, 0, false) ||
            StockAbilityProfile.Bonus(packet, Awakened, 204, 4, 0, true)), "Unchanged Awareness and active Coder records are retained");

        int unloadAt = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { Coder }, load: false);
        await StyleAsync(unloadAt, SelfDefense, 17);
        Check.Equal(0, actor.World.Problems.Count, "Live rank refresh framing");
        return Array.Empty<byte>();

        static int Value(AbilityCatalog.Modifier modifier, ushort rank)
        {
            Check.That(AbilityValues.TryValue(modifier, rank, Level, out int value), "Style snapshot is stored");
            return value;
        }

        async Task<int> UpgradeAsync(ushort rank)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            // Complete captured 80 b7 layout with the current archive slot and a one-rank increment.
            actor.SendRpc($"80 b7 02 00 {StockAbilityProfile.LittleEndianHex(rows[Awakened].Slot)} 01");
            var result = ServerPackets.BuildAbilityUpgraded(Awakened, rank);
            await StockAbilityProfile.ObserveAsync(actor, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(result),
                $"Awakened trains live to {rank}");
            await StockAbilityProfile.ObserveAsync(actor, cursor, StockAbilityProfile.GrantRecord(SelfDefense, false, rank),
                $"Self Defense replaces its registered hidden rank with {rank}");
            return cursor;
        }

        async Task StyleAsync(int cursor, ushort style, ushort rank)
        {
            foreach (var modifier in CombatStyles.Modifiers(style))
                await BonusAsync(cursor, style, modifier.Target, Value(modifier, rank), false);
        }

        Task<RpcRecord> BonusAsync(int cursor, ushort source, ushort stat, int value, bool remove) =>
            StockAbilityProfile.ObserveAsync(actor, cursor, packet => StockAbilityProfile.Bonus(packet, source, stat, value, 0, remove),
                $"{source}/{stat} {(remove ? "removes" : "adds")} {value}");

        bool HasStyleSwitch(int cursor, ushort style)
        {
            var off = AbilityBuffs.BuildExclusive(style, false);
            return actor.World.RpcSnapshot().Skip(cursor).Any(packet => packet.Bytes.AsSpan().SequenceEqual(off));
        }
    }
}
