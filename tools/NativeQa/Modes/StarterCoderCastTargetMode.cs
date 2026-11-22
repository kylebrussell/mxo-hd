using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterCoderSplashBatchMode;

namespace NativeQa.Modes;

public sealed class StarterCoderCastTargetMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, CasterZ = 8100, NearZ = 6600, FarZ = 6400;
    private const int SecondaryX = X + 100, SecondaryZ = 6500, StartingIs = 600;
    private const ushort Style = CombatStyles.CoderStyle;
    private static readonly ushort[] Hotbar = Samples.Select(sample => sample.Id).Prepend(Style).ToArray();
    // Same ranked prerequisite profile as the native Coder boundary run. The seeder owns discipline parents separately.
    private static readonly ushort[] Loadout = Hotbar.Concat(new ushort[] { 2, 17 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-coder-cast-target";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6112,6114)",
            $"UPDATE characters SET x={X},y={Y},z={CasterZ},rotation={MobAggro.YawTowards(X,CasterZ,X,NearZ)}," +
                $"innerStrM=800,innerStrC={StartingIs} WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={NearZ},alignment=2 WHERE charId=6114",
            $"UPDATE characters SET x={SecondaryX},y={Y},z={SecondaryZ},alignment=2 WHERE charId=6112",
            "DELETE FROM char_abilities WHERE char_id IN (6112,6114)",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: Hotbar, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "33,36,514", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy at ({X},{Y},{NearZ}), 15 m away. QAFar ({SecondaryX},{Y},{SecondaryZ}) is within 2 m of QAEnemy at either stand. The caster, targets and both move endpoints are on verified outdoor NAV with clear sight.",
            "Select Coder Style (page 2 slot 1) and /target QAEnemy. Use Tinkering (slot 3); after its 6 s bar appears, immediately submit the OUT helper move. On completion expect [ABILITY] 36 fizzles: the selected Coder target changed or left range, normal 20 IS cost and 30 s reuse, with no hit to QAEnemy or QAFar.",
            "Submit the IN helper move before the next cast, reselect QAEnemy if needed, then repeat with Advanced Tinkering (slot 4, 6 s cast, 40 IS, 30 s reuse). On the native UI, Static Blast (slot 2) can be left for the positive check because its 2 s bar leaves less time to submit OUT.",
            $$"""OUT: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{FarZ}}}. IN: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{NearZ}}}. Keep CSR on the original stand. Confirm the helper move is seen before the cast finishes.""",
            "Put QAEnemy back IN, then use slots 2, 3 and 4 once each as their timers clear. Static Blast (15 IS, 2 s cast, 6 s own/4 s group reuse) hits QAEnemy for 24 only. Tinkering and Advanced Tinkering hit both opposing helpers for 4 and 6. Native costs without the optional Static Blast fizzle leave 465 IS; no maximum [VITALS] or interlock rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Coder cast targeting needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, CasterZ), (X, NearZ), (X, FarZ), (SecondaryX, SecondaryZ) })
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1,
                $"({x},{Y},{z}) stands on verified outdoor NAV");
            Check.That(NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), $"Clear sight from CSR to ({x},{Y},{z})");
        }
        Check.That(NavMesh.HasSightPath(1, X, Y, NearZ, X, Y, FarZ), "The helper moves between its outdoor stands on clear NAV");

        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFar" })
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var profile = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => profile.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                profile.TryGetValue(need.Key, out var rank) && rank >= need.Value)), "All ranked prerequisites fit the level-19 stock profile");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var primary = context.Helpers["QAEnemy"];
        var secondary = context.Helpers["QAFar"];
        var view = await caster.WaitForViewNamedAsync(primary.Handle);
        await caster.WaitForViewNamedAsync(secondary.Handle);
        caster.SendRpc(StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        int[] starts = new int[Samples.Length];
        for (int i = 0; i < Samples.Length; i++)
        {
            var sample = Samples[i];
            int log = server.DebugLog().Length;
            int primaryBefore = primary.World.RpcSnapshot().Count;
            int secondaryBefore = secondary.World.RpcSnapshot().Count;
            int rpc = caster.World.RpcSnapshot().Count;
            starts[i] = rpc;
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Request, handle));
            await EvidenceRetestWire.CastBarAsync(caster, sample.Id, sample.Cast);
            await MoveAsync(FarZ);
            await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains(
                $"[ABILITY] {sample.Id} fizzles: the selected Coder target changed or left range", StringComparison.Ordinal),
                $"The live target left before {sample.Id} lands", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(caster, rpc, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            await NoHitAsync(primary, primaryBefore, sample);
            await NoHitAsync(secondary, secondaryBefore, sample);
            await MoveAsync(NearZ);
        }

        for (int i = 0; i < Samples.Length; i++)
        {
            var sample = Samples[i];
            await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(starts[i]).Any(frame => frame.Bytes.AsSpan()
                .SequenceEqual(AbilityCooldowns.BuildTimer(sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse, start: false))),
                $"{sample.Id} has finished its own reuse", TimeSpan.FromSeconds(34));
            int secondaryBefore = secondary.World.RpcSnapshot().Count;
            await StarterFixedDamageMode.Land(caster, primary, view, sample.Id, sample.Damage, sample.Cast, sample.Reuse, sample.Request);
            if (sample.Splash)
            {
                byte[] damage = AbilityMessages.Damage(false, sample.Id, caster.Handle, secondary.Handle, sample.Damage, 0);
                await StockAbilityProfile.ObserveAsync(secondary, secondaryBefore, frame => frame.Bytes.AsSpan().SequenceEqual(damage),
                    $"{sample.Id} splashes the same opposing helper with the primary in range");
            }
            else
            {
                await NoHitAsync(secondary, secondaryBefore, sample);
            }
        }

        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)450),
            "The three fizzles and three fresh casts each spend their ordinary IS", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, caster.World.Problems.Count + primary.World.Problems.Count + secondary.World.Problems.Count, "Stock range-change and cast framing");
        return Array.Empty<byte>();

        async Task MoveAsync(int z)
        {
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            byte[] movement = Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray();
            var record = BytePattern.Parse($"{StockAbilityProfile.LittleEndianHex(view.ViewId)} 01 08 {Convert.ToHexString(position)}");
            int cursor = caster.World.ViewSnapshot().Count;
            primary.World.SendViewList(movement);
            await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(cursor).Any(record.Occurs),
                $"CSR sees QAEnemy move to z={z} before the next step", HeadlessPlayer.DefaultTimeout);
        }

        async Task NoHitAsync(HeadlessPlayer target, int before, Sample sample)
        {
            target.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(target, before, frame => frame.Bytes is [0x81, 0x49, ..],
                "The recipient stream has advanced beyond the cast completion");
            byte[] prefix = AbilityMessages.Damage(false, sample.Id, caster.Handle, target.Handle, 0, 0)[..^8];
            Check.That(!target.World.RpcSnapshot().Skip(before).Any(frame => frame.Bytes.AsSpan().StartsWith(prefix)),
                $"{sample.Id} does not hit {target.Handle} on the excluded path");
        }
    }
}
