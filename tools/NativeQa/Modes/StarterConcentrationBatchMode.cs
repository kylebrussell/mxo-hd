using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterConcentrationBatchMode : NativeQaMode
{
    private const ushort Concentration = 628, Insidious = 360, Tinkering = 36, Hacker = 1006, Coder = 1008;
    private const string HackerCast = "80b9ee030000000000000000000000000000000000000000000000000000000090c97941086b0000000000";
    private const string CoderCast = "80b9f003000000000000000000000000000000000000000000000000585cc70001000000086b0000000000";
    private const string TinkeringCast = "80b924000400020000000000000000000000000000000000000000007802c50001000000086b0000000000";
    // Shipped Vector stock PacketLog line 30301. Only the live selected handle changes.
    private const string InsidiousCast = "80b96801130027000000000000000000000000000000000000000000000000204762ad41076b0000000000";
    private const string ArmDamage = "?combatdepth hurtme 100 next";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Concentration, Insidious, Tinkering, Hacker, Coder)
        .Order().ToArray();

    public override string Name => "ability-parity-starter-awakened-concentration";
    public override NativeQaModePlan Plan { get; } = new(Loadout, PvpOpponentsFixture.X, PvpOpponentsFixture.Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Hacker, Insidious, Coder, Tinkering },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_CAST_INTERRUPT_QA_ROLL"] = "85",
            ["MXO_CONCENTRATION_QA_DAMAGE"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "628,360,36", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay out of interlock. Use Hacker Style (page 2 slot 1) and select QAEnemy. Submit ?qaunload 628 and ?combatdepth hurtme 100 next with Enter. Keep selection without Escape. Use Insidious Code (slot 2). The controlled 100 HP loss interrupts the four-second cast at roll 85, with no completed effect or reuse.",
            "Reload Concentration with ?qaload 628. Re-arm the same 100 HP loss and cast the same slot on QAEnemy. The passive raises Concentration from 5 to 35 in this profile. The cast completes under the same roll 85 and starts 58 s reuse. Capture the target modifier and cast-bar outcome.",
            "After the cast resolves, choose Coder Style (slot 3). Re-arm and use Tinkering (slot 4) on the same QAEnemy. Its six-second cast completes normally and the arm does not fire. Tinkering has no explicit interruptible flag. Finish the session after this control."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var target = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(target.Handle, target);
        await PvpOpponentsFixture.EnterAsync(target);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, target);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer!;
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The profile fits stock memory");
        await PvpOpponentsFixture.EnterAsync(caster);
        var view = await caster.WaitForViewNamedAsync(target.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        caster.SendRpc(HackerCast);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        int prior = caster.World.RpcSnapshot().Count;
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { Concentration }, load: false);
        await StockAbilityProfile.ObserveAsync(caster, prior,
            frame => StockAbilityProfile.Bonus(frame, Concentration, 564, 30, 0, true), "Unloading removes +30 Concentration");
        int targetAt = target.World.RpcSnapshot().Count;
        int first = caster.World.RpcSnapshot().Count;
        await ArmAsync(caster);
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(InsidiousCast, handle));
        await EvidenceRetestWire.CastBarAsync(caster, Insidious, 4);
        await StockAbilityProfile.ObserveAsync(caster, first, frame => BytePattern.Parse("80 ad 68 01 00 00 02").IsMatch(frame.Bytes),
            "Damage interruption reason");
        Check.That(server.DebugLog().Contains("loss=100 concentration=5 through=77 roll=85"), "Baseline uses the fixed interrupt roll");

        prior = caster.World.RpcSnapshot().Count;
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { Concentration }, load: true);
        await StockAbilityProfile.ObserveAsync(caster, prior,
            frame => StockAbilityProfile.Bonus(frame, Concentration, 564, 30, 0, false), "Reload restores +30 Concentration");
        int second = caster.World.RpcSnapshot().Count;
        await ArmAsync(caster);
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(InsidiousCast, handle));
        await EvidenceRetestWire.CastBarAsync(caster, Insidious, 4);
        await EvidenceRetestWire.TimerAsync(caster, second, Insidious, AbilityCooldowns.OwnTimer, 58);
        var hit = BytePattern.Parse("80 bc ?? ?? ?? ?? 00 68 01 00 00 9d 00");
        await StockAbilityProfile.ObserveAsync(target, targetAt, frame => hit.IsMatch(frame.Bytes), "Insidious lands after the second damage roll");
        var ownReuse = BytePattern.Parse("80 bd 01 68 01");
        Check.That(!caster.World.RpcSnapshot().Skip(first).Take(second - first).Any(frame => ownReuse.IsMatch(frame.Bytes)),
            "The interrupted cast never starts completion reuse");
        Check.That(server.DebugLog().Contains("loss=100 concentration=35 through=90 roll=85"), "The loaded cast survives the same roll");

        caster.SendRpc(CoderCast);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        int controlAt = server.DebugLog().Length;
        await ArmAsync(caster);
        int controlCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(TinkeringCast, handle));
        await EvidenceRetestWire.CastBarAsync(caster, Tinkering, 6);
        await EvidenceRetestWire.TimerAsync(caster, controlCursor, Tinkering, AbilityCooldowns.OwnTimer, 30);
        Check.That(!server.DebugLog()[controlAt..].Contains("[CAST-INTERRUPT-QA] real self damage", StringComparison.Ordinal),
            "A template without the explicit interruptible flag does not consume the damage arm");
        Check.Equal(0, caster.World.Problems.Count + target.World.Problems.Count, "Concentration replay framing");
        return Array.Empty<byte>();
    }

    private static async Task ArmAsync(HeadlessPlayer caster)
    {
        caster.SkipReceived();
        caster.SendChat(ArmDamage);
        await caster.WaitForTextAsync("Concentration damage armed: 100");
    }
}
