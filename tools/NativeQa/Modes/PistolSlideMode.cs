using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class PistolSlideMode : NativeQaMode
{
    private const ushort Slide = 241, Handguns = CombatStyles.Handguns, Soldier = 154, Upgrade = 1061;
    private const int Level = 20, X = 17000, Y = 495, CasterZ = 6400, EnemyZ = 6600, OtherX = 17100;
    private const int Neutral = 73, Resisted = 64;
    private static readonly ushort[] Loadout = Closure(2, 17, Handguns, Slide);
    private static readonly ushort[] EnemyLoadout = Closure(2, 17, Soldier);
    // PR330/30f7f0fc retained handgun-choreography request, and the PR381 view-zero Soldier upgrade.
    private const string StockSlide = "80b9f100040002000000000000000000000000000000000000000000f1000000006b0000086b0000000000";
    internal const string StockHandguns = "80b98100040002000000000000000000000000000000000000000000985bac0001000000086b0000000000";
    internal const string StockSelfDefense = "80b911000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string StockUpgrade = "80b925040000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-pistol-slide";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, CasterZ, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Handgun) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},healthC=15000,healthM=15000,innerStrC=700,innerStrM=700 WHERE charId IN (110,6113,6114)",
            $"UPDATE characters SET x={X},z={CasterZ},rotation=0 WHERE charId=110",
            $"UPDATE characters SET x={X},z={EnemyZ},rotation=128 WHERE charId=6114",
            $"UPDATE characters SET x={OtherX},z={EnemyZ},rotation=128,alignment=2 WHERE charId=6113",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{0x80000000u | ((uint)id << 10)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Slide },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_CONDITIONAL_THROW_QA"] = "1",
            [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "241", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} legal memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{EnemyZ}); the second hostile QAOther ({OtherX},{Y},{EnemyZ}) is off to the side. A revolver is equipped. Page 2 Handguns then Pistol Slide.",
            "Select Handguns and QAEnemy. Try slot 2 at rest. Open ordinary Close Combat, reselect Handguns and try slot 2 before imposing Off Balance. Expect a grey local gate or a refusal without IS/reuse.",
            "Select QAEnemy again. Enter ?cd offbalance 30 and wait for 885/200 on QAEnemy. Press slot 2. Expect [SPECIAL-TACTIC OURS] ability=241 bonus=29 with total=base+29 on the queued round. Retain the exact request, [PVP] landed 9527 and a 73 hit. Expect 20 IS, 10 s own and 5 s group 690. An ordinary contest miss can be retried after reuse. A natural Powerless proc lasts 8 s.",
            """Submit ?cd fixture-disengage and wait for the HUD to clear. Send helper {"actor":"QAEnemy","action":"slide-armor"}. The helper waits out any landed Powerless. Verify [MODIFIER] char=6114 source=1061 stat=181 add value=+20. Reopen Close Combat on QAEnemy, reselect Handguns and apply ?cd offbalance 30 again. Slot 2 should produce [TYPED-RESISTANCE OURS] 241 type1 total20 raw73 final64 and the same 9527. The upgrade raises only the helper's max HP by 50. Projected DONE +1 (241)."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, CasterZ), (X, EnemyZ), (OtherX, EnemyZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), $"Verified outdoor NAV and sight ({x},{Y},{z})");

        var enemy = await EnterAsync("QAEnemy");
        Check.That(EnemyLoadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal Soldier helper memory");
        var other = await EnterAsync("QAOther");
        enemy.SendRpc("42 01 00 00 00 00 00");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var owned = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && owned.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(required =>
                owned.TryGetValue(required.Key, out ushort rank) && rank >= required.Value)), "Legal L20 Pistol Slide prerequisites");
        await PvpOpponentsFixture.EnterAsync(caster);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        uint opponent = Handle(await caster.WaitForViewNamedAsync(enemy.Handle));
        uint differentEnemy = Handle(await caster.WaitForViewNamedAsync(other.Handle));
        byte[] slide = WithHandle(StockSlide, opponent);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(opponent));
        await RefusedAsync(caster, Slide, slide);
        await OpenAsync();
        await RefusedAsync(caster, Slide, WithHandle(StockSlide, differentEnemy));
        await RefusedAsync(caster, Slide, WithHandle(StockSlide, opponent ^ 0x10000));
        var gated = await RefusedAsync(caster, Slide, slide, AbilityRefusals.TargetStateRequired);
        Check.That(AbilityRefusals.TryDecode(gated.Bytes, out var refusal) && refusal.StatesNotMet == 4, "Authentic missing Off Balance bit");
        await LandAsync(Neutral);
        caster.SendChat("?cd fixture-disengage");
        await caster.WaitForTextAsync("Fixture combat ended.");
        await ApplyArmorAsync(enemy);
        await OpenAsync();
        await LandAsync(Resisted);
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + other.World.Problems.Count, "Pistol Slide stock framing");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> EnterAsync(string actorName)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, actorName, actorName, stockCharacterLoad: true);
            context.Helpers.Add(actorName, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            return helper;
        }

        async Task OpenAsync()
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.World.SendRpcs(WithHandle(StockSelfDefense, opponent), WireFormat.FromHex($"40 {handle} 01 6b"));
            await ObserveAsync(caster, cursor, rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1,
                "Ordinary PvP Close Combat opens");
            int styleAt = caster.World.RpcSnapshot().Count;
            caster.SendRpc(WithHandle(StockHandguns, opponent));
            await ObserveAsync(caster, styleAt, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var r) &&
                r!.SourceId == Handguns && r.TargetId == 171 && r.ValueBits == 6 && r.AddRemoveType == 0, "Handguns contributes +6 Ballistic Damage");
            caster.SendRpc($"81 51 {handle}");
        }

        async Task LandAsync(int expected)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                caster.SkipReceived();
                caster.SendChat("?cd offbalance 30");
                await caster.WaitForTextAsync("OffBalance fixture applied 885/200 to QAEnemy");
                int logAt = server.DebugLog().Length, timerAt = caster.World.RpcSnapshot().Count;
                caster.SendRpc(slide);
                await EvidenceRetestWire.TimerAsync(caster, timerAt, Slide, AbilityCooldowns.OwnTimer, 10);
                await EvidenceRetestWire.TimerAsync(caster, timerAt, 690, AbilityCooldowns.GroupTimer, 5);
                byte[] expired = AbilityCooldowns.BuildTimer(Slide, AbilityCooldowns.OwnTimer, 10, false);
                await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(timerAt)
                    .Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(expired)), "Pistol Slide own reuse expires", TimeSpan.FromSeconds(14));
                string log = server.DebugLog()[logAt..];
                Check.That(log.Contains("[ABILITY-COST] char=110 ability=241 paid=20", StringComparison.Ordinal),
                    "Each admitted Pistol Slide debits the stored 20 IS");
                Check.That(log.Contains("[SPECIAL-TACTIC OURS] ability=241 bonus=29 ", StringComparison.Ordinal),
                    "The queued round uses Pistol Slide's authored tactic bonus");
                if (!log.Contains("PistolSlideAbility lands on QAEnemy for ", StringComparison.Ordinal)) continue;
                Check.That(log.Contains($"PistolSlideAbility lands on QAEnemy for {expected}", StringComparison.Ordinal) &&
                    log.Contains("animation 9527 (Pistol Slide)", StringComparison.Ordinal), $"Expected Pistol Slide damage {expected} and native move 9527");
                if (expected == Resisted)
                    Check.That(log.Contains("241 type1 total20 raw73 final64", StringComparison.Ordinal), "Typed Ballistic resistance on the same opponent");
                return;
            }
            throw new InvalidOperationException($"No Pistol Slide hit for {expected} landed in five ordinary combat rolls.");
        }
    }

    internal static async Task ApplyArmorAsync(HeadlessPlayer helper)
    {
        Check.Equal(6114u, helper.CharId, "The Soldier Upgrade belongs on QAEnemy only");
        // A natural 241 proc can still be active after disengage. Preserve its ordinary refusal window.
        await helper.WaitUntilAsync(() => !helper.World.RpcSnapshot()
            .Select(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) ? record : null)
            .Where(record => record is { SourceId: Slide, TargetId: TempMods.PowerlessAbility })
            .Select(record => record!.AddRemoveType == 0).LastOrDefault(),
            "QAEnemy's Pistol Slide Powerless has expired", TimeSpan.FromSeconds(10));
        int cursor = helper.World.RpcSnapshot().Count;
        helper.SendRpc(StockUpgrade);
        await ObserveAsync(helper, cursor, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) && record!.SourceId == Upgrade &&
            record.TargetId == 181 && record.ValueBits == 20 && record.AddRemoveType == 0, "Stock Soldier Upgrade adds +20 Ballistic Resistance");
    }

    private static ushort[] Closure(params ushort[] roots) =>
        roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
