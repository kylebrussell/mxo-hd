using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MartialStarterTacticDamageMode : NativeQaMode
{
    private const ushort Guard = 138, Iron = 139, Aikido = 101, KungFu = 133;
    private const int X = 17000, Y = 495, Z = 6400, DummyZ = 6700, Level = 50;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Aikido, KungFu, Guard, Iron).Order().ToArray();
    private static readonly ushort[] Hotbar = { Aikido, KungFu, Guard, Iron };

    public override string Name => "ability-parity-martial-starter-tactic-damage";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
            "rotation=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, testMobFixture: "wounding-throw.csv", hotbarAbilities: Hotbar, environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.MartialTacticMode] = "1"
    }, readyMessages: new[]
    {
        "Rank 50, unarmed. Page 2: 1 Aikido, 2 Kung-Fu, 3 Guard Breaker, 4 Iron Guard. CSR starts at READY HOME facing the isolated 30,000 HP Melee Dummy 3 m north, away from ambient FX. No teleport needed. If behind the camera, strafe and turn to single-click it. Right-click Close Combat.",
        "Select Kung-Fu (slot 2). Double-click Speed in Actions > Awakened. Enter ?cd ma 138 and then press slot 3. Expect [COMBAT] Round ... Speed ... player wins, result 0x100, player Guard Breaker lands; [ABILITY] 138 ... for 47; [EFFECTS] 138 on Melee Dummy: Stat 1009 -10 15 s.",
        "Double-click Power in Actions > Awakened. Wait out Guard Breaker's 10 s own reuse, enter ?cd ma 138 and press slot 3. Expect the Power round with the same 47 damage and defense reduction.",
        "Switch to Aikido (slot 1). In Power, enter ?cd ma 139 and press slot 4. Expect Iron Guard damage 15 and CSR [MODIFIER] source=139 stats 1009 and 1010 +10 for 12 s. Switch to Speed in Actions > Awakened, wait out the 10 s own reuse, enter ?cd ma 139 and press slot 4. Expect the same 15.",
        "Stay in the same fight. Enter ?cd ma75 once for +75 physical resistance on the dummy. Select Kung-Fu, arm ?cd ma 138 and press slot 3. Switch to Aikido after it lands, wait out the 10 s Iron Guard reuse, enter ?cd ma 139 and press slot 4. Expect [TYPED-RESISTANCE OURS] 138 type0 total75 raw47 final38 and 139 type0 total75 raw15 final12.",
        "QA aid: the ma commands arm one winning exchange for the stated special against this dummy and supply Off Balance for 138. It leaves the normal modifiers, timer, choreography and damage calculations in use. ma75 supplies only the resistance control. No helper actions or Escape."
    }, extraQaMetadata: new()
    {
        ["ScopedStockRows"] = "138,139", ["NativeCloseableRows"] = 2,
        ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
    });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY Melee Dummy {InvisibilityBeltWatcherMode.Warp(X, Y, DummyZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, DummyZ), "Clear outdoor NAV lane");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal complete rank-50 profile");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, dummy);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        string wireHandle = WireFormat.ToHex(BitConverter.GetBytes(handle));
        actor.World.SendRpcs(WithHandle(MartialArtsStarterAdmissionMode.StockOpen), WireFormat.FromHex($"40 {wireHandle} 01 6b"));
        await actor.WaitForRpcAsync($"40 {wireHandle} 00 6b");
        await actor.WaitForRpcAsync("47");
        actor.SendRpc($"81 51 {wireHandle}");
        await StyleAsync(KungFu, MartialArtsStarterAdmissionMode.StockKungFu);
        await TacticAsync(0, "Speed");
        await SampleAsync(Guard, MartialArtsStarterAdmissionMode.StockGuard, "Speed", 47, 47);
        await TacticAsync(1, "Power");
        await SampleAsync(Guard, MartialArtsStarterAdmissionMode.StockGuard, "Power", 47, 47);
        await StyleAsync(Aikido, MartialArtsStarterAdmissionMode.StockAikido);
        await SampleAsync(Iron, MartialArtsStarterAdmissionMode.StockIron, "Power", 15, 15);
        await TacticAsync(0, "Speed");
        await SampleAsync(Iron, MartialArtsStarterAdmissionMode.StockIron, "Speed", 15, 15);
        actor.SendChat("?cd ma75");
        await actor.WaitForTextAsync("Martial starter dummy resistance +75.");
        await StyleAsync(KungFu, MartialArtsStarterAdmissionMode.StockKungFu);
        await SampleAsync(Guard, MartialArtsStarterAdmissionMode.StockGuard, "Speed", 47, 38);
        await StyleAsync(Aikido, MartialArtsStarterAdmissionMode.StockAikido);
        await SampleAsync(Iron, MartialArtsStarterAdmissionMode.StockIron, "Speed", 15, 12);
        Console.WriteLine("MARTIAL STARTER TACTIC BOOTSTRAP PASS: both stock frames, both tactics and typed resistance control.");
        return Array.Empty<byte>();

        byte[] WithHandle(string request) => StarterHackerControlTargetMode.WithHandle(request, handle);

        async Task StyleAsync(ushort id, string frame)
        {
            actor.SendRpc(WithHandle(frame));
            await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {StockAbilityProfile.LittleEndianHex(id)} 00 00");
        }

        async Task TacticAsync(int value, string name)
        {
            actor.SendRpc($"42 {value:x2} 00 00 00 00 00");
            await actor.WaitUntilAsync(() => Regex.IsMatch(server.DebugLog(),
                $@"\[COMBAT\] Round \d+: {name} \("), $"{name} interlock round", HeadlessPlayer.DefaultTimeout);
        }

        async Task SampleAsync(ushort id, string request, string tactic, int raw, int final)
        {
            actor.SkipReceived();
            actor.SendChat($"?cd ma {id}");
            await actor.WaitForTextAsync($"Martial starter fixture armed {id}.");
            int cursor = actor.World.RpcSnapshot().Count;
            int log = server.DebugLog().Length;
            actor.SendRpc(WithHandle(request));
            await EvidenceRetestWire.TimerAsync(actor, cursor, id, AbilityCooldowns.OwnTimer, 10);
            await actor.WaitUntilAsync(() =>
            {
                string recent = server.DebugLog()[log..];
                return Regex.IsMatch(recent, $@"\[COMBAT\] Round \d+: {tactic} \(.*result 0x100, player {AbilityCatalog.Find(id)!.Name} lands") &&
                    Regex.IsMatch(recent, $@"\[ABILITY\] {id} lands on view {dummy.ViewId} for {final}\b") &&
                    (raw == final || recent.Contains($"[TYPED-RESISTANCE OURS] {id} type0 total75 raw{raw} final{final}", StringComparison.Ordinal));
            }, $"Landed {id} for {final} in {tactic}", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, cursor, id, AbilityCooldowns.OwnTimer, 10, ended: true);
        }
    }
}
