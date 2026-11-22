using hds;
using HeadlessClient;
using static HeadlessClient.AdvancedSimulacraNoticeFixture;

namespace NativeQa.Modes;

public sealed class AdvancedSimulacraOwnerNoticeMode : NativeQaMode
{
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private static readonly ushort[] Hotbar = { 1008, Proxy, Nuker };

    public override string Name => "ability-parity-advanced-simulacra-owner-notice";
    public override NativeQaModePlan Plan { get; } = new(
        Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            $"rotation={MobAggro.YawTowards(X,Z,X+150,Z)},innerStrC=600,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={ObserverX},y={Y},z={Z} WHERE charId=6111"
        },
        DevCommands: true, hotbarAbilities: Hotbar, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "594,616", ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, legal ranked profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Verified Mara NAV CSR ({X},{Y},{Z}) faces pet stand ({X+150},{Y},{Z}); observer QAFriend ({ObserverX},{Y},{Z}). No world object selection needed.",
            "Choose Coder Style on page 2 slot 1. Press slot 2 for Remote Proxy 5.0. After 8 s expect a level-38 proxy from the six-sheet stock pool, one 81 76 window, [ABILITY-COST] paid=120 IS=600->480 and 8 s own reuse. One visible owner notice reads 'You summoned a level 38 <sheet> Simulacrum.' Check the System tab if needed.",
            "Use slot 3 for Logic Daemon 4.0. Retain the complete first stock 616 request. After 8 s expect the old 81 77, one new 81 76, level-38 Nuker, [ABILITY-COST] paid=150 IS=480->330 and 10 s own reuse. The owner sees one 'You summoned a level 38 Nuker Simulacrum.' Finally type /ordersimulacra All Deactivate and confirm the new pet and only its window disappear.",
            "The notice uses the existing private stock-visible system channel and server wording. The original localized string ID and optional Proxy console override remain open."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Advanced summons require repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, X + 150, ObserverX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Visible outdoor NAV stand ({x},{Y},{Z})");
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } owner) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(owner.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Full ranked summon profile");
        await owner.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, owner);
        owner.SendRpc(CoderStyle);
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        owner.SendRpc(Proxy5);
        var registration = await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
        byte id = registration.Bytes[3];
        var pet = await PetViewAsync(owner, owner.CharId, id);
        await PetViewAsync(observer, owner.CharId, id);
        await owner.WaitForTextAsync($"You summoned a level {Level} {pet.Name}.");
        Check.That(!observer.World.RpcSnapshot().Any(IsNotice), "The stock-visible private notice only reaches its owner");
        owner.SendRpc(Deactivate);
        await owner.WaitForRpcAsync($"81 77 {id:x2}");
        Check.Equal(0, owner.World.Problems.Count + observer.World.Problems.Count, "Advanced summon views and notices decode cleanly");
        Console.WriteLine("ADVANCED SUMMON NOTICE NATIVE BOOTSTRAP PASS: retained Coder/594/Deactivate frames, one private owner notice. No 616 activation sent.");
        return Array.Empty<byte>();
    }
}
