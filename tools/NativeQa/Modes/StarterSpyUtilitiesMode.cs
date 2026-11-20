using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterSpyUtilitiesMode : NativeQaMode
{
    private const ushort DisarmTraps = UtilityAbilities.DisarmTraps;                   // 119
    private const ushort OpenLocks = UtilityAbilities.OpenLocks;                       // 142
    private const ushort DisableSecurityDevices = UtilityAbilities.DisableSecurityDevices; // 218

    private const uint LockPickGoid = 4;
    private const uint DisableDeviceToolGoid = 616;
    private const uint TrapDetectToolGoid = 2500;
    private const ushort FilingCabinetGoid = 326;
    private const ushort SecurityDeskGoid = 377;

    private const byte Level = 50, Rank = 50;
    private const int GroundY = 495, CasterX = 24800, CasterZ = -1498;

    private static readonly ushort[] Roots = { 2, 17, DisarmTraps, OpenLocks, DisableSecurityDevices };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-starter-spy-utilities";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CasterX, CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={GroundY}," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot IN (1,2,3,{CombatFixtureWeapons.HandSlot})",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{LockPickGoid},1,1,0),(110,{DisableDeviceToolGoid},2,1,0),(110,{TrapDetectToolGoid},3,1,0)"
        },
        DevCommands: true,
        loadAbilityAncestors: false,
        hotbarAbilities: new[] { DisarmTraps },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (42/100 memory, max IS 1000). Bag tools: slot 1 = Lock_Pick (GOID 4), slot 2 = DisableDeviceTool (GOID 616), slot 3 = TrapDetectToolBasic (GOID 2500). Page 2 hotbar: slot 1 = 119 Disarm Traps.",
            "Step A (142 Open Locks): Send '?objstate 326 0x836' to spawn a locked Filing Cabinet at your feet. Click it and choose Pick Lock (use 8): expect 'Lock Picked Successfully!' (0x7a000145), '[UTILITY] Pick Lock on 326: Success', and state transition 0x836 -> 0x841 (UnlockedState, offering Open).",
            "Step B (218 Disable Security Devices): Send '?objstate 377 0x839' to spawn an armed Security_Desk at your feet. Click it and choose Use / Disable Security Devices (use 1): expect '[UTILITY] Disable Security Devices on 377: result 1 (ACCESS GRANTED)' (state 0x83a) or 'result 2 (ALARM TRIGGERED)' (state 0x83b) plus the matching system chat line.",
            "Step C (119 Disarm Traps): Send '?trapobj 326' to spawn a trapped, detected Filing Cabinet (TrappedState 0x839) at your feet. Select the new Filing Cabinet and press page 2 slot 1 (119 Disarm Traps): expect 10 IS spent (1000 -> 990), 2.0 s cast bar, and '[UTILITY] Disarm Traps on 326:' Disarmed (0x7600024e -> 0x83a), Failed (0x7600024f, retryable), or Critical (0x76000250 -> 0x83b)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "119,142,218",
            ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var actor = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            $"Legal level-{Level} memory ({Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)})");

        // Step A: ?objstate 326 0x836 -> Pick Lock (use 8) -> Success (0x841).
        int c1 = actor.World.CreationSnapshot().Count;
        int logA = server.DebugLog().Length;
        actor.SendChat($"?objstate {FilingCabinetGoid} 0x836");
        var cabinet = await WaitNewCreationAsync(c1, "Locked Filing Cabinet spawns");
        actor.SendRpc(UseObject(cabinet.ViewId, cabinet.SpawnId, 8));
        await actor.WaitUntilAsync(() =>
            server.DebugLog()[logA..].Contains($"[UTILITY] Pick Lock on {FilingCabinetGoid}: Success", StringComparison.Ordinal),
            "Open Locks 142 succeeds on locked Filing Cabinet", TimeSpan.FromSeconds(5));

        // Step B: ?objstate 377 0x839 -> Disable Security Devices (use 1) -> ACCESS GRANTED or ALARM TRIGGERED.
        int c2 = actor.World.CreationSnapshot().Count;
        int logB = server.DebugLog().Length;
        actor.SendChat($"?objstate {SecurityDeskGoid} 0x839");
        var desk = await WaitNewCreationAsync(c2, "Armed Security_Desk spawns");
        actor.SendRpc(UseObject(desk.ViewId, desk.SpawnId, 1));
        await actor.WaitUntilAsync(() =>
            server.DebugLog()[logB..].Contains($"[UTILITY] Disable Security Devices on {SecurityDeskGoid}: result ", StringComparison.Ordinal),
            "Disable Security Devices 218 resolves on Security_Desk", TimeSpan.FromSeconds(5));

        // Step C: ?trapobj 326 -> cast 119 Disarm Traps (2 s cast, 10 IS).
        int c3 = actor.World.CreationSnapshot().Count;
        int logC = server.DebugLog().Length;
        actor.SendChat($"?trapobj {FilingCabinetGoid}");
        var trapped = await WaitNewCreationAsync(c3, "Trapped Filing Cabinet spawns");
        actor.SendRpc(new AbilityUseRecord(DisarmTraps, DuelPackets.Handle(trapped.ViewId, trapped.SpawnId),
            0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        await actor.WaitUntilAsync(() =>
            server.DebugLog()[logC..].Contains($"[UTILITY] Disarm Traps on {FilingCabinetGoid}:", StringComparison.Ordinal),
            "Disarm Traps 119 resolves on trapped Filing Cabinet", TimeSpan.FromSeconds(8));

        Console.WriteLine("  Starter Spy Utilities (142 Open Locks, 218 Disable Security Devices, 119 Disarm Traps) verified");
        return Array.Empty<byte>();

        async Task<ViewCreation> WaitNewCreationAsync(int fromIndex, string description)
        {
            await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Count > fromIndex,
                description, TimeSpan.FromSeconds(5));
            return actor.World.CreationSnapshot()[fromIndex];
        }

        static byte[] UseObject(ushort viewId, ushort spawnId, byte use)
        {
            byte[] rpc = new byte[7];
            rpc[0] = 0x80; rpc[1] = 0xc7;
            BitConverter.GetBytes(viewId).CopyTo(rpc, 2);
            BitConverter.GetBytes(spawnId).CopyTo(rpc, 4);
            rpc[6] = use;
            return rpc;
        }
    }
}
