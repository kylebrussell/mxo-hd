using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class TraumaSurgeonSupportMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, Z = 6400;
    private const ushort Hacker = 1006, Trauma = 439, Upgrade = 1093, Master = 425, MasterUpgrade = 1096, Repairs = 371;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, Trauma, Master, Repairs, 433).Order().ToArray();
    private static readonly (string Handle, int X, int Z)[] Stands =
    {
        ("QAFriend", X + 200, Z), ("QAFar", X, Z + 900), ("QAOther", X, Z + 950)
    };
    // Retained AfterWhoruNeo proxy_024 lines 12135–12136. Only the current target view/spawn changes.
    private const string RepairsStock = "80b973010d005a0000000000000000000000000000000000000000002000000078f21200086b0000000000";

    public override string Name => "ability-parity-trauma-surgeon-support";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=0," +
            "healthC=1000,healthM=5000,innerStrC=1000,innerStrM=2000,alignment=1,pvpflag=0 WHERE charId=110",
        $"UPDATE characters SET y={Y},healthC=100,healthM=5000,alignment=1,pvpflag=0 WHERE charId BETWEEN 6111 AND 6113",
        $"UPDATE characters SET x={Stands[0].X},z={Stands[0].Z} WHERE charId=6111",
        $"UPDATE characters SET x={Stands[1].X},z={Stands[1].Z} WHERE charId=6112",
        $"UPDATE characters SET x={Stands[2].X},z={Stands[2].Z} WHERE charId=6113",
        "DELETE FROM char_abilities WHERE char_id BETWEEN 6111 AND 6113"
    }, DevCommands: true, hotbarAbilities: new[] { Hacker, Repairs, Upgrade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "1093,425", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Level 50, 94/100 memory. Outdoor NAV CSR (17000,495,6400) faces the two far stands at z7300 and z7350. Accept QAFriend's team invite. QAFriend stands 2 m east and the other two teammates stand 9 and 9.5 m away.",
            "Page 2 slots 1–3 are Hacker Style, Restore Group 2.0 (371), and Trauma Surgeon (1093). Choose Hacker Style, /target QAOther, then use slot 2. CSR and QAFriend heal for 28. Neither far teammate heals.",
            "Use ?qaunload 425, then ?qaload 425. Expect source=425 stat=1090 value=+5 removed and added once, and the single 1096 Actions grant removed and restored. Record the passive and grant; this fixture uses no interruption cast.",
            "Keep QAOther selected and use slot 3. Retain the first full outgoing 1093 frame. Expect CSR-only [MODIFIER] source=1093 adds 127 +10, 174 +10, and 564 +7, a 20 IS debit and 30 s own reuse. Use slot 2 again: CSR, QAFriend, and QAFar heal for 30, while QAOther at 9.5 m stays outside.",
            "When the 30 s reuse clears, toggle slot 3 off. Capture all three removals. Use slot 2 once more to verify the baseline 28 heal and far exclusion."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Missing Trauma Surgeon fixture server");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing Trauma Surgeon NAV source");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Stand ({x},{Y},{z}) is on outdoor NAV");

        foreach (var (handle, _, _) in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var leader = context.Helpers["QAFriend"];
        var edge = context.Helpers["QAFar"];
        var outside = context.Helpers["QAOther"];
        await TeamBreadthMode.InviteAsync(leader, edge);
        await TeamBreadthMode.InviteAsync(leader, outside);
        if (context.StockProbe is not { } actor)
        {
            _ = StarterTeamPatcherUpgradeRadiusMode.InviteNativeAsync(leader, server);
            return Array.Empty<byte>();
        }

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) == 94 && 94 <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Ordinary ranked prerequisites fit the 94-memory Archive without seeding either active grant");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await TeamBreadthMode.InviteAsync(leader, actor);
        var selection = await actor.WaitForViewNamedAsync(outside.Handle);
        uint target = DuelPackets.Handle(selection.ViewId, selection.SpawnId);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Upgrade, remove: false), "Trauma Surgeon grants 1093");
        await MasterAsync(0, remove: false);
        int cast = actor.World.RpcSnapshot().Count;
        int log = server.DebugLog().Length;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(RepairsStock, target));
        await EvidenceRetestWire.CastBarAsync(actor, Repairs, 4);
        await EvidenceRetestWire.TimerAsync(actor, cast, 685, AbilityCooldowns.GroupTimer, 4, ended: true);
        string heal = server.DebugLog()[log..];
        Check.That(heal.Contains("Heal 371 -> CSR_Tester for 28") && heal.Contains("Heal 371 -> QAFriend for 28"),
            "The exact stock 371 heals the caster and near teammate for 28 before 1093");
        Check.That(!heal.Contains("Heal 371 -> QAFar for ") && !heal.Contains("Heal 371 -> QAOther for "),
            "Neither far teammate is within the baseline radius");

        int unload = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Master }, load: false);
        await MasterAsync(unload, remove: true);
        int reload = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Master }, load: true);
        await MasterAsync(reload, remove: false);
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Team, Archive and exact 371 traffic frame cleanly");
        Console.WriteLine("TRAUMA SURGEON BASELINE PASS: 94 memory, 371 heals 28, and 425/1090 +5 with 1096 both round-trip.");
        return Array.Empty<byte>();

        async Task MasterAsync(int cursor, bool remove)
        {
            bool Bonus(RpcRecord record) => StockAbilityProfile.Bonus(record, Master, 1090, 5, 0, remove);
            var grant = StockAbilityProfile.GrantRecord(MasterUpgrade, remove);
            await StockAbilityProfile.ObserveAsync(actor, cursor, Bonus, $"Upgrade Master +5 Concentration Bonus, remove={remove}");
            await StockAbilityProfile.ObserveAsync(actor, cursor, grant, $"Upgrade Master 1096 grant, remove={remove}");
            var records = actor.World.RpcSnapshot().Skip(cursor);
            Check.Equal(1, records.Count(Bonus), "One 425 bonus transition");
            Check.Equal(1, records.Count(grant), "One 1096 grant transition");
        }
    }
}
