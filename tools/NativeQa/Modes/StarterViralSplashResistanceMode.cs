using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterViralSplashResistanceMode : NativeQaMode
{
    private const int Level = 33, Rank = 19, X = 24800, Y = 495, Z = -1498, TargetZ = -1798;
    internal const int Resistance = 49;
    private const ushort Style = CombatStyles.HackerStyle, Barrage = 57, BarrageTwo = 59, Nuke = 60, EmptyMind = 8;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Barrage, BarrageTwo, Nuke, 75).Order().ToArray();
    private static readonly ushort[] ResistLoadout = StockAbilityProfile.Profile(2, 17, EmptyMind).Order().ToArray();

    public override string Name => "ability-parity-starter-viral-splash-resistance";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN(110,6111,6112,6113,6114)",
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,TargetZ)} WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={TargetZ},alignment=2 WHERE charId=6114",
            $"UPDATE characters SET x={X + 300},y={Y},z={TargetZ},alignment=2 WHERE charId=6112",
            $"UPDATE characters SET x={X + 150},y={Y},z={TargetZ - 100},alignment=1 WHERE charId=6111",
            $"UPDATE characters SET x={X - 460},y={Y},z={TargetZ},alignment=2 WHERE charId=6113",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", ResistLoadout.Select((id, i) => $"(6112,{i},{0x80000000u | ((uint)id << 10)},{Level},1)")),
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Style, Barrage, BarrageTwo, Nuke },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "57,59,60", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Rank-19 starter abilities at level 33. CSR faces the open street and QAEnemy at 3 m. QAFar stands 3 m to the east of QAEnemy, allied QAFriend about 1.8 m away, and hostile QAOther 4.6 m to the west. Select Hacker Style (page 2 slot 1), then QAEnemy. Remain outside interlock.",
            "Use slots 2 and 3 on QAEnemy, waiting for group 682 to clear between them. Expect two-second casts and four-second own and group reuse. Only QAEnemy is hit, for 9 and 19 respectively.",
            """Send {"actor":"QAFar","action":"aura-resist"} after both Barrages. Wait for OK and [MODIFIER] char=6112 source=8 stat=157 add value=+49 duration=20s, then press slot 4 immediately with QAEnemy still selected. Code Nuke starts a 4 s cast and 2 s own and group reuse. QAEnemy takes 8; QAFar takes 6 with type2 total49 raw8 final6. No damage to QAFriend or QAOther; area diagnostics distinguish ally from radius.""",
            "Wait for the matching source=8 stat=157 removal. Cast Code Nuke again on the same target and expect 8 on QAEnemy and 8 on QAFar. Both excluded helpers still take zero."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFar", "QAFriend", "QAOther" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var rows = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && rows.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                rows.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The full rank-19 profile fits in stock memory");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (string name in context.Helpers.Keys) await caster.WaitForViewNamedAsync(name);
        var primary = context.Helpers["QAEnemy"];
        var splash = context.Helpers["QAFar"];
        var target = await caster.WaitForViewNamedAsync(primary.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        await CastAsync(Barrage, 2, 4, 9);
        await CastAsync(BarrageTwo, 2, 4, 19);
        int resistAt = splash.World.RpcSnapshot().Count;
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(splash, Resistance);
        await CastAsync(Nuke, 4, 2, 8, splashDamage: 6, absorbed: 2);
        await splash.WaitUntilAsync(() => splash.World.RpcSnapshot().Skip(resistAt)
                .Any(frame => StockAbilityProfile.Bonus(frame, EmptyMind, 157, Resistance, 20, true)),
            "Empty Mind expires before the unresisted splash", TimeSpan.FromSeconds(23));
        await CastAsync(Nuke, 4, 2, 8, splashDamage: 8);
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "The starter viral comparison packet framing");
        return Array.Empty<byte>();

        async Task CastAsync(ushort id, int cast, int reuse, int primaryDamage, int? splashDamage = null, int absorbed = 0)
        {
            var start = context.Helpers.ToDictionary(pair => pair.Key, pair => pair.Value.World.RpcSnapshot().Count);
            int casterAt = caster.World.RpcSnapshot().Count;
            string request = StarterHackerViralSightMode.Samples.Single(row => row.Ability == id).Capture;
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(request, handle));
            await EvidenceRetestWire.CastBarAsync(caster, id, cast);
            await EvidenceRetestWire.TimerAsync(caster, casterAt, id, AbilityCooldowns.OwnTimer, reuse);
            await EvidenceRetestWire.TimerAsync(caster, casterAt, 682, AbilityCooldowns.GroupTimer, reuse);
            await CheckHitAsync(primary, primaryDamage, 0);
            if (splashDamage is int damage) await CheckHitAsync(splash, damage, absorbed);
            await EvidenceRetestWire.TimerAsync(caster, casterAt, 682, AbilityCooldowns.GroupTimer, reuse, ended: true);
            foreach (var helper in context.Helpers.Values.Where(player => player != primary && (player != splash || splashDamage == null)))
                Check.That(!helper.World.RpcSnapshot().Skip(start[helper.Handle]).Any(frame =>
                    frame.Bytes.Length >= 32 && frame.Bytes[0] == 0x81 && frame.Bytes[1] == 0x67 &&
                    BitConverter.ToUInt32(frame.Bytes, 6) == AbilityMessages.DamageTarget &&
                    BitConverter.ToUInt32(frame.Bytes, 15) == AbilityMessages.AbilityGameObject(id)),
                    $"{id} excludes {helper.Handle}");

            async Task CheckHitAsync(HeadlessPlayer victim, int damage, int reduction)
            {
                byte[] expected = AbilityMessages.Damage(false, id, caster.Handle, victim.Handle, damage, reduction);
                await StockAbilityProfile.ObserveAsync(victim, start[victim.Handle], frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                    $"{id} hits {victim.Handle} for {damage}, {reduction} absorbed");
            }
        }
    }
}
