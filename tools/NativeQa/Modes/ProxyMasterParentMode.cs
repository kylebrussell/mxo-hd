using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class ProxyMasterParentMode : NativeQaMode
{
    private const ushort Level = 50, Parent = 599, Grant = 1107, Proxy = 594, Coder = 1008;
    private const int X = 17000, Y = 495, Z = 6400, PetX = 17150;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Parent, Proxy).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
        { (Parent, Grant, new[] { ((ushort)1053, 5) }) };

    public override string Name => "ability-parity-proxy-master-parent";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
            $"x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,PetX,Z)},innerStrC=600,innerStrM=1000 WHERE charId=110" },
        DevCommands: true, hotbarAbilities: new ushort[] { Coder, Proxy }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "599", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} legal memory; CSR ({X},{Y},{Z}) faces the pet stand ({PetX},{Y},{Z}) on clear Mara NAV. No target or bag input. Proxy Master 599 is loaded at login. Look for its rank-zero Proxy Master action 1107 under Actions and 599/1053 +5 in Bonuses. 1107 is a grant, absent from the owned Archive.",
            "Choose Coder Style on page 2 slot 1. Cast Remote Proxy 5.0 from slot 2 with selection clear. Expect 114 IS (600->486), 8 s cast and 8 s own reuse. Dismiss with /ordersimulacra All Deactivate.",
            "Enter ?qaunload 599. The 1107 Actions entry and 599/1053 +5 are removed. After 594 reuse, slot 2 again: 120 IS (486->366). Dismiss. Enter ?qaload 599. Confirm both reappear once, then use slot 2 after reuse: 114 IS (366->252). Dismiss. Leave 1107 uncast so this verifies the parent and its grant."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Proxy Master requires repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int standX in new[] { X, PetX })
            Check.That(NavMesh.Locate(1, standX, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, standX, Y, Z), $"Clear outdoor NAV at ({standX},{Y},{Z})");
        if (context.StockProbe is not { } owner) return Array.Empty<byte>();
        var server = context.ActualServer!;

        var loaded = AbilityMemoryUiFixture.Decode(owner.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var owned = AbilityMemoryUiFixture.Decode(owner.CharacterData[7]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out var row) &&
                row.Level >= need.Value)), "The full stock loadout fits with every ranked prerequisite");
        Check.That(owned.ContainsKey(Parent) && !owned.ContainsKey(Grant), "The child never occupies an owned Archive slot");

        await owner.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, owner);
        var proof = new List<object>();
        await StockAbilityProfile.CheckPassivesAsync(owner, proof, Passives, 0, remove: false);
        owner.SendRpc(AdvancedSimulacraNoticeFixture.CoderStyle);
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        await SummonAsync(114, 600, 486);

        int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(owner, owned, new[] { Parent }, load: false);
        await StockAbilityProfile.CheckPassivesAsync(owner, proof, Passives, unloaded, remove: true);
        await Task.Delay(TimeSpan.FromSeconds(8.25));
        await SummonAsync(120, 486, 366);

        int reloaded = await StockAbilityProfile.ChangeLoadoutAsync(owner, owned, new[] { Parent }, load: true);
        await StockAbilityProfile.CheckPassivesAsync(owner, proof, Passives, reloaded, remove: false);
        await Task.Delay(TimeSpan.FromSeconds(8.25));
        await SummonAsync(114, 366, 252);
        Check.Equal(0, owner.World.Problems.Count, "Proxy Master grant and ordinary summons decode cleanly");
        Console.WriteLine("PROXY MASTER PARENT PASS: rank-zero grant appears, disappears and returns; exact stock 594 costs 114/120/114 IS.");
        return Array.Empty<byte>();

        async Task SummonAsync(ushort cost, ushort before, ushort after)
        {
            int rpcs = owner.World.RpcSnapshot().Count;
            int views = owner.World.ViewSnapshot().Count;
            int logAt = server.DebugLog().Length;
            owner.SkipReceived();
            owner.SendRpc(AdvancedSimulacraNoticeFixture.Proxy5);
            await EvidenceRetestWire.CastBarAsync(owner, Proxy, 8);
            var window = await StockAbilityProfile.ObserveAsync(owner, rpcs, frame => frame.Opcode == 0x176, "One level-50 Proxy window");
            byte id = window.Bytes[3];
            var pet = await AdvancedSimulacraNoticeFixture.PetViewAsync(owner, owner.CharId, id);
            Check.Equal((byte)Level, pet.Attributes["Level"][0], "Ordinary summon still reaches its natural L50 cap");
            await EvidenceRetestWire.TimerAsync(owner, rpcs, Proxy, AbilityCooldowns.OwnTimer, 8);
            await owner.WaitUntilAsync(() => owner.World.ViewSnapshot().Skip(views)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(after),
                $"Ordinary stock summon debits IS {before}->{after}", HeadlessPlayer.DefaultTimeout);
            Check.Equal(1, server.DebugLog()[logAt..].Split($"ability={Proxy} paid={cost} IS={before}->{after}").Length - 1,
                "One exact resource debit for the stock request");
            int dismiss = owner.World.RpcSnapshot().Count;
            owner.SendRpc(AdvancedSimulacraNoticeFixture.Deactivate);
            await StockAbilityProfile.ObserveAsync(owner, dismiss,
                frame => frame.Bytes is [0x81, 0x77, var removed] && removed == id, "The current Proxy dismisses normally");
        }
    }
}
