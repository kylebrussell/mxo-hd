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
            "Stay at verified HOME. No teleport. Bag slots 1 and 2 carry the ordinary tools. Page 2 slot 2 is Sneak. Spawn each object first, strafe once to see it, then use Sneak. No helper or forced contest.",
            "Type '?objstate 326' then ' 0x836' and press Enter once. Strafe off the cabinet, Sneak slot 2, then radial Pick Lock. Expect [STEALTH] 1111 off (picked a lock), Pick Lock Success and UnlockedState.",
            "Type '?objstate 377' then ' 0x839' and press Enter once. Strafe off the desk, Sneak slot 2 after its reuse, then radial Use / Disable Security Devices. Expect [STEALTH] 1111 off (used a security device), resolved contest and matching object state.",
            "Headless also verifies the same interactions after the relevant ability is unloaded: both actions leave Sneak active. Earlier native tool and rule coverage carries."
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
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(CasterX, GroundY, CasterZ, 0, inside: false)}");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await CheckRule(OpenLocks, FilingCabinetGoid, "0x836", 8, "[UTILITY] Pick Lock", "picked a lock");
        await CheckRule(DisableSecurityDevices, SecurityDeskGoid, "0x839", 1,
            "[UTILITY] Disable Security Devices", "used a security device");
        Console.WriteLine("SPY UTILITY SNEAK PASS: both ordinary rules preserve Sneak on refusal and end it on admission.");
        return Array.Empty<byte>();

        async Task CheckRule(ushort ability, ushort goid, string state, byte use, string marker, string reason)
        {
            actor.SkipReceived();
            actor.SendChat($"?qaunload {ability}");
            await actor.WaitForRpcAsync($"80 b3 {EvidenceRetestWire.Le(ability)}");
            int cursor = actor.World.CreationSnapshot().Count;
            actor.SendChat($"?objstate {goid} {state}");
            var prop = await WaitNewCreationAsync(cursor, $"Object {goid} ready");
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
