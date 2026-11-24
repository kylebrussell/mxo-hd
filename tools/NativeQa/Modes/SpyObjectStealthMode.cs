using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class SpyObjectStealthMode : NativeQaMode
{
    private const ushort OpenLocks = UtilityAbilities.OpenLocks;                       // 142
    private const ushort DisableSecurityDevices = UtilityAbilities.DisableSecurityDevices; // 218

    private const uint LockPickGoid = 4;
    private const uint DisableDeviceToolGoid = 616;
    private const ushort FilingCabinetGoid = 326;
    private const ushort SecurityDeskGoid = 377;

    private const byte Level = 50, Rank = 50;
    private const int GroundY = 495, CasterX = 17000, CasterZ = 2300;
    private static readonly Pose LockUse = new(CasterX, CasterZ, 0), LockProp = new(CasterX, 2420, 0);
    private static readonly Pose DeskUse = new(17360, CasterZ, 64), DeskProp = new(17500, CasterZ, 64);
    private sealed record Pose(int X, int Z, byte Yaw)
    {
        internal string Command => InvisibilityBeltWatcherMode.Warp(X, GroundY, Z, Yaw, inside: false);
    }

    private static readonly ushort[] Roots = { 2, 17, 151, OpenLocks, DisableSecurityDevices };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-spy-object-stealth";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CasterX, CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={GroundY}," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot IN (1,2,{CombatFixtureWeapons.HandSlot})",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{LockPickGoid},1,1,0),(110,{DisableDeviceToolGoid},2,1,0)"
        },
        DevCommands: true,
        loadAbilityAncestors: false,
        hotbarAbilities: new ushort[] { 17, 1111 },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0"
        },
        readyMessages: new[]
        {
            "Bag slots 1/2 hold the stock tools. Page 2 slot 2 is Sneak. Use the printed prop and approach stands; all teleports are before Sneak. Enter each printed command in chunks with one final Enter.",
            "Warp to LOCK PROP. Type '?objstate 326' then ' 0x836' and press Enter. Warp to LOCK USE, 1.20 m from the cabinet facing it. Stay on the stand, press Sneak slot 2, then radial Pick Lock. Expect OBJECT-USE, 1111 off (picked a lock), Success and UnlockedState.",
            "Warp to DESK PROP. Type '?objstate 377' then ' 0x839' and press Enter. Warp to DESK USE, 1.40 m from the desk facing it. Stay there, press Sneak slot 2, then radial Use. Expect OBJECT-USE, 1111 off (used a security device), resolved contest and matching state.",
            "No strafe or auto-approach is needed. If the stock client keeps the request pending even at the close stand, record the coordinates and whether stock sent 80 c7. Keep that separate from the server transition check."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "142,218",
            ["NativeCloseableRows"] = 2,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        PrintPair("LOCK", LockProp, LockUse);
        PrintPair("DESK", DeskProp, DeskUse);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await CheckRule(OpenLocks, FilingCabinetGoid, "0x836", 8, "[UTILITY] Pick Lock", "picked a lock", LockProp, LockUse);
        await CheckRule(DisableSecurityDevices, SecurityDeskGoid, "0x839", 1,
            "[UTILITY] Disable Security Devices", "used a security device", DeskProp, DeskUse);
        Console.WriteLine("SPY UTILITY SNEAK PASS: both ordinary rules preserve Sneak on refusal and end it on admission.");
        return Array.Empty<byte>();

        async Task CheckRule(ushort ability, ushort goid, string state, byte use, string marker, string reason, Pose placement, Pose useStand)
        {
            actor.SkipReceived();
            actor.SendChat($"?qaunload {ability}");
            await actor.WaitForRpcAsync($"80 b3 {EvidenceRetestWire.Le(ability)}");
            await Move(placement);
            int cursor = actor.World.CreationSnapshot().Count;
            actor.SendChat($"?objstate {goid} {state}");
            var prop = await WaitNewCreationAsync(cursor, $"Object {goid} ready");
            await Move(useStand);
            int enabledAt = actor.World.RpcSnapshot().Count;
            actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
            byte[] on = AbilityBuffs.BuildExclusive(1111, true), off = AbilityBuffs.BuildExclusive(1111, false);
            await StockAbilityProfile.ObserveAsync(actor, enabledAt, f => f.Bytes.AsSpan().SequenceEqual(on), "Stock Sneak started");
            int refusedAt = server.DebugLog().Length;
            actor.SendRpc(UseObject(prop.ViewId, prop.SpawnId, use));
            await actor.WaitUntilAsync(() => server.DebugLog()[refusedAt..].Contains(marker, StringComparison.Ordinal),
                "Unloaded rule traced", HeadlessPlayer.DefaultTimeout);
            Check.That(!actor.World.RpcSnapshot().Skip(enabledAt).Any(f => f.Bytes.AsSpan().SequenceEqual(off)),
                "The unloaded refusal preserves active Sneak");

            actor.SkipReceived();
            actor.SendChat($"?qaload {ability}");
            await actor.WaitForRpcAsync($"80 b2 {EvidenceRetestWire.Le(ability)}");
            int from = actor.World.RpcSnapshot().Count, logAt = server.DebugLog().Length;
            actor.SendRpc(UseObject(prop.ViewId, prop.SpawnId, use));
            await StockAbilityProfile.ObserveAsync(actor, from, f => f.Bytes.AsSpan().SequenceEqual(off),
                "Admitted object use ends Sneak");
            Check.That(server.DebugLog()[logAt..].Contains($"1111 off ({reason})", StringComparison.Ordinal),
                "Object rule supplies the expected exclusive break reason");
            if (ability == OpenLocks && AbilityCooldowns.TryGet(1111, out var cooldown))
                await Task.Delay(TimeSpan.FromSeconds(cooldown.Seconds + .25));
        }

        async Task Move(Pose pose)
        {
            actor.SkipReceived();
            actor.SendChat(pose.Command);
            await actor.WaitForTextAsync("Teleported to");
        }

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
    private static void PrintPair(string name, Pose prop, Pose stand)
    {
        Check.That(NavMesh.HasSightPath(1, prop.X, GroundY, prop.Z, stand.X, GroundY, stand.Z),
            $"{name} has clear NAV sight at its within-use stand");
        Print($"{name} PROP", prop);
        Print($"{name} USE", stand);

        static void Print(string label, Pose pose)
        {
            string[] words = pose.Command.Split(' ');
            Console.WriteLine($"READY {label} type '{string.Join(' ', words[..3])}', then ' {string.Join(' ', words[3..7])}', then ' {words[7]} {words[8]}'; press Enter once.");
        }
    }
}
