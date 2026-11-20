using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class DesperateSpeedMode : NativeQaMode
{
    private const ushort DesperateSpeed = 291;
    private const byte Level = 25, Rank = 25;
    private static readonly ushort[] Roots = { 2, 17, DesperateSpeed };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-desperate-speed";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Level 25, rank 25, unarmed, outside interlock. Activate Desperate Speed (291) from Actions > Shadow (self buff, no target needed).",
            "Expect instant cast, 50 IS spent (1000 -> 950), 298.0 s own reuse (80 bd 01 23 01 00 298.0f), '[BUFF] 291 on for 15 s', 3 stat adds (Stat 115 MovementSpeed +40, Stat 877 SneakMovement +20, Stat 878 InvisMovement +20 for 15 s), own-view MovementScale 0.40f (02 00 02 80 88 cd cc cc 3e 80 80 80 02 00 00) visibly speeding run speed, and after 15 s '[BUFF] 291 expired' with 3 stat removals and MovementScale reset to 0.0f."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "291", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-25 memory");

        int castStart = server.DebugLog().Length;
        int viewCursor = caster.World.ViewSnapshot().Count;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(DesperateSpeed, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());

        await caster.WaitUntilAsync(() => server.DebugLog()[castStart..].Contains("[BUFF] 291 on for 15 s", StringComparison.Ordinal),
            "Desperate Speed activates for 15 s", TimeSpan.FromSeconds(5));

        byte[] speedOn = AbilityBuffs.BuildMovementUpdate(0.40f, 0);
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(viewCursor).Any(v => v.AsSpan().IndexOf(speedOn) >= 0),
            "Desperate Speed publishes MovementScale 0.40f on activation", TimeSpan.FromSeconds(5));

        byte[] ownTimer = AbilityCooldowns.BuildTimer(DesperateSpeed, AbilityCooldowns.OwnTimer, 298, true);
        Check.That(caster.World.RpcSnapshot().Skip(rpcCursor).Any(r => r.Bytes.AsSpan().SequenceEqual(ownTimer)),
            "Desperate Speed starts 298 s own reuse");

        byte[] speedOff = AbilityBuffs.BuildMovementUpdate(0f, 0);
        int expiryCursor = caster.World.ViewSnapshot().Count;
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(expiryCursor).Any(v => v.AsSpan().IndexOf(speedOff) >= 0),
            "Desperate Speed resets MovementScale to 0.0f after 15 s", TimeSpan.FromSeconds(20));

        Console.WriteLine("  Desperate Speed (291) published MovementScale 0.40f for 15 s with 298 s reuse and cleared cleanly on expiry");
        return Array.Empty<byte>();
    }
}
