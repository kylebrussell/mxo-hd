using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class ProxyMasterSelectedViralMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, CasterZ = 6400, CentreZ = 6600;
    private const int EnrageEdgeZ = 7100, PulseEdgeZ = 7300, OutsideZ = 7301;
    private const ushort Pulse = 595, Enrage = 597, Barrage = 598, StaticBlast = 33;
    private const ushort RunAdvancedProgram = 585, ProxyMaster = 599;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, CombatStyles.CoderStyle, Pulse, Enrage, Barrage, StaticBlast, ProxyMaster)
        .Order().ToArray();
    private static readonly ushort[] Resister = StockAbilityProfile.Profile(2, 17, 8).Order().ToArray();
    private static readonly (ushort Ability, ushort Rank)[] Samples = { (Pulse, 38), (Enrage, 41), (Barrage, 44) };
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private static readonly (string Name, int Character, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, CentreZ, 2),
        ("QAOther", 6113, X, EnrageEdgeZ, 2),
        ("QAFar", 6112, X, OutsideZ, 2),
        ("QAFriend", 6111, X + 100, CentreZ, 1)
    };

    public override string Name => "ability-parity-proxy-master-selected-viral";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, CasterZ, Array.Empty<string>(),
        Samples.Select(sample => $"UPDATE char_abilities SET level={sample.Rank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(sample.Ability)}").ToArray(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={CasterZ}," +
                $"rotation={MobAggro.YawTowards(X, CasterZ, X, CentreZ)},innerStrC=700,innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Resister.Select((id, slot) =>
                $"(6112,{slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={stand.X},y={Y},z={stand.Z}," +
            $"alignment={stand.Organization},pvpflag=0,healthC=1000,healthM=1000,innerStrC=500,innerStrM=500 WHERE charId={stand.Character}"))
        .ToArray(), DevCommands: true, hotbarAbilities: new[] { CombatStyles.CoderStyle, Pulse, Enrage, Barrage, StaticBlast },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "595,597,598", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, ranks 38/41/44 and {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{CentreZ}) at 2 m. QAOther starts at ({X},{Y},{EnrageEdgeZ}), 5.00 m from QAEnemy. QAFar ({X},{Y},{OutsideZ}) is 7.01 m from QAEnemy. In-radius QAFriend is allied at ({X + 100},{Y},{CentreZ}). All stands are clear of WinterFrost and Trainman.",
            "Select Coder Style (page 2 slot 1). With no target, then with /target QAFriend, press slots 2–4. Expect local suppression or reason 24 with no cast, debit, timer or effects. Then use /target QAEnemy. Keep that target and stay out of interlock. Save the first complete stock 80 b9 request for each tested ability.",
            "Enrage 2.0, slot 3. Expect 4 s, 43 IS (700->657), 30 s own reuse, and [MODIFIER] source=597 stat=93 add value=0 duration=10s for char=6114 and 6113. QAFar and QAFriend are excluded. Wait for both removals. No HP change.",
            $$"""Move QAOther to the verified 7.00 m edge with {"actor":"QAOther","action":"move","x":{{X}},"y":{{Y}},"z":{{PulseEdgeZ}}}. Press Stun Pulse slot 2. Expect 6 s, 29 IS (657->628), 30 s own reuse, and [FREECAST-VIRAL] ability=595 hit=True on 6114 and 6113 only. Both receive [MODIFIER] source=595 stat=102 add value=0 duration=6s and source=734 stat=681 add value=0 duration=12s, then paired removals. No HP change. QAFar is 7.01 m away and QAFriend is an ally.""",
            """Static Barrage 2.0, slot 4. Expect 2 s, 26 IS (628->602), 6 s own and 4 s group 683 reuse. Only QAEnemy loses 87 HP (1000->913). Once the own timer clears, /target QAFar and send {"actor":"QAFar","action":"aura-resist"}. After OK and [MODIFIER] char=6112 source=8 stat=157 +75 duration=20s, press slot 4. Expect 26 IS (602->576), identical timers and [TYPED-RESISTANCE OURS] 598 type2 total75 raw87 final71, only QAFar HP 1000->929. No [PVP] interlock or max [VITALS] change. Slot 5 is the Static Blast bootstrap control.""",
            "Projected DONE +2 (595, 598). 597 still needs the shared Enrage target-switch and ISBlock checks."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Concat(new[] { (X, CasterZ), (X, PulseEdgeZ) }))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) < 1, $"Verified outdoor NAV ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, CentreZ, x, Y, z) &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), $"Open sight to ({x},{Y},{z}) from caster and centre");
        }

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loaded.Keys.ToHashSet().SetEquals(Abilities) && Samples.All(sample =>
            loaded.GetValueOrDefault(sample.Ability) == sample.Rank) && loaded.GetValueOrDefault(RunAdvancedProgram) == Level &&
            loaded.GetValueOrDefault(ProxyMaster) == Level && loaded.GetValueOrDefault(CombatStyles.CoderStyle) == Level &&
            Memory <= AbilityMemory.ForLevel(Level) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.GetValueOrDefault(need.Key) >= need.Value)), "The complete ranked Proxy Master profile fits memory");

        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var stand in Stands)
        {
            var view = await caster.WaitForViewNamedAsync(stand.Name);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Name} is on the documented stand");
        }
        await StockAbilityProfile.ObserveAsync(caster, 0, frame => StockAbilityProfile.Bonus(frame, ProxyMaster, 1053, 5, 0, false),
            "Proxy Master grants its stored +5% Inner Strength cost modifier");
        await StockAbilityProfile.ObserveAsync(caster, 0, frame => StockAbilityProfile.Bonus(frame, RunAdvancedProgram, 1031, 10, 0, false),
            "Run Advanced Program contributes +10 Viral Damage Bonus");
        caster.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        var enemy = await caster.WaitForViewNamedAsync("QAEnemy");
        uint handle = DuelPackets.Handle(enemy.ViewId, enemy.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        // The exact retained Static Blast request is the only ability cast during bootstrap.
        // The first full requests of the three Proxy Master casts come from native QA.
        const string stock = "80b92100050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
        int cursor = caster.World.RpcSnapshot().Count;
        int vitals = caster.World.ViewSnapshot().Count;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(stock, handle));
        await EvidenceRetestWire.CastBarAsync(caster, StaticBlast, 2);
        await EvidenceRetestWire.TimerAsync(caster, cursor, StaticBlast, AbilityCooldowns.OwnTimer, 6);
        await EvidenceRetestWire.TimerAsync(caster, cursor, AbilityCooldowns.VirusDamage1, AbilityCooldowns.GroupTimer, 4);
        var damage = BytePattern.Parse("81 67 ?? ?? ?? ?? " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.DamageTarget)) +
            " 00 00 10 00 00 " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.AbilityGameObject(StaticBlast))));
        await StockAbilityProfile.ObserveAsync(context.Helpers["QAEnemy"], 0, rpc => damage.IsMatch(rpc.Bytes),
            "Captured Static Blast control reached the live selected target");
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(vitals)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)685),
            "Captured Static Blast leaves 685 IS", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Static Blast control framing");
        Console.WriteLine("PROXY MASTER BOOTSTRAP PASS. Exact stock Coder Style and Static Blast; stock 595/597/598 requests pending.");
        return Array.Empty<byte>();
    }
}
