using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class TraumaSurgeonParentRadiusMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, Level = 50;
    private const ushort Style = CombatStyles.HackerStyle, Parent = 439, Upgrade = 1093, Heal = 371;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Parent, Heal).Order().ToArray();
    private static readonly (string Name, int Id, int Z)[] Helpers =
    {
        ("QAFriend", 6111, 7260), ("QAFar", 6112, 7270), ("QAOther", 6113, 7350)
    };
    // Retained AfterWhoruNeo proxy_024 lines 12135–36. Only the active view/spawn is rebound.
    private const string HealStock = "80b973010d005a0000000000000000000000000000000000000000002000000078f21200086b0000000000";

    public override string Name => "ability-parity-trauma-parent-radius";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=0,healthC=1000,healthM=5000,innerStrC=800,innerStrM=1200,alignment=1,pvpflag=0 WHERE charId=110",
        "UPDATE characters SET healthC=100,healthM=5000,alignment=1,pvpflag=0 WHERE charId BETWEEN 6111 AND 6113",
        "DELETE FROM char_abilities WHERE char_id BETWEEN 6111 AND 6113"
    }.Concat(Helpers.Select(p => $"UPDATE characters SET x={X},y={Y},z={p.Z} WHERE charId={p.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, Heal, Upgrade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = Parent.ToString(), ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/100 memory. Stay at verified HOME. Accept QAFriend's team invite, select Hacker Style on page 2 and /target QAFriend. Slots 2/3 are Restore Group 2.0 and the granted upgrade. No combat or random aid.",
            $"At login verify source={Parent} stat=1054 +5 and the single {Upgrade} grant. Cast slot 2, then repeat after ?qaunload {Parent} and ?qaload {Parent}. Wait for the family timer each time. Each cast must heal CSR and QAFriend at 8.60 m. QAFar (8.70 m) and QAOther (9.50 m) must receive no heal. Save the modifier and grant removal/restoration.",
            $"After reload cast slot 3, then slot 2. Expect [MODIFIER] source={Upgrade} stat=127 +10, and Heal 371 on CSR, QAFriend and QAFar only. Cast slot 2 after ?dismissbuff {Upgrade}; QAFar must drop out again. The owned 1054 record never substitutes for a direct 127 modifier."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Repository NAV is required.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        var server = context.ActualServer ?? throw new InvalidOperationException("Disposable server required.");
        foreach (var helper in Helpers)
        {
            Console.WriteLine($"READY {helper.Name} {InvisibilityBeltWatcherMode.Warp(X, Y, helper.Z, 128, inside: false)}");
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, helper.Z), $"Outdoor sight to {helper.Name}");
            var player = await HeadlessPlayer.LoginAsync(context.Address, helper.Name, helper.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Name, player);
            await player.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        var leader = context.Helpers["QAFriend"];
        await TeamBreadthMode.InviteAsync(leader, context.Helpers["QAFar"]);
        await TeamBreadthMode.InviteAsync(leader, context.Helpers["QAOther"]);
        if (context.StockProbe is not { } actor)
        {
            _ = StarterTeamPatcherUpgradeRadiusMode.InviteNativeAsync(leader, server);
            return Array.Empty<byte>();
        }

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await TeamBreadthMode.InviteAsync(leader, actor);
        var selected = await actor.WaitForViewNamedAsync(leader.Handle);
        uint handle = DuelPackets.Handle(selected.ViewId, selected.SpawnId);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, 0, record => StockAbilityProfile.Bonus(record, Style, 172, 27, 0, false), "Hacker Style");
        var proof = new List<object>();
        var passive = new (ushort, ushort, (ushort, int)[])[] { (Parent, Upgrade, new (ushort, int)[] { (1054, 5) }) };
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, passive, 0, false);
        await CastAsync(expanded: false);
        int removed = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: false);
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, passive, removed, true);
        await CastAsync(expanded: false);
        int restored = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: true);
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, passive, restored, false);
        await CastAsync(expanded: false);
        // The 1093 request is reserved for native QA. The replay covers the parent-only comparison.
        return Array.Empty<byte>();

        async Task CastAsync(bool expanded)
        {
            int start = actor.World.RpcSnapshot().Count;
            int at = server.DebugLog().Length;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(HealStock, handle));
            await EvidenceRetestWire.CastBarAsync(actor, Heal, 4);
            await EvidenceRetestWire.TimerAsync(actor, start, 685, AbilityCooldowns.GroupTimer, 4, ended: true);
            string effects = server.DebugLog()[at..];
            foreach (string name in new[] { actor.Handle, leader.Handle, "QAFar", "QAOther" })
            {
                bool eligible = name == actor.Handle || name == leader.Handle || (expanded && name == "QAFar");
                Check.Equal(eligible, effects.Contains($"Heal {Heal} -> {name} for ", StringComparison.Ordinal),
                    $"Restore Group recipient {name}, direct radius active={expanded}");
            }
        }
    }
}
