using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterControlDeflectMode : NativeQaMode
{
    private const ushort Deflect = 23, Style = 1006, Blind = 43, Pacify = 48, Stun = 315;
    private static readonly ushort[] CasterAbilities = StockAbilityProfile.Profile(2, 17, Style, Blind, Pacify, Stun).Order().ToArray();
    private static readonly ushort[] DefenderAbilities = DeflectCodeFixture.DefenderAbilities;

    private sealed record Sample(ushort Ability, ushort State, float Duration, int Immunity, string Capture);
    private static readonly Sample[] Samples =
    {
        new(Blind, TempMods.BlindAbility, 14.4f, 0, EarlyHackerControlRecoveryMode.BlindRequest),
        new(Pacify, TempMods.MezAbility, 5.4f, 12, EarlyHackerControlRecoveryMode.PacifyRequest),
        new(Stun, TempMods.StunAbility, 6.615f, 14, EarlyHackerControlRecoveryMode.StunRequest)
    };

    public override string Name => "ability-parity-starter-deflect-controls";
    public override NativeQaModePlan Plan { get; } = new(DefenderAbilities,
        PvpOpponentsFixture.X, PvpOpponentsFixture.Z, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", CasterAbilities.Select((id, i) => $"(6114,{400 + i},{0x80000000L | ((long)id << 10)},50,1)"))
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Deflect },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "23,43,48,315", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Deflect Code is initially loaded (+10 Determination Bonus). Page 2 slot 1 identifies the passive. Stay still outside interlock. Enter submits commands. Keep the hostile helper in view without Escape.",
            """Have QAEnemy cast 43 (Blind), 48 (Pacify), then 315 (Stun) using {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":43} with the documented ability changed each time. Wait for removal and all immunity to expire after each cast. The helper uses the recorded stock requests, with only the live handle changed.""",
            "Compare the state durations and actual removals, especially Pacify and Stun with their independent 12/14 s immunity. Once Stun and its immunity end, ?qaunload 23 and have the same helper cast 315 again. Its state returns to 7.35 s while immunity stays 14 s. This tests the current server policy for Deflect Code."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var caster = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(caster.Handle, caster);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, caster);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        if (context.StockProbe is not { } defender) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(defender.CharacterData[6]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await PvpOpponentsFixture.EnterAsync(defender);
        await defender.WaitForViewNamedAsync(caster.Handle);
        await StockAbilityProfile.ObserveAsync(defender, 0, frame => StockAbilityProfile.Bonus(frame, Deflect, 1047, 10, 0, false),
            "The seeded Deflect Code adds ten Determination");
        foreach (var sample in Samples)
            await CheckCastAsync(caster, defender, sample);

        int cursor = defender.World.RpcSnapshot().Count;
        await StockAbilityProfile.ChangeLoadoutAsync(defender, archive, new[] { Deflect }, load: false);
        await StockAbilityProfile.ObserveAsync(defender, cursor, frame => StockAbilityProfile.Bonus(frame, Deflect, 1047, 10, 0, true),
            "Unloading Deflect Code removes only its Determination record");
        await CheckCastAsync(caster, defender, Samples[^1] with { Duration = 7.35f });
        Check.Equal(0, defender.World.Problems.Count + caster.World.Problems.Count, "Deflect control replay framing");
        return Array.Empty<byte>();
    }

    public static async Task<object> SendControlAsync(HeadlessPlayer caster, string target, ushort ability)
    {
        if (caster.Handle != "QAEnemy" || target != "CSR_Tester")
            throw new InvalidOperationException("Use QAEnemy to cast a fixture control on CSR_Tester.");
        var sample = Samples.Single(value => value.Ability == ability);
        var view = await caster.WaitForViewNamedAsync(target);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        byte[] request = StarterHackerControlTargetMode.WithHandle(sample.Capture, handle);
        caster.SendRpc(request);
        return new { Ability = ability, Target = target, Request = Convert.ToHexString(request) };
    }

    private static async Task CheckCastAsync(HeadlessPlayer caster, HeadlessPlayer defender, Sample sample)
    {
        int cursor = defender.World.RpcSnapshot().Count;
        await SendControlAsync(caster, defender.Handle, sample.Ability);
        var state = await StockAbilityProfile.ObserveAsync(defender, cursor, frame =>
            DeflectCodeFixture.State(frame.Bytes, sample.Ability, sample.State, false, out float duration)
            && Math.Abs(duration - sample.Duration) < 0.005f,
            $"{sample.Ability} applies for {sample.Duration} seconds");

        RpcRecord? immunity = null;
        if (sample.Immunity != 0)
            immunity = await StockAbilityProfile.ObserveAsync(defender, cursor, frame =>
                DeflectCodeFixture.State(frame.Bytes, TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, false, out float duration)
                && duration == sample.Immunity, "Immunity retains its independent duration");

        await RequireRemovalAsync(defender, cursor, state, $"natural state removal for {sample.Ability}");
        if (immunity != null)
        {
            await RequireRemovalAsync(defender, cursor, immunity, "natural immunity removal");
            var records = defender.World.RpcSnapshot().Skip(cursor).ToList();
            Check.That(records.FindIndex(RemovalOf(state)) < records.FindIndex(RemovalOf(immunity)),
                "The shortened state removes before its original immunity");
        }
    }

    private static Task RequireRemovalAsync(HeadlessPlayer recipient, int cursor, RpcRecord add, string description)
    {
        var match = RemovalOf(add);
        return recipient.WaitUntilAsync(() => recipient.World.RpcSnapshot().Skip(cursor).Any(frame => match(frame)),
            description, TimeSpan.FromSeconds(18));
    }

    private static Predicate<RpcRecord> RemovalOf(RpcRecord record)
    {
        byte[] removal = record.Bytes.ToArray();
        removal[17] = 1;
        return frame => frame.Bytes.AsSpan().SequenceEqual(removal);
    }
}
