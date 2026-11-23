using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ProxyMasterRecipientMode : NativeQaMode
{
    private const ushort Level = 50, Parent = 599, Grant = 1107, Proxy = 594;
    private const int X = 17000, Y = 495, Z = 6400, PetX = X + 150;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, CombatStyles.CoderStyle, Parent, Proxy)
        .Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
        { (Parent, Grant, new[] { ((ushort)1053, 5) }) };

    public override string Name => "ability-parity-proxy-master-recipient";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,PetX,Z)},innerStrC=700,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.CoderStyle, Proxy, Grant },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CoderPets.ProxyMasterMeterQa] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "1107", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} legal memory. Verified clear outdoor NAV CSR ({X},{Y},{Z}) faces the owned Proxy's spawn ({PetX},{Y},{Z}) at 1.5 m. All of 1107's ranked prerequisites are loaded and 1107 itself is a transient Actions grant.",
            "Choose Coder Style on page 2 slot 1 and summon Remote Proxy 5.0 from slot 2 from rest. Expect L50, 8 s, 114 IS (700->586), 8 s own reuse. Click the visible Proxy once and confirm its name. Before 1107, type ?pet exhaust, then ?pet meter twice about 5 s apart. Ignore reading intervals spanning the reset. Compare delta IS divided by delta tick in [PET METER]. Expect +2 IS/s and cost 10.",
            "With that same pet selected, double-click the rank-zero Proxy Master action in the separate Proxy Master Actions section (or use page 2 slot 3). Save the full first ordinary stock 80 b9 53 04 frame. Expect 2 s, [ABILITY-COST] ability=1107 paid=190 IS=586->396, 15 s own reuse, and [PET] Proxy Master (1107) with the current pet ID, costReduction=25% regenIncrease=50% duration=600.",
            "Repeat ?pet exhaust and two ?pet meter samples on the same pet. Expect +3 IS/s and cost 7.5. Type ?qaunload 599. Its Actions grant and source 599 stat 1053 +5 disappear. The current pet still reports cost 7.5. Reload 599, then /ordersimulacra All Deactivate. Expect that pet's window removed. Summon again from slot 2. The new ID reports cost 10 with no inherited 1107 effect.",
            "The pet meter reads actual live resource ticks without advancing the clock. Focused tests exercise the 600 s expiry boundary. Pet HP regeneration, negative-condition cleanse and the authored original pet resource baseline remain open. Projected DONE +0."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Proxy Master requires repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, PetX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Clear outdoor NAV at ({x},{Y},{Z})");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer!;

        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var owned = AbilityMemoryUiFixture.Decode(caster.CharacterData[7]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.Append(Grant).All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Proxy Master and its Actions grant have every ranked prerequisite in the legal loadout");
        Check.That(!owned.ContainsKey(Grant), "The Actions grant has no owned Archive row");

        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        await StockAbilityProfile.CheckPassivesAsync(caster, new List<object>(), Passives, 0, remove: false);
        caster.SendRpc(AdvancedSimulacraNoticeFixture.CoderStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        int cast = caster.World.RpcSnapshot().Count;
        caster.SendRpc(AdvancedSimulacraNoticeFixture.Proxy5);
        await EvidenceRetestWire.CastBarAsync(caster, Proxy, 8);
        await EvidenceRetestWire.TimerAsync(caster, cast, Proxy, AbilityCooldowns.OwnTimer, 8);
        var registered = await StockAbilityProfile.ObserveAsync(caster, cast, frame => frame.Opcode == 0x176, "Ordinary Proxy window");
        byte id = registered.Bytes[3];
        var pet = await AdvancedSimulacraNoticeFixture.PetViewAsync(caster, caster.CharId, id);
        Check.Equal((byte)Level, pet.Attributes["Level"][0], "The stock summon yields a level-50 Proxy");
        Check.That(EngageFoesFixture.Position(pet).SequenceEqual(new double[] { PetX, Y, Z }), "The Proxy is on its visible NAV stand");
        Check.That(server.DebugLog().Contains("[ABILITY-COST] char=110 ability=594 paid=114 IS=700->586"),
            "The captured summon pays the discounted amount once");
        await ProxyMasterResourceFixture.CheckRateAsync(caster, id, 2, 10);

        int dismissed = caster.World.RpcSnapshot().Count;
        caster.SendRpc(AdvancedSimulacraNoticeFixture.Deactivate);
        await StockAbilityProfile.ObserveAsync(caster, dismissed,
            frame => frame.Bytes is [0x81, 0x77, var removed] && removed == id, "Captured Deactivate removes the original Proxy");
        Check.Equal(0, caster.World.Problems.Count, "Grant and ordinary Proxy wire parsing");
        Console.WriteLine("PROXY MASTER RECIPIENT BOOTSTRAP PASS: retained Coder Style, 594 and Deactivate; visible pet, grant and baseline resource ticks. The first complete 1107 frame is reserved for stock QA.");
        return Array.Empty<byte>();
    }
}
