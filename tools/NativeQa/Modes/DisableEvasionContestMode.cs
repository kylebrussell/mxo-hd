using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class DisableEvasionContestMode : NativeQaMode
{
    private const int X = 21550, Y = 1095, Z = 16150, TargetX = 21750, OtherZ = 16250, Level = 50;
    private const ushort Spy = CombatStyles.SpyStyle, Sneak = Stealth.SneakGrantAbility,
        Disable = StealthShieldAbilities.DisableEvasion, Evade = EvasionShield.Parent;
    private const uint Analyzer = 465;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, 151, Disable);

    public override string Name => "ability-parity-disable-evasion-contest";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},pvpflag=1 WHERE charId IN (110,6113,6114)",
        $"UPDATE characters SET x={X},z={Z},rotation=64,alignment=1,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={TargetX},z={Z},rotation=64,alignment=2,innerStrC=1000,innerStrM=1000 WHERE charId=6114",
        $"UPDATE characters SET x={TargetX},z={OtherZ},rotation=64,alignment=2,innerStrC=1000,innerStrM=1000 WHERE charId=6113",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id IN (6113,6114)",
        $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES (6113,400,{AbilityMessages.AbilityGameObject(Evade)},{Level},1),(6114,400,{AbilityMessages.AbilityGameObject(Evade)},{Level},1)",
        "DELETE FROM inventory WHERE charId=110 AND slot=1",
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,{Analyzer},1,1,0)"
    }, DevCommands: true, hotbarAbilities: new[] { Spy, Sneak },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(1, Analyzer, "Weakness Analyzer") },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_DISABLE_EVASION_QA"] = "split"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "1125", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay at verified outdoor HOME, facing two Evade-active hostiles. No tp. Page 2 slot 1 Spy Style, slot 2 Sneak, slot 3 Weakness Analyzer. If slot 3 only shows its tooltip, use Actions > Tools > right-click Weakness Analyzer > Add Item to Hotbar and press that shortcut. The raw ability itself has no tool slot.",
            "Select Spy then Sneak. /target QAOther, then the tool. Expect 25 IS, 4 s cast and a deflected line. Sneak and the target's Evade stay active. No source1125/710 or 1125/681. Wait out the 45 s own timer.",
            "Still sneaking, /target QAEnemy and use the tool. Expect 25 IS, 4 s cast, throw contest hit, [EVASION] QAEnemy state off; parent217=True, source1125 stat710 for 8 s and caster stat681 for 1.5 s. Sneak returns after 1.5 s and the same Evade child returns with the same parent record. Neither sample deals damage. Save the first stock tool frame.",
            "QA fixes only the two opposed contest draws by recipient on the disposable server. The shield, cast, own reuse and remainder timings take normal paths."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        foreach (var (name, x, z) in new[] { ("HOME", X, Z), ("QAEnemy", TargetX, Z), ("QAOther", TargetX, OtherZ) })
        {
            Console.WriteLine($"READY {name} {InvisibilityBeltWatcherMode.Warp(x, Y, z, 64, inside: false)}");
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear sight toward {name}");
        }
        var server = context.ActualServer!;
        var (enemy, initial) = await ShieldHolderAsync("QAEnemy");
        var (other, _) = await ShieldHolderAsync("QAOther");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        caster.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        caster.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await caster.WaitUntilAsync(() => server.DebugLog().Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
            "Sneak is active before the tool cast", HeadlessPlayer.DefaultTimeout);
        int otherAt = other.World.RpcSnapshot().Count;
        int missAt = await CastAsync(other, hit: false);
        var reuseEnd = BytePattern.Parse("80 bd 01 65 04 04 00 00 34 42 00");
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(missAt).Any(p => reuseEnd.IsMatch(p.Bytes)),
            "The first 45 s own reuse expires normally", TimeSpan.FromSeconds(48));
        Check.That(!other.World.RpcSnapshot().Skip(otherAt).Any(p => HasRecord(p, Disable, 710) || HasRecord(p, Evade, 624)),
            "A deflect leaves the target's original shield active");
        Check.That(!caster.World.RpcSnapshot().Skip(missAt).Any(p => HasRecord(p, Disable, 681)),
            "The failed throw starts no caster restealth marker");

        int enemyAt = enemy.World.RpcSnapshot().Count;
        int hitAt = await CastAsync(enemy, hit: true);
        await ObserveAsync(enemy, enemyAt, p => Bonus(p, Disable, 710, 0, 8, false), "Successful throw suppresses Evade");
        await ObserveAsync(caster, hitAt, p => Bonus(p, Disable, 681, -1, 1.5f, false), "The caster's separate marker starts");
        await ObserveAsync(enemy, enemyAt, p => Bonus(p, Disable, 710, 0, 8, true), "Target modifier retires once");
        var child = await ObserveAsync(enemy, enemyAt, p => HasRecord(p, Evade, 624) &&
            ManageBonusRecord.TryDecode(p.Bytes, out var mod) && mod!.AddRemoveType == 0, "The original shield child returns");
        Check.Equal(BitConverter.ToUInt16(initial, 4), BitConverter.ToUInt16(child.Bytes, 4), "Same parent record identity");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + other.World.Problems.Count, "Stock protocol framing");
        Console.WriteLine("DISABLE EVASION CONTEST PASS: preserved shield on deflect, suppression and the same shield identity on success.");
        return Array.Empty<byte>();

        async Task<(HeadlessPlayer Player, byte[] Initial)> ShieldHolderAsync(string name)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            return (helper, await StealthShieldFixture.ActivateTargetAsync(helper));
        }

        async Task<int> CastAsync(HeadlessPlayer target, bool hit)
        {
            var view = await caster.WaitForViewNamedAsync(target.Handle);
            uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
            int from = caster.World.RpcSnapshot().Count, log = server.DebugLog().Length;
            // Full 80 b9 targeted tool shape. PR547 established the Weakness Analyzer in actual bag slot 1.
            caster.SendRpc(new AbilityUseRecord(Disable, handle, 0, 0, 0, Disable | (0x6bUL << 40), 8, 1, 0, 0).Encode());
            await EvidenceRetestWire.CastBarAsync(caster, Disable, 4);
            var contest = new Regex($@"\[DISABLE-EVASION CONTEST\] CSR_Tester -> {target.Handle} inputs\(1032/1011\)=\d+/\d+ rolls=\d+/\d+ hit={hit} qa=True");
            await caster.WaitUntilAsync(() => contest.IsMatch(server.DebugLog()[log..]), "The expected thrown contest outcome", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(caster, from, Disable, AbilityCooldowns.OwnTimer, 45);
            Check.That(server.DebugLog()[log..].Contains($"ability={Disable} paid=25", StringComparison.Ordinal), "The legal cast costs 25 IS");
            return from;
        }
    }

    private static bool HasRecord(RpcRecord rpc, ushort source, ushort target) =>
        ManageBonusRecord.TryDecode(rpc.Bytes, out var record) && record!.SourceId == source && record.TargetId == target;
}
