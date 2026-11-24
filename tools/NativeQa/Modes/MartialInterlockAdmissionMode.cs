using System.Buffers.Binary;
using System.Diagnostics;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public abstract class MartialInterlockAdmissionMode : NativeQaMode
{
    private const int X = 24751, Z = -1498;
    private const ushort SwirlingKiSummon = 268;
    private sealed record Sample(ushort Id, uint RequiredState, string Request);
    // Full stock requests from PR551/555/557/558/559/561. Replay substitutes the receiver-local target handle.
    private static readonly Sample[] Captures =
    {
        new(138, 4, "80b98a000600040000000000000000000000000000000000000000008a000000006b0000086b0000000000"),
        new(139, 0, "80b98b000400020000000000000000000000000000000000000000008b000000006b0000086b0000000000"),
        new(259, 2, "80b9030104000200000000000000000000000000000000000000000003010000006b0000086b0000000000"),
        new(260, 0, "80b9040104000200000000000000000000000000000000000000000004010000006b0000086b0000000000"),
        new(261, 0, "80b9050104000200000000000000000000000000000000000000000005010000006b0000086b0000000000"),
        new(262, 8, "80b9060104000200000000000000000000000000000000000000000006010000006b0000086b0000000000"),
        new(265, 8, "80b9090104000200000000000000000000000000000000000000000009010000006b0000086b0000000000"),
        new(266, 0, "80b90a010400020000000000000000000000000000000000000000000a010000006b0000086b0000000000"),
        new(267, 0, "80b90b010400020000000000000000000000000000000000000000000b010000006b0000086b0000000000"),
        new(268, 0, "80b90c010400020000000000000000000000000000000000000000000c010000006b0000086b0000000000"),
        new(273, 4, "80b9110104000200000000000000000000000000000000000000000011010000006b0000086b0000000000"),
        new(274, 0, "80b9120104000200000000000000000000000000000000000000000012010000006b0000086b0000000000"),
        new(296, 4, "80b9280104000200000000000000000000000000000000000000000028010000006b0000086b0000000000"),
        new(427, 4, "80b9ab01040002000000000000000000000000000000000000000000ab010000006b0000086b0000000000"),
        new(428, 0, "80b9ac01040002000000000000000000000000000000000000000000ac010000006b0000086b0000000000"),
        new(429, 0, "80b9ad01040002000000000000000000000000000000000000000000ad010000006b0000086b0000000000"),
        new(430, 2, "80b9ae01040002000000000000000000000000000000000000000000ae010000006b0000086b0000000000"),
        new(532, 2, "80b9140204000200000000000000000000000000000000000000000014020000006b0000086b0000000000"),
        new(534, 0, "80b9160204000200000000000000000000000000000000000000000016020000006b0000086b0000000000"),
        new(536, 8, "80b9180204000200000000000000000000000000000000000000000018020000006b0000086b0000000000"),
    };

    internal static string StockRequest(ushort id) => Captures.Single(sample => sample.Id == id).Request;

    private readonly ushort style;
    private readonly Sample[] samples;
    public override string Name { get; }
    public override NativeQaModePlan Plan { get; }

    protected MartialInterlockAdmissionMode(string family, ushort style, params ushort[] ids)
    {
        this.style = style;
        samples = ids.Select(id => Captures.Single(sample => sample.Id == id)).ToArray();
        Name = "ability-parity-ma-admission-" + family;
        var loadout = StockAbilityProfile.Profile(new ushort[] { 2, 17, style }.Concat(ids).ToArray()).Order().ToArray();
        Plan = new(loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y=495,z={Z},alignment=1 WHERE charId=110",
            $"UPDATE characters SET x={X + 450},y=495,z={Z},alignment=2 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, hotbarAbilities: new[] { style }.Concat(ids).ToArray(),
            testMobFixture: "awakened-interlock-resistance.csv",
            environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
                [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1",
                ["MXO_CONDITIONAL_THROW_QA"] = "1", ["MXO_CONDITIONAL_POWERLESS_QA"] = "1"
            }, extraQaMetadata: new()
            {
                ["ScopedStockRows"] = string.Join(",", ids), ["NativeCloseableRows"] = 0
            }, readyMessages: new[] { "Run this proof with --bootstrap-check. The fixture compares actual gameplay admission and preserves ordinary combat rolls." });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var bystander = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(bystander.Handle, bystander);
        await PvpOpponentsFixture.EnterAsync(bystander);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        Check.That(Plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The ability family fits a level 50 loadout");
        var other = await caster.WaitForViewNamedAsync(bystander.Handle);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        uint opponent = Handle(dummy);
        foreach (var sample in samples)
        {
            caster.SendChat("?combatdepth fixture-reset");
            await caster.WaitForTextAsync($"Fixture ready: Melee Dummy view {dummy.ViewId}, spawn {dummy.SpawnId}");
            if (sample.Id != SwirlingKiSummon)
                await RefuseAsync(sample, opponent);
            await StarterInterlockDamageMode.Open(caster, dummy);
            await StarterInterlockDamageMode.Select(caster, dummy, style);
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(opponent))}");

            if (sample.Id == SwirlingKiSummon)
            {
                // The 2 s caster-area special can resolve in interlock without an opponent handle.
                int from = server.DebugLog().Length;
                caster.SendRpc(Use(sample, 0));
                await EvidenceRetestWire.CastBarAsync(caster, sample.Id, 2);
                await caster.WaitUntilAsync(() => server.DebugLog()[from..].Contains("[EFFECTS] 268 on Melee Dummy: Stat 524 -60 12 s, Stat 522 -60 12 s",
                    StringComparison.Ordinal), "Swirling Ki Summon uses the caster's area", HeadlessPlayer.DefaultTimeout);
                Check.That(!server.DebugLog()[from..].Contains("[EFFECTS] 268 on QAEnemy", StringComparison.Ordinal),
                    "Swirling Ki Summon excludes the live bystander outside the caster area");
                continue;
            }

            await RefuseAsync(sample, 0);
            await RefuseAsync(sample, opponent ^ 0x10000);
            await RefuseAsync(sample, Handle(other));
            if (sample.RequiredState != 0)
            {
                var refusal = await RefuseAsync(sample, opponent, AbilityRefusals.TargetStateRequired);
                Check.That(AbilityRefusals.TryDecode(refusal.Bytes, out var state) && state.StatesNotMet == sample.RequiredState,
                    $"{sample.Id} refuses with its shipped tactic state");
                (string command, string response) = sample.RequiredState switch
                {
                    2 => ("daze", "Daze"), 4 => ("offbalance", "OffBalance"), 8 => ("stagger", "Stagger"),
                    _ => throw new InvalidOperationException("Unknown required state in the capture matrix.")
                };
                caster.SendChat($"?combatdepth {command} 60");
                await caster.WaitForTextAsync($"{response} fixture applied");
            }

            int before = caster.World.RpcSnapshot().Count;
            long queuedAt = Stopwatch.GetTimestamp();
            var request = Use(sample, opponent);
            Check.Equal(43, request.Length, "Ordinary stock ability request length");
            caster.SendRpc(request);
            var round = await StockAbilityProfile.ObserveAsync(caster, before,
                frame => frame.Bytes.Length == 89 && frame.Bytes[0] == 0x45 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(frame.Bytes.AsSpan(13)) == (0x80000000u | sample.Id),
                $"Normal combat round resolves the admitted ability {sample.Id}");
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(round.Bytes.AsSpan(61));
            Console.WriteLine($"  MA admission {sample.Id}: valid round flags 0x{flags:x}; invalid handles were free");
            TimeSpan remaining = TimeSpan.FromSeconds(5.3) - Stopwatch.GetElapsedTime(queuedAt);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
        }
        Check.Equal(0, caster.World.Problems.Count + bystander.World.Problems.Count, "Martial Arts matrix framing");
        return Array.Empty<byte>();

        Task<RpcRecord> RefuseAsync(Sample sample, uint handle, uint reason = AbilityRefusals.InvalidTarget) =>
            StarterHackerControlTargetMode.RefusedAsync(caster, sample.Id, Use(sample, handle), reason);
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);

    private static byte[] Use(Sample sample, uint handle) => StarterHackerControlTargetMode.WithHandle(sample.Request, handle);

    internal static byte[] ControlRequest(ushort id, uint handle) => Use(Captures.Single(sample => sample.Id == id), handle);
}

public sealed class MartialKungFuAdmissionMode() : MartialInterlockAdmissionMode("kung-fu", CombatStyles.KungFu,
    138, 259, 260, 261, 262, 427, 428, 429, 430);

public sealed class MartialKarateAdmissionMode() : MartialInterlockAdmissionMode("karate", CombatStyles.Karate,
    265, 266, 267, 532, 534, 536, 268);

public sealed class MartialAikidoAdmissionMode() : MartialInterlockAdmissionMode("aikido", CombatStyles.Aikido,
    139, 273, 274, 296);
