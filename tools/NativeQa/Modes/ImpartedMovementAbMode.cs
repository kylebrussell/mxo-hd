using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class ImpartedMovementAbMode : NativeQaMode
{
    private const ushort Imparted = 629, Invisibility = 175;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400, LaneEnd = 8300, FriendX = 17400;
    private const string Home = "?tp 17000 495 6400 4e2 12 4 - 00";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Imparted, Invisibility,
        CombatStyles.SpyStyle).Order().ToArray();

    public override string Name => "ability-parity-imparted-movement-ab";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},alignment=1,pvpflag=0 WHERE charId=6111"
        }, DevCommands: true,
        hotbarAbilities: new[] { CombatStyles.SpyStyle, Imparted },
        testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [MovementSampleQa.Mode] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "175", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        },
        readyMessages: new[]
        {
            $"Page 2 Spy and 629. No target or RNG. Start on verified outdoor NAV ({X},{Y},{Z}) facing +Z; QAFriend ({FriendX},{Y},{Z}) watches from the east. Reset to {Home} between samples. The clear lane is verified through z={LaneEnd}.",
            $"Select Spy. With no state active, hold forward about 2 s and release before z={LaneEnd}. Return HOME. Run A: clear selection, cast 629 and wait for [STEALTH] 629 on (bits 4, 120 s). Save the stock request and hold forward for the same interval. Use the run setting and forward key consistently. Take at least two moving samples per condition, omitting acceleration and release.",
            "Wait out the 30 s reuse and press 629 again. Confirm 629 off, then use ?qaunload 175. Keep Spy and 629 loaded. Return HOME, recast 629 after its reuse, and repeat the samples for B. End 629 again; ?qaload 175, return HOME and repeat A. Expect the same 629 lease and view retention across A/B/A. No 115/881 record from 629 and no nonzero server MovementScale should appear.",
            "Save positions and monotonic ms from [MOVE-SAMPLE], and outgoing client gait bytes for each segment. Compare (last z - first z) / (last ms - first ms) using the same observed gait. Report a repeatable change only if both A runs agree and B differs. Matching speeds leave the original 1051-to-878 coefficient open."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var lane = Enumerable.Range(0, (LaneEnd - Z) / 100 + 1).Select(i => (X, Z + 100 * i));
        foreach (var (x, z) in lane.Append((FriendX, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && stand.floorY + 95 == Y &&
                !stand.indoors && NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear movement NAV ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) &&
            (home.sector, home.region, home.polygon) == (0x4e2, 0x12, 4), "HOME matches NAV");

        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await friend.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        foreach (bool unload in new[] { true, false })
        {
            int cursor = actor.World.RpcSnapshot().Count;
            var ack = BytePattern.Parse($"80 {(unload ? "b3" : "b2")} af 00");
            actor.SendChat(unload ? "?qaunload 175" : "?qaload 175");
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(rpc => ack.IsMatch(rpc.Bytes)),
                $"Ordinary 175 loadout acknowledgement (unload={unload})", HeadlessPlayer.DefaultTimeout);
        }
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Stock framing");
        return Array.Empty<byte>();
    }
}
