using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class DisablingShotMode : NativeQaMode
{
    private const ushort Shot = 255, Rifles = CombatStyles.Rifles, Soldier = 154, Upgrade = 1061;
    private const int Level = 22, PickX = 21550, EnemyX = 21750, Z = 16150, Y = 1095;
    private const int Raw = 17, Resisted = 15;
    private static readonly ushort[] Loadout = Closure(2, 17, Rifles, Shot);
    private static readonly ushort[] EnemyLoadout = Closure(2, 17, Soldier);
    internal const string StockShot = "80b9ff00070005000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    internal const string StockRifles = "80b993000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    // Full PR381 view-zero Soldier Upgrade request, PacketLog 0291e4b5eae0:1880–1881.
    internal const string StockUpgrade = "80b925040000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-disabling-shot";
    public override NativeQaModePlan Plan { get; } = new(Loadout, PickX, Z, Array.Empty<string>(),
        new[] { CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle) }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},healthC=5000,healthM=5000,innerStrC=700,innerStrM=700 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={PickX},z={Z},rotation=64 WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},z={Z},rotation=64 WHERE charId=6114",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{0x80000000u | ((uint)id << 10)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { Rifles, Shot }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "255", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} legal memory. CSR ({PickX},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) from behind, clear of ice. Rifle in hand, page 2 Rifles and Disabling Shot.",
            """Select Rifles and QAEnemy, remain out of combat. Submit helper {"actor":"QAEnemy","action":"disabling-probe"} and press slot 2 promptly. Capture full stock 255. Expect 20 IS, 2 s, 10 s own and 5 s group 689, 17 damage. The helper watches for a natural 4 s Powerless proc and attempts Soldier Upgrade 1061. If no proc, wait for own reuse, use a fresh helper request and retry slot 2. The helper keeps running.""",
            """On a proc, expect source 255 state 96 and source 734/681 immunity for four seconds, a powerless refusal when the helper tries 1061, and paired removals. The helper then casts the same 1061 and reports [MODIFIER] source=1061 stat=181 add value=+20 and +50 helper max HP.""",
            "After reuse, select the same player and cast again at rest. Expect [TYPED-RESISTANCE OURS] 255 type1 total20 raw17 final15. No special animation or [PVP] Round is expected for this opener. Projected DONE +1 (255)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        _ = StarterConcealedShotTargetingMode.Stand(PickX, Z);
        _ = StarterConcealedShotTargetingMode.Stand(EnemyX, Z);
        Check.That(NavMesh.HasSightPath(1, PickX, Y, Z, EnemyX, Y, Z), "Disabling Shot has clear sight");

        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        CheckStand(enemy, EnemyX);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, enemy);
        CheckProfile(enemy, EnemyLoadout);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        CheckProfile(caster, Loadout);
        await PvpOpponentsFixture.EnterAsync(caster);
        CheckStand(caster, PickX);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, caster);
        int styled = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StockRifles);
        await ObserveAsync(caster, styled, rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
            record!.SourceId == Rifles && record.TargetId == 171 && record.AddRemoveType == 0, "Stock Rifles style");
        var view = await caster.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        byte[] request = WithHandle(StockShot, handle);
        bool witnessed = false;
        for (int attempt = 0; attempt < 6 && !witnessed; attempt++)
        {
            int cursor = caster.World.RpcSnapshot().Count, targetCursor = enemy.World.RpcSnapshot().Count;
            var probe = ProbeAsync(enemy);
            caster.SendRpc(request);
            await VerifyHitAsync(cursor, targetCursor, Raw);
            witnessed = (await probe).RefusedWhilePowerless;
            await ReuseEndsAsync(cursor);
        }
        Check.That(witnessed, "Natural 255 proc refused Soldier Upgrade, expired and allowed immediate recovery");

        int armorAt = caster.World.RpcSnapshot().Count, recipientAt = enemy.World.RpcSnapshot().Count;
        caster.SendRpc(request);
        await VerifyHitAsync(armorAt, recipientAt, Resisted);
        Check.That(context.ActualServer!.DebugLog().Contains("255 type1 total20 raw17 final15", StringComparison.Ordinal),
            "Player Ballistic Resistance changes only the typed shot");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Exact retained Disabling Shot framing");
        return Array.Empty<byte>();

        async Task VerifyHitAsync(int cursor, int targetCursor, int expected)
        {
            byte[] damage = AbilityMessages.Damage(false, Shot, caster.Handle, enemy.Handle, expected, Raw - expected);
            await EvidenceRetestWire.CastBarAsync(caster, Shot, 2);
            await ObserveAsync(enemy, targetCursor, rpc => rpc.Bytes.AsSpan().SequenceEqual(damage), $"Disabling Shot lands for {expected}");
            await EvidenceRetestWire.TimerAsync(caster, cursor, Shot, AbilityCooldowns.OwnTimer, 10);
            await EvidenceRetestWire.TimerAsync(caster, cursor, 689, AbilityCooldowns.GroupTimer, 5);
        }

        Task ReuseEndsAsync(int cursor)
        {
            byte[] expired = AbilityCooldowns.BuildTimer(Shot, AbilityCooldowns.OwnTimer, 10, false);
            return caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(cursor)
                .Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(expired)), "Disabling Shot own reuse expires", TimeSpan.FromSeconds(14));
        }
    }

    internal sealed record ProbeResult(bool RefusedWhilePowerless, bool ArmorActive);

    internal static async Task<ProbeResult> ProbeAsync(HeadlessPlayer enemy)
    {
        Check.Equal(6114u, enemy.CharId, "Disabling Shot recipient is QAEnemy only");
        try
        {
            await enemy.WaitUntilAsync(() => ActivePowerless(enemy), "A natural Disabling Shot Powerless proc", TimeSpan.FromSeconds(7));
        }
        catch (TimeoutException)
        {
            return new(false, false);
        }
        Check.That(enemy.World.RpcSnapshot().Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
            record is { SourceId: 734, TargetId: 681, AddRemoveType: 0 } && record.Duration == 4),
            "Four-second separate control immunity is present");

        int cursor = enemy.World.RpcSnapshot().Count;
        enemy.SendRpc(StockUpgrade);
        var answer = await ObserveAsync(enemy, cursor, rpc => ArmorRecord(rpc) ||
            AbilityRefusals.TryDecode(rpc.Bytes, out var refusal) && refusal.AbilityID == Upgrade, "Soldier Upgrade during Powerless");
        if (ArmorRecord(answer)) return new(false, true);
        Check.That(AbilityRefusals.TryDecode(answer.Bytes, out var denied) && denied.ErrorID == AbilityRefusals.LegacyAmbiguous,
            "Existing Powerless refusal reached the helper");
        await enemy.WaitUntilAsync(() => !ActivePowerless(enemy), "Matching four-second Powerless removal", TimeSpan.FromSeconds(7));

        int recovered = enemy.World.RpcSnapshot().Count;
        enemy.SendRpc(StockUpgrade);
        await ObserveAsync(enemy, recovered, ArmorRecord, "Recovered Soldier Upgrade adds +20 Ballistic Resistance");
        return new(true, true);
    }

    private static bool ArmorRecord(RpcRecord rpc) => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
        record is { SourceId: Upgrade, TargetId: 181, ValueBits: 20, AddRemoveType: 0 };

    private static bool ActivePowerless(HeadlessPlayer enemy) => enemy.World.RpcSnapshot()
        .Select(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) ? record : null)
        .LastOrDefault(record => record is { SourceId: Shot, TargetId: TempMods.PowerlessAbility }) is
            { AddRemoveType: 0 } active && active.Duration == 4;

    internal static ushort[] Closure(params ushort[] roots) =>
        roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    internal static void CheckStand(HeadlessPlayer actor, int x)
    {
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(EngageFoesFixture.Position(self).SequenceEqual(new double[] { x, Y, Z }),
            $"{actor.Handle} entered on the intended verified stand");
    }

    internal static void CheckProfile(HeadlessPlayer actor, ushort[] expected, int level = Level)
    {
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(expected.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level) && loaded.Keys.ToHashSet().SetEquals(expected) &&
            expected.All(id => AbilityPrerequisites.Current.Closure(id).All(required =>
                loaded.TryGetValue(required.Key, out ushort rank) && rank >= required.Value)), $"Exact legal level-{level} rifle opener profile");
    }
}
