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
    // PR885 pr885-PacketLog.txt lines 4152-4153. Only the live selection handle is replaced.
    private const string EnrageStock = "80b955020700050000000000000000000000000000000000000000001060c50001000000086b0000000000";
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, CombatStyles.CoderStyle, Pulse, Enrage, Barrage, StaticBlast, ProxyMaster)
        .Order().ToArray();
    private static readonly ushort[] Resister = StockAbilityProfile.Profile(2, 17, 8, CombatStyles.CoderStyle, Enrage, ProxyMaster)
        .Order().ToArray();
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
                $"rotation={MobAggro.YawTowards(X, CasterZ, X, CentreZ)},healthC=5000,healthM=5000,innerStrC=700,innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Resister.Select((id, slot) =>
                $"(6112,{slot},{AbilityMessages.AbilityGameObject(id)},{(id == Enrage ? 41 : Level)},1)"))
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
        extraQaMetadata: new() { ["ScopedStockRows"] = "595,597,598", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, ranks 38/41/44 and {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{CentreZ}) at 2 m. QAOther starts at ({X},{Y},{EnrageEdgeZ}), 5.00 m from QAEnemy. QAFar ({X},{Y},{OutsideZ}) is 7.01 m from QAEnemy. In-radius QAFriend is allied at ({X + 100},{Y},{CentreZ}). All stands are clear of WinterFrost and Trainman.",
            "Select Coder Style (page 2 slot 1). With no target, then with /target QAFriend, press slots 2–4. Expect local suppression or reason 24 with no cast, debit, timer or effects. Then use /target QAEnemy. Keep that target and stay out of interlock. Save the first complete stock 80 b9 request for each tested ability.",
            "Enrage 2.0, slot 3. Expect 4 s, 43 IS (700->657), 30 s own reuse, and [MODIFIER] source=597 stat=93 add value=0 duration=10s for char=6114 and 6113. QAFar and QAFriend are excluded. Wait for both removals. No HP change.",
            $$"""Move QAOther to the verified 7.00 m edge with {"actor":"QAOther","action":"move","x":{{X}},"y":{{Y}},"z":{{PulseEdgeZ}}}. Press Stun Pulse slot 2. Expect 6 s, 29 IS (657->628), 30 s own reuse, and [FREECAST-VIRAL] ability=595 hit=True on 6114 and 6113 only. Both receive [MODIFIER] source=595 stat=102 add value=0 duration=6s and source=734 stat=681 add value=0 duration=12s, then paired removals. No HP change. QAFar is 7.01 m away and QAFriend is an ally.""",
            """Static Barrage 2.0, slot 4. Expect 2 s, 26 IS (628->602), 6 s own and 4 s group 683 reuse. Only QAEnemy loses 87 HP (1000->913). Once the own timer clears, /target QAFar and send {"actor":"QAFar","action":"aura-resist"}. After OK and [MODIFIER] char=6112 source=8 stat=157 +75 duration=20s, press slot 4. Expect 26 IS (602->576), identical timers and [TYPED-RESISTANCE OURS] 598 type2 total75 raw87 final71, only QAFar HP 1000->929. No [PVP] interlock or max [VITALS] change. Slot 5 is the Static Blast bootstrap control.""",
            "Optional interlock retarget stage. You can skip the original three rows already passed by PR885. Use /target QAEnemy and its radial Close Combat to open ordinary PvP. Wait until the first exchange. QAEnemy is the original opponent, and QAOther starts 7 m from CSR on the same clear outdoor line. Keep CSR still, then use /target QAOther to select your alternate before arming the stage.",
            """Send {"actor":"QAFar","action":"enrage-retarget"} once. QAFar casts the captured Enrage 2.0 request while outside the fight (4 s, 43 IS, 30 s own reuse). Expect [MODIFIER] char=110 source=597 stat=93 add value=0 duration=9s. QAOther joins your existing fight immediately after that add. The helper prints ENRAGE RETARGET ARMED when QAOther is in the fight.""",
            "While Enraged, try radial Close Combat on the selected QAOther. Save the stock 40 or the local refusal if the client sends none. Selection may change but the fight must keep QAEnemy. There should be no old 46 flag 00, new 40 echo or new 46 flag 01 on CSR. If the request reaches the server, expect [PVP] Target change refused for CSR_Tester: Enraged.",
            "After [MODIFIER] char=110 source=597 stat=93 remove, repeat the same radial Close Combat on QAOther. Expect the old 46 flag 00, new 40 echo, and new 46 flag 01 for QAOther. Save the full native 40 attempts and matching server replies.",
            "Optional IS Block sample during this same fight. Before arming, choose Block under Actions > Awakened and wait for one +10 own IS round. Keep Block selected through the cast, retarget attempts and recovery. The fixture holds background IS regeneration, so compare one live Block round without the award while stat 93 is active and one +10 round after removal. Save the 42, rounds and own IS views. The server interprets the separate shipped ISBlock flag as the Block round IS award. Stock verification is pending."
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

        var resister = context.Helpers["QAFar"];
        var casterProfile = AbilityMemoryUiFixture.Decode(resister.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(casterProfile.Keys.ToHashSet().SetEquals(Resister) && casterProfile[Enrage].Level == 41 &&
            Resister.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Resister.All(id =>
                AbilityPrerequisites.Current.Closure(id).All(need => casterProfile.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The helper's legal ranked Enrage and resistance profile fits memory");

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
        Check.Equal((ushort)5000, BitConverter.ToUInt16(caster.World.CreationSnapshot().Last(view => view.ViewId == 2).Attributes["Health"]),
            "CSR starts with enough HP to hold the live retarget check through expiry");
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
        await VerifyRetargetAsync(caster, context.Helpers, server);
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Static Blast control framing");
        Console.WriteLine("PROXY MASTER BOOTSTRAP PASS. The stock Block tactic remains usable in live rounds, its +10 IS award pauses during Enrage, and award and retarget both resume after expiry.");
        return Array.Empty<byte>();
    }

    public static async Task<object> ArmRetargetAsync(HeadlessPlayer caster, IReadOnlyDictionary<string, HeadlessPlayer> helpers,
        ServerInstance server)
    {
        if (caster.Handle != "QAFar") throw new InvalidOperationException("Use QAFar to arm the Enrage retarget stage.");
        const string recipient = "CSR_Tester";
        var original = helpers["QAEnemy"];
        var alternate = helpers["QAOther"];
        string originalSubject = HandleHex(await original.WaitForViewNamedAsync(recipient));
        await original.WaitForRpcAsync($"46 {originalSubject} 01", TimeSpan.FromSeconds(3));
        var target = await caster.WaitForViewNamedAsync(recipient);
        caster.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        uint selected = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc($"81 51 {HandleHex(target)}");
        var request = StarterHackerControlTargetMode.WithHandle(EnrageStock, selected);
        int start = server.DebugLog().Length;
        int rpcCursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(request);
        await EvidenceRetestWire.CastBarAsync(caster, Enrage, 4);
        await EvidenceRetestWire.TimerAsync(caster, rpcCursor, Enrage, AbilityCooldowns.OwnTimer, 30);
        Check.That(server.DebugLog()[start..].Contains(
            $"[ABILITY-COST] char={caster.CharId} ability={Enrage} paid=43 IS=", StringComparison.Ordinal),
            "The captured helper cast pays the same 43 IS after the Proxy Master discount");
        await caster.WaitUntilAsync(() => server.DebugLog()[start..]
            .Contains("[MODIFIER] char=110 source=597 stat=93 add value=0 duration=9s", StringComparison.Ordinal),
            "Enrage 2.0 lands on the stock recipient in the original interlock", TimeSpan.FromSeconds(7));

        string alternateSubject = HandleHex(await alternate.WaitForViewNamedAsync(recipient));
        alternate.SkipReceived();
        alternate.SendRpc($"40 {alternateSubject} 01 6b");
        await alternate.WaitForRpcAsync($"46 {alternateSubject} 01");
        Console.WriteLine("ENRAGE RETARGET ARMED. Try Close Combat on QAOther before stat 93 expires.");
        return new { Ability = Enrage, Target = recipient, Request = Convert.ToHexString(request), Alternate = alternate.Handle };
    }

    private static async Task VerifyRetargetAsync(HeadlessPlayer recipient, IReadOnlyDictionary<string, HeadlessPlayer> helpers,
        ServerInstance server)
    {
        string original = HandleHex(await recipient.WaitForViewNamedAsync("QAEnemy"));
        string alternate = HandleHex(await recipient.WaitForViewNamedAsync("QAOther"));
        recipient.SendRpc($"40 {original} 01 6b");
        await recipient.WaitForRpcAsync($"46 {original} 01");
        ushort originalIs = ReadInnerStrength();
        int blockStart = recipient.World.ViewSnapshot().Count;
        await SelectTacticAsync(3, "Block");
        await WaitForInnerStrengthAsync(blockStart, (ushort)(originalIs + 10), "Block awards ten IS before Enrage");
        await SelectTacticAsync(0, "Speed");

        int logCursor = server.DebugLog().Length;
        await ArmRetargetAsync(helpers["QAFar"], helpers, server);
        ushort heldIs = ReadInnerStrength();
        int innerStrengthCursor = recipient.World.ViewSnapshot().Count;
        int blockRoundLog = server.DebugLog().Length;
        await SelectTacticAsync(3, "Block");
        recipient.SendRpc($"81 51 {alternate}");
        recipient.SendChat("?combatdepth shieldstatus");
        await recipient.WaitForTextAsync("target=QAOther");

        int rpcCursor = recipient.World.RpcSnapshot().Count;
        recipient.SendRpc($"40 {alternate} 01 6b");
        await recipient.WaitUntilAsync(() => server.DebugLog()[logCursor..]
            .Contains("[PVP] Target change refused for CSR_Tester: Enraged", StringComparison.Ordinal),
            "The Enraged recipient retains the original combat target", TimeSpan.FromSeconds(4));
        await recipient.WaitUntilAsync(() => server.DebugLog()[blockRoundLog..].Split('\n').Any(line =>
            line.StartsWith("[PVP] Round ", StringComparison.Ordinal) && !line.Contains("(opening)", StringComparison.Ordinal) &&
            line.Contains("CSR_Tester Block (", StringComparison.Ordinal)),
            "The Enraged recipient still fights normal Block rounds", TimeSpan.FromSeconds(6));
        recipient.SendChat("?combatdepth shieldstatus");
        await recipient.WaitForTextAsync("target=QAOther");
        Check.That(!recipient.World.ViewSnapshot().Skip(innerStrengthCursor).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(),
            "A live round on Block awards no Inner Strength while Enraged");
        await recipient.WaitUntilAsync(() => server.DebugLog()[logCursor..]
            .Contains("[MODIFIER] char=110 source=597 stat=93 remove", StringComparison.Ordinal),
            "Enrage 2.0 expires naturally", TimeSpan.FromSeconds(13));
        string[] swap = { $"46 {original} 00", $"40 {alternate} 00 6b", $"46 {alternate} 01" };
        var swaps = swap.Select(BytePattern.Parse).ToArray();
        Check.That(!recipient.World.RpcSnapshot().Skip(rpcCursor).Any(frame => swaps.Any(pattern => pattern.IsMatch(frame.Bytes))),
            "There is no old-target removal, retarget echo or alternate-target opening while Enraged");
        recipient.SkipReceived();
        recipient.SendRpc($"40 {alternate} 01 6b");
        foreach (string packet in swap) await recipient.WaitForRpcAsync(packet);
        await WaitForInnerStrengthAsync(innerStrengthCursor, (ushort)(heldIs + 10), "The Block round award returns after Enrage expires");

        ushort ReadInnerStrength() => recipient.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Last();

        Task WaitForInnerStrengthAsync(int cursor, ushort value, string description) =>
            recipient.WaitUntilAsync(() => recipient.World.ViewSnapshot().Skip(cursor).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Contains(value), description, TimeSpan.FromSeconds(8));

        async Task SelectTacticAsync(byte tactic, string name)
        {
            int cursor = server.DebugLog().Length;
            recipient.SendRpc($"42 {tactic:x2} 00 00 00 00 00");
            await recipient.WaitUntilAsync(() => server.DebugLog()[cursor..]
                .Contains($"[COMBAT] Tactic changed to {name} (Normal) for CSR_Tester", StringComparison.Ordinal),
                $"The stock {name} tactic request reaches the live fight", HeadlessPlayer.DefaultTimeout);
        }
    }

    private static string HandleHex(ViewCreation view) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
}
