using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public class StarterControlDeflectMode : NativeQaMode
{
    private const ushort Deflect = 23, Style = 1006, Blind = 43, Pacify = 48, Stun = 315;
    private static readonly ushort[] CasterAbilities = StockAbilityProfile.Profile(2, 17, Style, Blind, Pacify, Stun).Order().ToArray();
    private readonly ushort passive;
    private readonly (ushort Stat, int Value)[] bonuses;
    private const int X = 17000, Y = 495, Home = 6400, Enemy = 6600;

    private sealed record Sample(ushort Ability, ushort State, float Duration, int Immunity, string Capture);
    private static readonly Sample[] Samples =
    {
        new(Blind, TempMods.BlindAbility, 14.4f, 0, EarlyHackerControlRecoveryMode.BlindRequest),
        new(Pacify, TempMods.MezAbility, 5.4f, 12, EarlyHackerControlRecoveryMode.PacifyRequest),
        new(Stun, TempMods.StunAbility, 6.615f, 14, EarlyHackerControlRecoveryMode.StunRequest)
    };

    public override string Name => "ability-parity-starter-deflect-controls";
    public override NativeQaModePlan Plan { get; }

    public StarterControlDeflectMode() : this(Deflect, new (ushort, int)[] { (1047, 10) }) { }

    protected StarterControlDeflectMode(ushort passive, (ushort Stat, int Value)[] bonuses)
    {
        this.passive = passive;
        this.bonuses = bonuses;
        bool deflect = passive == Deflect;
        var abilities = DisablingShotMode.Closure(2, 17, passive);
        var sql = new List<string>
        {
            PvpOpponentsFixture.NativeSql,
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", CasterAbilities.Select((id, i) => $"(6114,{400 + i},{0x80000000L | ((long)id << 10)},50,1)"))
        };
        if (!deflect)
        {
            sql.Add($"UPDATE characters SET x={X},y={Y},z={Home},rotation=0 WHERE charId=110");
            sql.Add($"UPDATE characters SET x={X},y={Y},z={Enemy},rotation=128 WHERE charId=6114");
        }
        Plan = new(abilities, deflect ? PvpOpponentsFixture.X : X, deflect ? PvpOpponentsFixture.Z : Home,
            Array.Empty<string>(), Array.Empty<string>(), sql.ToArray(),
            DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { passive },
            environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
                ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
                ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
                ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
            }, extraQaMetadata: new() { ["ScopedStockRows"] = deflect ? "23,43,48,315" : passive.ToString(), ["NativeCloseableRows"] = 0 },
            readyMessages: deflect ? new[]
            {
                "Deflect Code is initially loaded (+10 Determination Bonus). Page 2 slot 1 identifies the passive. Stay still outside interlock. Enter submits commands. Keep the hostile helper in view without Escape.",
                """Have QAEnemy cast 43 (Blind), 48 (Pacify), then 315 (Stun) using {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":43} with the documented ability changed each time. Wait for removal and all immunity to expire after each cast. The helper uses the recorded stock requests, with only the live handle changed.""",
            } : new[]
            {
                $"Verified open HOME ({X},{Y},{Home}) faces QAEnemy ({X},{Y},{Enemy}). No initial teleport or selection. Remain at rest. Check source498 stat1047 +5, stat1042 +10, stat1143 +10 and stat1140 +5.",
                """Request {"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":315}. It sends retained stock Stun. Expect state 102 at 6.9825 s and separate 734/681 immunity at 14 s. Wait for both removals before repeating. The QA aid fixes only the helper's Viral contest draws.""",
                "Type ?qaunload 498 and expect matching removes for all four stats. Repeat the same Stun for 7.35 s with 14 s immunity. After both removals use ?qaload 498 and repeat for 6.9825 s and 14 s. Original Determination scaling and the other live stat consumers remain open."
            });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (passive != Deflect)
        {
            SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
            NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
            Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Home, 0, inside: false)}");
            Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(X, Y, Enemy, 128, inside: false)}");
            Check.That(NavMesh.HasSightPath(1, X, Y, Home, X, Y, Enemy), "Clear outdoor Stun path");
        }
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
        await VerifyRecordsAsync(0, false);
        if (passive == Deflect)
            foreach (var sample in Samples)
                await CheckCastAsync(caster, defender, sample);
        else
            await CheckCastAsync(caster, defender, Samples[^1] with { Duration = 6.9825f });

        int cursor = await StockAbilityProfile.ChangeLoadoutAsync(defender, archive, new[] { passive }, load: false);
        await VerifyRecordsAsync(cursor, true);
        await CheckCastAsync(caster, defender, Samples[^1] with { Duration = 7.35f });
        if (passive != Deflect)
        {
            cursor = await StockAbilityProfile.ChangeLoadoutAsync(defender, archive, new[] { passive }, load: true);
            await VerifyRecordsAsync(cursor, false);
            await CheckCastAsync(caster, defender, Samples[^1] with { Duration = 6.9825f });
            Console.WriteLine("HANDGUN ARTISTRY PASS: 6.9825/7.35/6.9825 s Stun with independent 14 s immunity.");
        }
        Check.Equal(0, defender.World.Problems.Count + caster.World.Problems.Count, "Deflect control replay framing");
        return Array.Empty<byte>();

        async Task VerifyRecordsAsync(int from, bool remove)
        {
            foreach (var (stat, value) in bonuses)
                await StockAbilityProfile.ObserveAsync(defender, from,
                    frame => StockAbilityProfile.Bonus(frame, passive, stat, value, 0, remove),
                    $"Matching passive {passive} stat {stat} loaded={!remove}");
        }
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
