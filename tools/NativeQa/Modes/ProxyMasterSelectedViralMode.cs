using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class ProxyMasterSelectedViralMode : NativeQaMode
{
    private const int Level = 50, X = 17000, Y = 495, CasterZ = 6400, CentreZ = 6600;
    private const int EnrageEdgeZ = 7100, PulseEdgeZ = 7300, OutsideZ = 7301;
    private const int RetargetX = 17250, RetargetZ = 6450;
    private const string Recipient = "CSR_Tester";
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
            [AreaTargetDiagnostics.Variable] = "1", [EnrageRetargetQa.Mode] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "595,597,598", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, ranks 38/41/44 and {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{CentreZ}) at 2 m. QAOther starts at ({X},{Y},{EnrageEdgeZ}), 5.00 m from QAEnemy. QAFar ({X},{Y},{OutsideZ}) is 7.01 m from QAEnemy. In-radius QAFriend is allied at ({X + 100},{Y},{CentreZ}). All stands are clear of WinterFrost and Trainman.",
            "Select Coder Style (page 2 slot 1). With no target, then with /target QAFriend, press slots 2–4. Expect local suppression or reason 24 with no cast, debit, timer or effects. Then use /target QAEnemy. Keep that target and stay out of interlock. Save the first complete stock 80 b9 request for each tested ability.",
            "Enrage 2.0, slot 3. Expect 4 s, 43 IS (700->657), 30 s own reuse, and [MODIFIER] source=597 stat=93 add value=0 duration=10s for char=6114 and 6113. QAFar and QAFriend are excluded. Wait for both removals. No HP change.",
            $$"""Move QAOther to the verified 7.00 m edge with {"actor":"QAOther","action":"move","x":{{X}},"y":{{Y}},"z":{{PulseEdgeZ}}}. Press Stun Pulse slot 2. Expect 6 s, 29 IS (657->628), 30 s own reuse, and [FREECAST-VIRAL] ability=595 hit=True on 6114 and 6113 only. Both receive [MODIFIER] source=595 stat=102 add value=0 duration=6s and source=734 stat=681 add value=0 duration=12s, then paired removals. No HP change. QAFar is 7.01 m away and QAFriend is an ally.""",
            """Static Barrage 2.0, slot 4. Expect 2 s, 26 IS (628->602), 6 s own and 4 s group 683 reuse. Only QAEnemy loses 87 HP (1000->913). Once the own timer clears, /target QAFar and send {"actor":"QAFar","action":"aura-resist"}. After OK and [MODIFIER] char=6112 source=8 stat=157 +75 duration=20s, press slot 4. Expect 26 IS (602->576), identical timers and [TYPED-RESISTANCE OURS] 598 type2 total75 raw87 final71, only QAFar HP 1000->929. No [PVP] interlock or max [VITALS] change. Slot 5 is the Static Blast bootstrap control.""",
            "PR883 retest: skip the earlier casts and the already-passed natural nine-second Block sample. Open stock Close Combat on QAEnemy and choose Block to keep the fight stable.",
            $$"""Send {"actor":"QAOther","action":"enrage-retarget-prepare"}. QAOther moves to verified outdoor NAV ({{RetargetX}},{{Y}},{{RetargetZ}}), about 2.55 m to CSR's side, and joins the existing fight. The helper prints RETARGET PREPARED. Repeating preparation in that fight keeps the same alternate instead of opening another fight.""",
            "With no Enrage yet, /target QAOther and find a Close Combat input that sends 40. Try the target radial, then the stock Close Combat action on the combat tray if the radial offers only Details. Confirm the old 46 flag 00, new 40 echo and new 46 flag 01. Use the same control to switch back to QAEnemy. Selection alone is separate from the interlock target.",
            $$"""Preselect QAOther again, then type ?combatdepth {{EnrageRetargetQa.Command}}. This is a disclosed disposable hold, with no cast or debit. After RETARGET HOLD ACTIVE, the actual Enraged state 93 lasts {{EnrageRetargetQa.HoldSeconds}} s. Use the same proven stock Close Combat control on QAOther. Expect an incoming 40 and [PVP] Target change refused for CSR_Tester: Enraged, with no target swap or echo.""",
            "After [MODIFIER] char=110 source=597 stat=93 remove, repeat the same control on QAOther. Expect the old 46 flag 00, new 40 echo, and new 46 flag 01 for QAOther. Save all three stock 40 samples and the replies. If the calm dry run cannot send 40, record the UI behavior with the hold still off.",
            """The original helper action {"actor":"QAFar","action":"enrage-retarget"} still uses the captured 597 cast, 4 s/43 IS/30 s reuse and the natural nine-second state on CSR with Deflect Code. Its Block +10/0/+10 sample passed natively at 6c57075e."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Concat(new[] { (X, CasterZ), (X, PulseEdgeZ), (RetargetX, RetargetZ) }))
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
        Console.WriteLine("PROXY MASTER BOOTSTRAP PASS. Natural nine-second Enrage still suspends Block IS. The staged alternate switches normally, is blocked during the disclosed hold, and switches again after that hold expires.");
        return Array.Empty<byte>();
    }

    public static async Task<object> ArmRetargetAsync(HeadlessPlayer caster, IReadOnlyDictionary<string, HeadlessPlayer> helpers,
        ServerInstance server)
    {
        if (caster.Handle != "QAFar") throw new InvalidOperationException("Use QAFar to arm the Enrage retarget stage.");
        var alternate = helpers["QAOther"];
        await RequireOriginalFightAsync(helpers["QAEnemy"]);
        var target = await caster.WaitForViewNamedAsync(Recipient);
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

        await JoinAlternateAsync(alternate);
        Console.WriteLine("ENRAGE RETARGET ARMED. Try Close Combat on QAOther before stat 93 expires.");
        return new { Ability = Enrage, Target = Recipient, Request = Convert.ToHexString(request), Alternate = alternate.Handle };
    }

    public static async Task PrepareRetargetAsync(HeadlessPlayer alternate, IReadOnlyDictionary<string, HeadlessPlayer> helpers)
    {
        if (alternate.Handle != "QAOther") throw new InvalidOperationException("Use QAOther to prepare the retarget stage.");
        var original = helpers["QAEnemy"];
        await RequireOriginalFightAsync(original);
        string handle = HandleHex(await alternate.WaitForViewNamedAsync(Recipient));
        if (!HasActiveTarget(alternate, handle))
        {
            var observed = await original.WaitForViewNamedAsync(alternate.Handle);
            byte[] coordinates = new[] { (float)RetargetX, Y, RetargetZ }.SelectMany(BitConverter.GetBytes).ToArray();
            alternate.World.SendViewList(WireFormat.FromHex("02 00 01 08").Concat(coordinates).Concat(new byte[] { 0, 0 }).ToArray());
            await original.WaitForViewUpdateAsync(observed.ViewId, $"01 08 {WireFormat.ToHex(coordinates)}");
        }
        await JoinAlternateAsync(alternate);
        Console.WriteLine($"RETARGET PREPARED. QAOther is in the fight. Side stand ({RetargetX},{Y},{RetargetZ}). Try the calm control first.");
    }

    private static async Task RequireOriginalFightAsync(HeadlessPlayer original)
    {
        string handle = HandleHex(await original.WaitForViewNamedAsync(Recipient));
        await original.WaitUntilAsync(() => HasActiveTarget(original, handle),
            "QAEnemy is still interlocked with CSR", TimeSpan.FromSeconds(3));
    }

    private static async Task JoinAlternateAsync(HeadlessPlayer alternate)
    {
        string handle = HandleHex(await alternate.WaitForViewNamedAsync(Recipient));
        if (HasActiveTarget(alternate, handle)) return;
        alternate.SendRpc($"40 {handle} 01 6b");
        await alternate.WaitUntilAsync(() => HasActiveTarget(alternate, handle),
            "QAOther joins CSR's existing interlock", HeadlessPlayer.DefaultTimeout);
    }

    private static bool HasActiveTarget(HeadlessPlayer helper, string handle)
    {
        byte[] opened = WireFormat.FromHex($"46 {handle} 01");
        byte[] closed = WireFormat.FromHex($"46 {handle} 00");
        var flag = helper.World.RpcSnapshot().LastOrDefault(frame =>
            frame.Bytes.AsSpan().SequenceEqual(opened) || frame.Bytes.AsSpan().SequenceEqual(closed));
        return flag?.Bytes[^1] == 1;
    }

    private static async Task VerifyRetargetAsync(HeadlessPlayer recipient, IReadOnlyDictionary<string, HeadlessPlayer> helpers,
        ServerInstance server)
    {
        string original = HandleHex(await recipient.WaitForViewNamedAsync("QAEnemy"));
        string alternate = HandleHex(await recipient.WaitForViewNamedAsync("QAOther"));
        recipient.SendRpc($"40 {original} 01 6b");
        await recipient.WaitForRpcAsync($"46 {original} 01");
        await PrepareRetargetAsync(helpers["QAOther"], helpers);
        await PrepareRetargetAsync(helpers["QAOther"], helpers);
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

        recipient.SendRpc($"40 {original} 01 6b");
        await recipient.WaitForRpcAsync($"46 {alternate} 00");
        await recipient.WaitForRpcAsync($"40 {original} 00 6b");
        await recipient.WaitForRpcAsync($"46 {original} 01");
        await VerifyLongHoldAsync(recipient, server, alternate, swap, swaps);

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

    private static async Task VerifyLongHoldAsync(HeadlessPlayer recipient, ServerInstance server, string alternate,
        string[] swap, BytePattern[] swaps)
    {
        int logCursor = server.DebugLog().Length;
        int rpcCursor = recipient.World.RpcSnapshot().Count;
        recipient.SendChat($"?combatdepth {EnrageRetargetQa.Command}");
        await recipient.WaitForTextAsync($"RETARGET HOLD ACTIVE for {EnrageRetargetQa.HoldSeconds} seconds.");
        await recipient.WaitUntilAsync(() => server.DebugLog()[logCursor..].Contains(
            $"[MODIFIER] char=110 source=597 stat=93 add value=0 duration={EnrageRetargetQa.HoldSeconds}s", StringComparison.Ordinal),
            "The disclosed fixture hold uses the true Enrage state", HeadlessPlayer.DefaultTimeout);

        recipient.SendRpc($"40 {alternate} 01 6b");
        await recipient.WaitUntilAsync(() => server.DebugLog()[logCursor..].Contains(
            "[PVP] Target change refused for CSR_Tester: Enraged", StringComparison.Ordinal),
            "The real retarget gate also denies the request during the longer test hold", TimeSpan.FromSeconds(5));
        await recipient.WaitUntilAsync(() => server.DebugLog()[logCursor..].Contains(
            $"[MODIFIER] char=110 source=597 stat=93 remove value=0 duration={EnrageRetargetQa.HoldSeconds}s", StringComparison.Ordinal),
            "The fixture hold expires naturally", TimeSpan.FromSeconds(EnrageRetargetQa.HoldSeconds + 5));
        Check.That(!recipient.World.RpcSnapshot().Skip(rpcCursor).Any(frame => swaps.Any(pattern => pattern.IsMatch(frame.Bytes))),
            "The fixture hold does not send a target swap or echo");
        recipient.SkipReceived();
        recipient.SendRpc($"40 {alternate} 01 6b");
        foreach (string packet in swap) await recipient.WaitForRpcAsync(packet);
    }

    private static string HandleHex(ViewCreation view) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
}
