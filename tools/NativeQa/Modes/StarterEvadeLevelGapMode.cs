using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterEvadeLevelGapMode : NativeQaMode
{
    private const int Level = 19, X = PvpOpponentsFixture.X, Y = PvpOpponentsFixture.Y, Z = PvpOpponentsFixture.Z;
    private const ushort ExecuteProgram = 45, Blur = 49, Insight = 113, Evade = 217;
    private const string StockEvade = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string StockBlur = "80b93100000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, ExecuteProgram, Blur, Evade).Order().ToArray();
    private static readonly ushort[] AttackLoadout = new ushort[] { 2, 17, Insight };
    private static readonly (string Name, uint Id, int Level, int X, int Z)[] Attackers =
    {
        ("QAEnemy", 6114, 11, X + 120, Z),
        ("QAOther", 6113, 19, X, Z + 150)
    };

    public override string Name => "ability-parity-starter-evade-level-gap";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},rotation=64," +
                "innerStrC=77,innerStrM=77 WHERE charId=110",
            "DELETE FROM inventory WHERE charId IN (110,6113,6114) AND slot IN (105,107)",
            "DELETE FROM char_abilities WHERE char_id IN (6113,6114)"
        }.Concat(Attackers.SelectMany(s => new[]
        {
            $"UPDATE characters SET alignment=2,level={s.Level},exp={PlayerProgression.ExperienceForLevel(s.Level)},x={s.X},y={Y},z={s.Z} WHERE charId={s.Id}",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", AttackLoadout.Select((id, i) => $"({s.Id},{400+i},{AbilityMessages.AbilityGameObject(id)},{s.Level},1)"))
        })).ToArray(), DevCommands: true, hotbarAbilities: new[] { Evade, Blur }, hotbarWithdraw: true,
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "45,49,113,217", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"CSR level {Level} at ({X},{Y},{Z}), QAEnemy level 11 at ({X+120},{Y},{Z}), QAOther level 19 at ({X},{Y},{Z+150}). All stands are outdoor NAV, clear of ambient aggro. Remain in Speed, selection clear.",
            "Page 2 slot 1 Evade Combat (77->37), slot 2 Gaussian Blur (37->17, 2 s cast). Expect the stock Evade Combat activation line (81 67 string 7c0001b9), [EVASION] state on, [MODIFIER] 217/624 and [MODIFIER] 49/520 +25 for 600 s. Execute Program 45 stays loaded.",
            """Send {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. Expect [EVASION] IS 17->10 cost=7, shield 255->194 damage=24 and no interlock. Both helpers already selected Power.""",
            """Send {"actor":"QAOther","action":"melee","target":"CSR_Tester"}. Expect [EVASION] IS 10->0 cost=10, shield 194->102 damage=36. The state stays active after the exact payment.""",
            "Repeat QAEnemy melee at 0 IS. Expect [EVASION] resistance shortfall required=7 available=0, shield released, 624 removed and 30 s own reuse. Blur stays active. Only another new melee can open interlock. No max [VITALS] changes during the sample."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x,z) in Attackers.Select(s => (s.X,s.Z)).Append((X,Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified outdoor stand and clear route ({x},{Y},{z})");

        foreach (var (name, _, level, _, _) in Attackers)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            CheckProfile(helper, AttackLoadout, level);
            await helper.EnterWorldAsync();
            await helper.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
            helper.SendRpc("42 01 00 00 00 00 00");
            await StockAbilityProfile.ObserveAsync(helper, 0, frame =>
                ManageBonusRecord.TryDecode(frame.Bytes, out var bonus) && bonus!.SourceId == 1017 && bonus.TargetId == 1012,
                $"{name} selected Power");
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        CheckProfile(actor, Loadout, Level);
        await actor.EnterWorldAsync();
        await actor.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var views = new Dictionary<string, ViewCreation>();
        foreach (var (name, helper) in context.Helpers)
        {
            var view = await helper.WaitForViewNamedAsync(actor.Handle);
            views[name] = view;
        }

        var start = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockEvade);
        await StockAbilityProfile.ObserveAsync(actor, start,
            frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, false), "Owner sees live Evade shield");
        var notice = BytePattern.Parse("81 67 17 00 ?? ?? b9 01 00 7c 01 d9 00 00 00");
        await StockAbilityProfile.ObserveAsync(actor, start, frame => notice.IsMatch(frame.Bytes) &&
            frame.Bytes.AsSpan().SequenceEqual(AbilityMessages.AboutAbility(0x7c0001b9, true, Evade, actor.Handle, actor.Handle, 0, 0)),
            "The caster receives the captured Evade Combat activation message");
        await StrengthAsync(37);
        actor.SendRpc(StockBlur);
        await EvidenceRetestWire.CastBarAsync(actor, Blur, 2);
        await StockAbilityProfile.ObserveAsync(actor, start, frame => StockAbilityProfile.Bonus(frame, Blur, 520, 25, 600, false),
            "Gaussian Blur adds direct evasion on the current shield");
        await StrengthAsync(17);

        await ResistAsync("QAEnemy", "resistance IS 17->10 cost=7; shield 255->194 damage=24", strength: 10, health: 194);
        await ResistAsync("QAOther", "resistance IS 10->0 cost=10; shield 194->102 damage=36", strength: 0, health: 102);
        Check.That(!actor.World.RpcSnapshot().Skip(start).Any(frame =>
            StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, true)), "Exact payment keeps the shield active");

        int end = actor.World.RpcSnapshot().Count;
        await ResistAsync("QAEnemy", "resistance shortfall required=7 available=0, shield released");
        await StockAbilityProfile.ObserveAsync(actor, end,
            frame => StockAbilityProfile.Bonus(frame, Evade, EvasionShield.StateAbility, 0, 0, true), "Shortfall removes the Evading child");
        await EvidenceRetestWire.TimerAsync(actor, end, Evade, AbilityCooldowns.OwnTimer, 30);
        Check.That(!actor.World.RpcSnapshot().Skip(end).Any(frame => StockAbilityProfile.Bonus(frame, Blur, 520, 25, 600, true)),
            "The other buff survives the shield shortfall");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Evade Combat entry replay framing");
        return Array.Empty<byte>();

        Task StrengthAsync(ushort value) => actor.WaitUntilAsync(() =>
            AwakenedPowerShotMode.InnerStrengthUpdates(actor.World.ViewSnapshot()).LastOrDefault(ushort.MaxValue) == value,
            $"Self view reports {value} IS", HeadlessPlayer.DefaultTimeout);

        async Task ResistAsync(string name, string expected, ushort strength = 0, byte health = 0)
        {
            var helper = context.Helpers[name];
            var view = views[name];
            string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
            int logAt = server.DebugLog().Length;
            helper.SkipReceived();
            actor.SkipReceived();
            helper.SendRpc($"40 {handle} 01 6b");
            await helper.WaitForRpcAsync($"4d 0e 00 01 00 {handle}");
            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains(expected, StringComparison.Ordinal),
                expected, HeadlessPlayer.DefaultTimeout);
            if (health > 0)
            {
                byte[] own = PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfInnerStrength, PlayerViewAttributes.U16(strength)),
                    (30, PlayerViewAttributes.U8(health)));
                await actor.WaitForViewUpdateAsync(2, WireFormat.ToHex(own[2..]));
                byte[] other = PlayerViewAttributes.Build((21, PlayerViewAttributes.U8(health)));
                await helper.WaitForViewUpdateAsync(view.ViewId, WireFormat.ToHex(other));
            }
        }
    }

    private static void CheckProfile(HeadlessPlayer actor, ushort[] loadout, int level)
    {
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level) && loadout.All(id =>
            rows.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                rows.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), $"{actor.Handle} has qualified loaded abilities within memory");
    }
}
