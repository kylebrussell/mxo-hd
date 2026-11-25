using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public class NetworkAttackerAccuracyMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 6400, EnemyZ = 6600, Rank = 50;
    private const ushort Network = 483, Grant = 1080, Accuracy = 1147, Style = CombatStyles.HackerStyle, Blast = 58;
    private static readonly (ushort Ability, float Reuse, string Capture) Sample =
        StarterHackerViralSightMode.Samples.Single(row => row.Ability == Blast);
    private readonly ushort parent;
    private readonly ushort[] loadout;
    private static readonly Regex Ledger = new(@"\[FREECAST-VIRAL\] ability=58 source=CSR_Tester target=QAEnemy inputs=(\d+)/(\d+) rolls=\d+/\d+ hit=True qa=True",
        RegexOptions.CultureInvariant);

    public override string Name => "ability-parity-network-attacker-accuracy";
    public override NativeQaModePlan Plan { get; }

    public NetworkAttackerAccuracyMode() : this(Network) { }

    protected NetworkAttackerAccuracyMode(ushort parent)
    {
        this.parent = parent;
        loadout = DisablingShotMode.Closure(2, 17, Style, parent, Blast);
        bool network = parent == Network;
        var (loaded, unloaded) = ExpectedAttack(parent);
        Plan = new(loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,level={Rank},exp={PlayerProgression.ExperienceForLevel(Rank)},innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={EnemyZ},rotation=128,level={Rank},exp={PlayerProgression.ExperienceForLevel(Rank)},healthC=5000,healthM=5000 WHERE charId=6114",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }, DevCommands: true, hotbarAbilities: new[] { Style, Blast }, testMobFixture: "martial-player-controls.csv",
            environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
                ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
            }, extraQaMetadata: new() { ["ScopedStockRows"] = parent.ToString(), ["NativeCloseableRows"] = network ? 1 : 0 },
            readyMessages: new[]
            {
                "Stay at verified HOME facing QAEnemy 2 m north. No tp. /target QAEnemy, page 2 slot 1 Hacker Style, then double-click Power in Actions > Awakened. Stay out of interlock.",
                $"Confirm source={parent} stat=1147 +5. Press page 2 slot 2 Logic Blast for [FREECAST-VIRAL] inputs={loaded}/0. Wait its 4 s reuse.",
                $"Use ?qaunload {parent}, repeat Logic Blast, then ?qaload {parent} and repeat. Expect attack/defense {loaded}/0, {unloaded}/0, {loaded}/0. The disposable aid fixes only contest draws.",
                network
                    ? "Network Attacker Upgrade disappears and returns with 483. After the comparison use it once for 20 IS, 30 s reuse and +7 Concentration, +12 IS regeneration, +9 Viral Accuracy on CSR only."
                    : "All three Code Shield records follow the Archive transition: stat1030 +11, stat1147 +5 and stat1042 +10. This run isolates Viral Accuracy. The original coefficient and the other live consumers remain open."
            });
    }

    private static (int Loaded, int Unloaded) ExpectedAttack(ushort parent) => parent == Network ? (72, 69) : (64, 63);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(X, Y, EnemyZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X, Y, EnemyZ), "Clear outdoor rear-free cast stand");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, loadout, Rank);
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        await AssertParentAsync(0, false);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        caster.SendRpc("42 01 00 00 00 00 00");
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        var loaded = await CastAsync();
        int off = await ChangeLoadoutAsync(caster, archive, new[] { parent }, false);
        await AssertParentAsync(off, true);
        var unloaded = await CastAsync();
        int on = await ChangeLoadoutAsync(caster, archive, new[] { parent }, true);
        await AssertParentAsync(on, false);
        var restored = await CastAsync();
        Check.Equal(loaded, restored, "The same normal cast returns to its original input");
        Check.Equal(loaded.Defense, unloaded.Defense, "No target input changes");
        var expected = ExpectedAttack(parent);
        Check.Equal((expected.Loaded, expected.Unloaded, expected.Loaded, 0), (loaded.Attack, unloaded.Attack, restored.Attack, loaded.Defense),
            "The ordinary cast sees the exact rank-50 +5 source contribution after truncation");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Clean retained stock framing");
        Console.WriteLine($"VIRAL ACCURACY PASS {parent}: attack {loaded.Attack}/{unloaded.Attack}/{restored.Attack}, defense {loaded.Defense}.");
        return Array.Empty<byte>();

        async Task AssertParentAsync(int at, bool remove)
        {
            await ObserveAsync(caster, at, p => Bonus(p, parent, Accuracy, 5, 0, remove), "The same +5 record follows the parent");
            if (parent != Network)
            {
                await ObserveAsync(caster, at, p => Bonus(p, parent, 1030, 11, 0, remove), "Code Shield publishes matching Viral Defense");
                await ObserveAsync(caster, at, p => Bonus(p, parent, 1042, 10, 0, remove), "Code Shield publishes matching Powerless Duration");
                return;
            }

            await ObserveAsync(caster, at, p => ManageBonusRecord.TryDecode(p.Bytes, out var m) && m is { Family: 0x345 } &&
                m.SourceId == Network && m.TargetId == Grant && m.ValueBits == 0 && m.AddRemoveType == (remove ? 1 : 0),
                "The zero grant follows the same ordinary Archive transition");
            byte[] expected = remove ? ServerPackets.BuildAbilityUnload(Grant) :
                ServerPackets.BuildAbilityLoad(Grant, 0, AbilityProgression.HiddenSlot);
            await ObserveAsync(caster, at, p => p.Bytes.AsSpan().SequenceEqual(expected), "Actions child changes with its parent");
        }

        async Task<(int Attack, int Defense)> CastAsync()
        {
            int from = caster.World.RpcSnapshot().Count, log = server.DebugLog().Length;
            caster.SendRpc(WithHandle(Sample.Capture, handle));
            await EvidenceRetestWire.CastBarAsync(caster, Blast, AbilityCatalog.Find(Blast).CastSeconds);
            await caster.WaitUntilAsync(() => Ledger.IsMatch(server.DebugLog()[log..]),
                "Retained Logic Blast gives a normal Viral input", HeadlessPlayer.DefaultTimeout);
            var match = Ledger.Match(server.DebugLog()[log..]);
            await EvidenceRetestWire.TimerAsync(caster, from, Blast, AbilityCooldowns.OwnTimer, Sample.Reuse, ended: true);
            return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
        }
    }
}
