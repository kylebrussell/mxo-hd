using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class EmpBlastParityMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400, NearZ = 6700, FarZ = 7600, FriendX = 17150;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private const ushort EmpBlast = AbilityEmpBlast.Ability; // 1025
    private static readonly ushort[] Loadout = { 2, 17, EmpBlast };

    public override string Name => "ability-parity-emp-blast";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0,alignment=1,healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z},rotation=192,alignment=1 WHERE charId=6111",
        "DELETE FROM char_abilities WHERE char_id=6111",
        $"UPDATE char_abilities SET level=1 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(EmpBlast)}"
    }, DevCommands: true, hotbarAbilities: new ushort[] { EmpBlast, 17 }, testMobFixture: "emp-blast.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1025", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        }, readyMessages: new[]
        {
            $"Level {Level}, EMCP Blast (1025) loaded ({Loadout.Sum(AbilityMemory.Cost)}/100 mem). Verified outdoor HOME ({X},{Y},{Z}) facing +Z ('{Home}'). Near hostile Melee Dummy 3 m N ({X},{Y},{NearZ}, inside 10 m radius); Far Dummy 12 m N ({X},{Y},{FarZ}, outside 10 m radius); friendly QAFriend 1.5 m E ({FriendX},{Y},{Z}). Page 2: 1=EMCP Blast (1025), 2=Self Defense (17).",
            "Step 1 (Unweakened baseline): Press slot 1 (1025) at rest -> 1.0 s cast, 30.0 s own reuse, caster FX 0x28000a43, '[EMP] <nearView> damage=0 hp=30000' (Far Dummy excluded by 10 m radius, QAFriend untouched).",
            "Step 2 (Weakened 5000 damage inside 10 m, excluded outside 10 m): Click Melee Dummy (3 m N) and type '?cd empweakness on 6000'; click Far Dummy (12 m N) and type '?cd empweakness on 6000'. After 30 s reuse clears, press slot 1 (1025) at rest -> 1.0 s cast, 30.0 s reuse, Melee Dummy takes 5000 damage ('[EMP] <nearView> damage=5000 hp=1000'), while Far Dummy stays at 6000 HP.",
            "Step 3 (In-combat refusal + kill): Open Close Combat on Melee Dummy and press slot 1 (1025) -> refused MustBeOutsideCombat (22) with 0 IS spent and no cast/reuse; type '?cd stop' to disengage. After 30 s reuse clears, press slot 1 (1025) at rest -> kills Melee Dummy ('[EMP] <nearView> damage=5000 hp=0'), Far Dummy stays alive at 6000 HP."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("EMCP Blast mode requires NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.Equal(Home, InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false), "Printed HOME");
        InvisibilityBeltWatcherMode.Warp(X, Y, NearZ, 128, inside: false);
        InvisibilityBeltWatcherMode.Warp(X, Y, FarZ, 128, inside: false);
        InvisibilityBeltWatcherMode.Warp(FriendX, Y, Z, 192, inside: false);
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, NearZ), "Clear LOS to near dummy (3 m)");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, FarZ), "Clear LOS to far dummy (12 m)");

        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var near = await actor.WaitForViewNamedAsync("Melee Dummy");
        var far = await actor.WaitForViewNamedAsync("Far Dummy");
        uint nearHandle = DuelPackets.Handle(near.ViewId, near.SpawnId);
        uint farHandle = DuelPackets.Handle(far.ViewId, far.SpawnId);

        // Step 1: Unweakened cast at rest -> 0 damage on near, far excluded.
        int rpcAt = actor.World.RpcSnapshot().Count;
        int logAt = server.DebugLog().Length;
        actor.SendRpc(Use(EmpBlast, 0));
        await EvidenceRetestWire.CastBarAsync(actor, EmpBlast, 1);
        await EvidenceRetestWire.TimerAsync(actor, rpcAt, EmpBlast, AbilityCooldowns.OwnTimer, 30);
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains($"[EMP] {near.ViewId} damage=0 hp=30000", StringComparison.Ordinal),
            "Unweakened near dummy takes 0 damage", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog()[logAt..].Contains($"[EMP] {far.ViewId} ", StringComparison.Ordinal),
            "Far dummy at 12 m is excluded by 10 m radius");

        // Grant 1026 weakness + 6000 HP to BOTH near and far dummies.
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(nearHandle))}");
        await Task.Delay(150);
        actor.SendChat("?cd empweakness on 6000");
        await actor.WaitForTextAsync("EMP fixture weakness=1; HP=6000.");

        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(farHandle))}");
        await Task.Delay(150);
        actor.SendChat("?cd empweakness on 6000");
        await actor.WaitForTextAsync("EMP fixture weakness=1; HP=6000.");

        // Wait for 30 s reuse before Step 2.
        byte[] ownEnd = AbilityCooldowns.BuildTimer(EmpBlast, AbilityCooldowns.OwnTimer, 30, false);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(rpcAt).Any(r => r.Bytes.AsSpan().SequenceEqual(ownEnd)),
            "EMCP Blast 30 s reuse timer expires", TimeSpan.FromSeconds(36));

        // Step 2: Weakened cast -> 5000 damage on near (6000->1000), far (12 m) untouched!
        rpcAt = actor.World.RpcSnapshot().Count;
        logAt = server.DebugLog().Length;
        actor.SendRpc(Use(EmpBlast, 0));
        await EvidenceRetestWire.CastBarAsync(actor, EmpBlast, 1);
        await EvidenceRetestWire.TimerAsync(actor, rpcAt, EmpBlast, AbilityCooldowns.OwnTimer, 30);
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains($"[EMP] {near.ViewId} damage=5000 hp=1000", StringComparison.Ordinal),
            "Weakened near dummy takes 5000 damage (6000 -> 1000)", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog()[logAt..].Contains($"[EMP] {far.ViewId} ", StringComparison.Ordinal),
            "Weakened far dummy at 12 m still excluded by 10 m radius");

        // Wait for 30 s reuse so we can test MustBeOutsideCombat (22) cleanly, then kill near dummy!
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(rpcAt).Any(r => r.Bytes.AsSpan().SequenceEqual(ownEnd)),
            "Second EMCP Blast 30 s reuse timer expires", TimeSpan.FromSeconds(36));

        // Step 3: In-combat refusal (22) + ?cd stop + killing cast (1000->0).
        actor.SendRpc($"40 {WireFormat.ToHex(BitConverter.GetBytes(nearHandle))} 01 6b");
        await actor.WaitForRpcAsync("47");
        await StarterHackerControlTargetMode.RefusedAsync(actor, EmpBlast, Use(EmpBlast, 0), AbilityRefusals.MustBeOutsideCombat);
        actor.SendChat("?cd stop");
        await actor.WaitForTextAsync("Fixture combat ended.");

        rpcAt = actor.World.RpcSnapshot().Count;
        logAt = server.DebugLog().Length;
        actor.SendRpc(Use(EmpBlast, 0));
        await EvidenceRetestWire.CastBarAsync(actor, EmpBlast, 1);
        await EvidenceRetestWire.TimerAsync(actor, rpcAt, EmpBlast, AbilityCooldowns.OwnTimer, 30);
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains($"[EMP] {near.ViewId} damage=5000 hp=0", StringComparison.Ordinal),
            "Second weakened EMCP Blast kills near dummy (1000 -> 0)", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog()[logAt..].Contains($"[EMP] {far.ViewId} ", StringComparison.Ordinal),
            "Far dummy at 12 m remains unaffected");

        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "EMCP Blast framing");
        Console.WriteLine("EMCP BLAST 1025 PASS: unweakened 0, weakened 5000 (6000->1000->0), 12 m radius exclusion, and MustBeOutsideCombat(22) verified.");
        return Array.Empty<byte>();
    }

    private static byte[] Use(ushort ability, uint target) =>
        new AbilityUseRecord(ability, target, 0, 0, 0, ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();
}
