using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class SpecialistDuelistGrantMode : NativeQaMode
{
    private const ushort Specialist = 495, Duelist = 497, Desperation = 1149, Initiative = 1044;
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, CurrentHealth = 1, MaxHealth = 500, Snapshot = 108;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Desperation, Specialist, Duelist).Order().ToArray();
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (Specialist, 1072, new (ushort, int)[] { (1046, 1), (995, 2), (1139, 4) }),
        (Duelist, 1076, new (ushort, int)[] { (1046, 2), (1027, 5), (1042, 2) })
    };

    public override string Name => "ability-parity-specialist-duelist-grants";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
            $"healthC={CurrentHealth},healthM={MaxHealth},innerStrC=500,innerStrM=500 WHERE charId=110",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
    }, DevCommands: true, hotbarAbilities: new[] { Desperation }, environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
    }, extraQaMetadata: new() { ["ScopedStockRows"] = "495,497", ["NativeCloseableRows"] = 2 }, readyMessages: new[]
    {
        $"Level {Level}, legal {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory with full prerequisites. Outdoor CSR ({X},{Y},{Z}), HP {CurrentHealth}/{MaxHealth} held, 500 IS. No target or bag interactions. Page 2 slot 1 Desperation.",
        "In Actions confirm SMG Specialist Upgrade 1072 and Duelist Upgrade 1076 each appear once. In DebugLog, source 495 [MODIFIER] adds 1046 +1, 995 +2, 1139 +4. Source 497 adds 1046 +2, 1027 +5, 1042 +2.",
        "Use ?qaunload 495 and ?qaload 495. Check only 1072 leaves and returns and only source 495's three records remove and return. Repeat with ?qaunload 497 and ?qaload 497 for 1076 and its three source 497 records. Both upgrades present before the next step.",
        "With HP 1/500 and both disciplines loaded, press Desperation. Expect 10 IS (500 to 490), own 90 s reuse and [MODIFIER] source 1149 stat 1044 +108 for 20 s. The other loaded disciplines contribute +6 to 1046 and the two tested sources add +1/+2. Unload both after the cast. The grants and six records retire while Desperation stays +108 until its own 20 s removal. The short ?cd initiative command reads the current snapshot. No [PVP] lines."
    });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var stand = StarterConcealedShotTargetingMode.Stand(X, Z);
        Console.WriteLine($"CSR {stand.Command}");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.Equal(93, Loadout.Sum(AbilityMemory.Cost), "Both complete discipline branches fit together");
        Check.That(Passives.All(entry => !rows.ContainsKey(entry.Child)), "Both children are granted live, never owned in Margin");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var own = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal((ushort)CurrentHealth, BitConverter.ToUInt16(own.Attributes["Health"]), "Low HP separates each Initiative Bonus contribution");

        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Passives, 0, remove: false);
        foreach (var passive in Passives)
        {
            Check.Equal(1, actor.World.RpcSnapshot().Count(GrantRecord(passive.Child, remove: false)),
                $"One live grant {passive.Child} at readiness");
            var other = Passives.Single(candidate => candidate.Parent != passive.Parent);
            int removed = await ChangeLoadoutAsync(actor, rows, new[] { passive.Parent }, load: false);
            await CheckPassivesAsync(actor, proof, new[] { passive }, removed, remove: true);
            int restored = await ChangeLoadoutAsync(actor, rows, new[] { passive.Parent }, load: true);
            await CheckPassivesAsync(actor, proof, new[] { passive }, restored, remove: false);
            Check.That(!actor.World.RpcSnapshot().Skip(removed).Any(GrantRecord(other.Child, remove: true)),
                $"{other.Child} stays available while {passive.Parent} changes membership");
        }

        int castAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterInitiativeBatchMode.StockCast);
        await ObserveAsync(actor, castAt, frame => Bonus(frame, Desperation, Initiative, Snapshot, 20, false),
            "Stock Desperation samples both loaded disciplines exactly once");
        await EvidenceRetestWire.TimerAsync(actor, castAt, Desperation, AbilityCooldowns.OwnTimer, 90);
        int bothOff = await ChangeLoadoutAsync(actor, rows, Passives.Select(entry => entry.Parent).ToArray(), load: false);
        await CheckPassivesAsync(actor, proof, Passives, bothOff, remove: true);
        actor.SendChat("?cd initiative");
        await actor.WaitForTextAsync($"bonus={Snapshot}");
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(castAt).Any(frame =>
            Bonus(frame, Desperation, Initiative, Snapshot, 20, true)), "The original snapshot retires at 20 seconds", TimeSpan.FromSeconds(24));
        actor.SendChat("?cd initiative");
        await actor.WaitForTextAsync("bonus=0");
        Check.Equal(0, actor.World.Problems.Count, "Archive, grant and stock activation framing");
        Console.WriteLine("SPECIALIST DUELIST BOOTSTRAP PASS: both actionable grants, isolated passive round-trips and real Initiative +108 through its 20-second lifetime.");
        return JsonSerializer.SerializeToUtf8Bytes(proof);
    }
}
