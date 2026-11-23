using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerSelectedPacifyRanksMode : NativeQaMode
{
    private const int Level = 30, X = 17000, Y = 495, Z = 8100;
    private const int CloseZ = 7900, MiddleZ = 6600, FarZ = 6099, FriendX = 17100;
    private const ushort CodeStop = 393, Freeze2 = 352, Freeze1 = 48, Blast = 58;
    private const float PacifySeconds = 6.54f, ImmunitySeconds = 12;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, CombatStyles.HackerStyle,
        CodeStop, Freeze2, Freeze1, Blast).Order().ToArray();

    public override string Name => "ability-parity-hacker-selected-pacify-ranks";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X,CloseZ)},innerStrC=700,innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={CloseZ}," +
                "alignment=2,pvpflag=0,healthC=1000,healthM=1000,innerStrC=500,innerStrM=500 WHERE charId=6114",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z}," +
                "alignment=1,pvpflag=0 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114)"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, CodeStop, Freeze2, Blast, Freeze1 },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "393,352", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            $"Level {Level}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{CloseZ}), 2 m south. QAFriend ({FriendX},{Y},{Z}) is the allied control. All stands are verified outdoor NAV away from the ice.",
            "Select Hacker Style on page 2 slot 1. With no target, then with /target QAFriend, try slots 2 and 3. Expect stock local suppression or reason 24 (hex 18) and no cost or reuse. Then /target QAEnemy.",
            $$"""Send helper move {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{FarZ}}} for 20.01 m. Both slots should be blocked locally or get reason 34. Move to z={{MiddleZ}} (15 m) and try only slot 2 to show its 14 m range. Return QAEnemy to z={{CloseZ}} before successful casts. CSR stays still.""",
            "Stage A. Press slot 2 (393), record its stock request and check [ABILITY-COST] paid=20 IS=700->680, 2 s cast, 15 s reuse. QAEnemy char=6114 gets [MODIFIER] source=393 stat=85 for 6.54 s and source=734 stat=681 for 12 s. QAEnemy loses no IS. Press Logic Blast in slot 4 immediately when Pacify lands. Its actual damage removes 393/85 early but leaves the original 734/681.",
            "Stage B. While that immunity is still active, use slot 3 (352). Record its first full stock request. Expect 2 s, 34 IS and 15 s reuse, [EFFECTS] 352 control immunity, no new state or timer, and source=352 stat=157 -48 for 12 s. If the client locally gates this attempt, record that and move on when immunity expires.",
            "Stage C. After both immunity and the 352 reuse clear, use slot 3 again. Expect source=352 stat=85 for 6.54 s, stat=157 -48 for 12 s, and independent 734/681 for 12 s. This time let all three records expire. No maximum [VITALS] changes or [PVP] round. Slot 5 is the retained rank-one stock control used in bootstrap."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (X, CloseZ), (X, MiddleZ), (X, FarZ), (FriendX, Z) })
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) <= 100, $"Verified outdoor NAV ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear fixture sight at ({x},{Y},{z})");
        }

        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loaded.Keys.ToHashSet().SetEquals(Abilities) && Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out ushort rank) &&
                rank >= need.Value)), "The stock profile includes both full prerequisite trees and fits in memory");

        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var enemy = context.Helpers["QAEnemy"];
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        await caster.WaitForViewNamedAsync("QAFriend");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        await VerifyRankOnePacifyAsync(caster, enemy, handle, PacifySeconds);
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Rank-one stock control framing");
        Console.WriteLine("SELECTED PACIFY BOOTSTRAP PASS: stock 48/58 control; rank-two and Code Stop stock frames pending.");
        return Array.Empty<byte>();
    }

    internal static async Task VerifyRankOnePacifyAsync(HeadlessPlayer caster, HeadlessPlayer enemy, uint handle, float pacifySeconds)
    {
        int cast = caster.World.RpcSnapshot().Count;
        int recipient = enemy.World.RpcSnapshot().Count;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(EarlyHackerControlRecoveryMode.PacifyRequest, handle));
        await EvidenceRetestWire.CastBarAsync(caster, Freeze1, 2);
        await EvidenceRetestWire.TimerAsync(caster, cast, Freeze1, AbilityCooldowns.OwnTimer, 43);
        var state = await ModifierAsync(Freeze1, TempMods.MezAbility, 0, pacifySeconds, false);
        var immunity = await ModifierAsync(TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, 0, ImmunitySeconds, false);
        await ModifierAsync(Freeze1, 157, -20, ImmunitySeconds, false);
        caster.SkipReceived();
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(StarterHackerDotResistanceMode.BlastUse, handle));
        await EvidenceRetestWire.CastBarAsync(caster, Blast, 2);
        await ModifierAsync(Freeze1, TempMods.MezAbility, 0, pacifySeconds, true, state.RecordId);
        await ModifierAsync(Freeze1, 157, -20, ImmunitySeconds, true);
        Check.That(!enemy.World.RpcSnapshot().Skip(recipient).Any(frame =>
            StockAbilityProfile.Bonus(frame, TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, 0, ImmunitySeconds, true)),
            "The damage break removes Freeze without removing its original immunity");
        await ModifierAsync(TempMods.ControlImmunityTimerAbility, TempMods.ISDrainTargetAbility, 0, ImmunitySeconds, true, immunity.RecordId);

        async Task<ManageBonusRecord> ModifierAsync(ushort source, ushort stat, int value, float duration, bool removed, ushort? locator = null)
        {
            var packet = await StockAbilityProfile.ObserveAsync(enemy, recipient, frame => ManageBonusRecord.TryDecode(frame.Bytes, out var mod) &&
                mod!.SourceId == source && mod.TargetId == stat && mod.SignedValue == value && Math.Abs(mod.Duration - duration) < 0.005f &&
                mod.AddRemoveType == (removed ? 1 : 0) && (!locator.HasValue || mod.RecordId == locator.Value), $"Stock 48 control record {source}/{stat}, remove={removed}");
            ManageBonusRecord.TryDecode(packet.Bytes, out var record);
            return record!;
        }
    }
}
