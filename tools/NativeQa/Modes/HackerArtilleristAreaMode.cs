using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public class HackerArtilleristAreaMode : NativeQaMode
{
    private const int Level = 30, X = 17000, Y = 495, Z = 6400, Centre = 6900, Refusal = 8001, StartingIs = 600;
    internal const int Resistance = 45;
    private const ushort Style = CombatStyles.HackerStyle, EmptyMind = 8, ViralResistance = 157;
    // Stock 42 payloads from actions_combat_marketplace_abilitychangecoder.log lines 11161 and 12676.
    private const string StockSpeed = "42000000000000", StockPower = "42010000000000";
    // Complete stock use requests from actions_combat.log, lines 13247–48 and 13499–500.
    private sealed record Sample(ushort Id, string Capture, int Radius, int Cost, ushort Group, int DamageMin, int DamageMax);
    private static readonly Sample[] AllSamples =
    {
        new(336, "80b950010a00a100000000000000000000000000000000000000000050010000006b0000086b0000000000", 3, 13, 682, 345, 419),
        new(337, "80b951010a00a100000000000000000000000000000000000000000051010000006b0000086b0000000000", 5, 17, 683, 367, 449)
    };
    private static readonly ushort[] Loaded = StockAbilityProfile.Profile(2, 17, Style, 336, 337).Order().ToArray();
    private static readonly ushort[] ResistLoadout = StockAbilityProfile.Profile(2, 17, EmptyMind).Order().ToArray();
    private static readonly (string Name, uint Id, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, Centre, 2), ("QAOther", 6113, X, Refusal, 2),
        ("QAFar", 6112, X, Centre + 301, 2), ("QAFriend", 6111, X + 100, Centre, 1)
    };

    private readonly Sample[] samples;
    public override string Name => "ability-parity-hacker-artillerist-area";
    public override NativeQaModePlan Plan { get; }

    public HackerArtilleristAreaMode() : this(0) { }

    protected HackerArtilleristAreaMode(ushort ability)
    {
        samples = ability == 0 ? AllSamples : new[] { AllSamples.Single(sample => sample.Id == ability) };
        Plan = BuildPlan(ability);
    }

    private NativeQaModePlan BuildPlan(ushort ability) => new(Loaded, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,Centre)},innerStrC={StartingIs},innerStrM=1000 WHERE charId=110",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", ResistLoadout.Select((id, slot) => $"(6112,{slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Organization},healthC=5000,healthM=5000 WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: samples.Select(sample => sample.Id).Prepend(Style).ToArray(), testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = string.Join(",", samples.Select(sample => sample.Id)),
            ["NativeCloseableRows"] = ability == 0 ? 0 : 1, ["MemoryUsed"] = Loaded.Sum(AbilityMemory.Cost)
        },
        readyMessages: ability == 0 ? LegacyMessages() : DamageMessages(samples[0]));

    private static string[] LegacyMessages() => new[]
        {
            $"Level {Level}, complete Artillerist profile at {Loaded.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{Centre}) at 5 m. Friendly QAFriend is 1 m from QAEnemy. Initially hostile QAOther ({X},{Y},{Refusal}) is 16.01 m from CSR.",
            "Select Hacker Style in page 2 slot 1. Try slots 2 and 3 with no target, then on /target QAFriend and /target QAOther. If sent, the stock request is refused with 24 or 34 before cast, debit or reuse. Return to /target QAEnemy.",
            $$"""Move helpers using {"actor":"QAFar","action":"move","x":{{X}},"y":{{Y}},"z":7200} and {"actor":"QAOther","action":"move","x":{{X}},"y":{{Y}},"z":7201}. Press slot 2 (336). Expect 4 s cast, 13 IS (600→587) and 2 s own/shared 682. [PVP] damages only QAEnemy and the 3.00 m edge QAFar. QAOther at 3.01 m and the nearby ally take no damage.""",
            "Move QAFar/QAOther the same way to z7400/z7401. Keep QAEnemy selected and press slot 3 (337). Expect 4 s cast, 17 IS (587→570) and 2 s own/shared 683. [PVP] damages QAEnemy and QAFar at 5.00 m only. Neither cast changes maximum vitals or starts an interlock.",
            "Dedicated Code cuts the 14/18 base costs by 9 percent. With +30 Viral Damage from Style and the loaded profile, the current capture-calibrated model gives 345–419 for 336 and 367–449 for 337 on Speed. The historical level-50 capture reported pre-resistance damage 260/280 and 300/360.",
            """For the typed resistance check, leave QAEnemy selected with the 5 m stands. Send {"actor":"QAFar","action":"aura-resist"} and confirm source 8 stat 157 +45 for 20 s. Start 337, then click the stock Power tactic during the four-second cast. The selected target receives 440–538 before resistance. Only QAFar has [TYPED-RESISTANCE OURS] 337 type2 total45 and reduced damage. After the matching source 8 removal, switch to Speed and cast again; both targets receive an unmitigated Speed roll. Record this control separately from the historical base-damage calculation."""
        };

    private static string[] DamageMessages(Sample sample)
    {
        int edge = Centre + sample.Radius * 100;
        string tactic = sample.Id == 337
            ? "For the resisted cast only, click stock Power during the cast. Expect the traced raw subtotal to be floor(influenced × 1.2)."
            : "Keep the Speed tactic throughout.";
        return new[]
        {
            $"Legal level-{Level} Artillerist profile at {Loaded.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Stay on READY HOME facing QAEnemy at 5 m. If displaced, type the printed warp in chunks of up to 16 characters into the same chat line before Enter. No NPC selection.",
            $$"""Place QAFar at the {{sample.Radius}}.00 m edge with {"actor":"QAFar","action":"move","x":{{X}},"y":{{Y}},"z":{{edge}}}. Put QAOther 1 unit farther via the same move action, z={{edge + 1}}. Page 2 slot 1 Hacker Style, then /target QAEnemy. Splash targets: QAFar in, QAOther and QAFriend out.""",
            $"Slot 2 uses {sample.Id}, 4 s, {sample.Cost} IS, 2 s own and group {sample.Group}. With Speed and no resistance, record [ARTILLERIST-DAMAGE] base, influence=30, influenced and raw. Only QAEnemy and QAFar lose HP. Stock damage uses the archived comparison band, with the original authored-to-archived conversion still undocumented.",
            $$"""Send {"actor":"QAFar","action":"aura-resist"}. Wait for stat157 +45 on QAFar. Repeat slot 2 after reuse. {{tactic}} Only QAFar shows [TYPED-RESISTANCE OURS] with type2 total45. Compare that raw subtotal with the traced calculation and the final HP loss. After 157 expires, select Speed and repeat to verify both recipients are neutral.""",
            "The disposable fixture fixes the Viral hit contest and holds CSR's IS regeneration. Damage draws, tactic effects, resistance, geometry, costs and reuse use ordinary rules."
        };
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Artillerist area requires repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var edges = samples.SelectMany(sample => new[] { (X, Centre + sample.Radius * 100), (X, Centre + sample.Radius * 100 + 1) });
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Concat(edges).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && at.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z) && NavMesh.HasSightPath(1, X, Y, Centre, x, Y, z),
                $"Visible outdoor NAV stand ({x},{Y},{z})");

        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        var server = context.ActualServer ?? throw new InvalidOperationException("Artillerist area needs a disposable server.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(archive.Keys.ToHashSet().SetEquals(Loaded) && Loaded.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loaded.All(id => AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Complete ranked Artillerist archive");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Name);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Name} is on its documented NAV stand");
        }

        ushort[] tested = samples.Select(sample => sample.Id).ToArray();
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, tested, load: false);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, tested, load: true);
        int styleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, styleAt, frame => StockAbilityProfile.Bonus(frame, Style, 172, 18, 0, false),
            "Hacker Style adds +18 Viral Damage");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, 95, 1031, 9, 0, false),
            "Transmit Virus adds +9 linked Viral Damage");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, 346, 1031, 3, 0, false),
            "Dedicated Code adds +3 linked Viral Damage");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame => StockAbilityProfile.Bonus(frame, 346, 1053, 9, 0, false),
            "Dedicated Code adds +9% cost reduction");

        var enemy = await actor.WaitForViewNamedAsync("QAEnemy");
        var ally = await actor.WaitForViewNamedAsync("QAFriend");
        var farSelection = await actor.WaitForViewNamedAsync("QAOther");
        foreach (var sample in samples)
        {
            await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, sample.Id, sample.Capture, enemy, ally);
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Capture, Handle(farSelection)));
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame => AbilityRefusals.TryDecode(frame.Bytes, out var record) &&
                record.AbilityID == sample.Id && record.ErrorID == AbilityRefusals.TargetOutOfRange, "16.01 m refusal for " + sample.Id);
        }

        int strength = StartingIs;
        var edge = context.Helpers["QAFar"];
        var resistProfile = AbilityMemoryUiFixture.Decode(edge.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(resistProfile.Keys.ToHashSet().SetEquals(ResistLoadout) && ResistLoadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            ResistLoadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                resistProfile.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Complete ranked Empty Mind profile");
        await CastSamplesAsync("neutral", resisted: false);
        int resistAt = edge.World.RpcSnapshot().Count;
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(edge, Resistance);
        await CastSamplesAsync("protected", resisted: true);
        await SetTacticAsync("Speed", StockSpeed);
        await edge.WaitUntilAsync(() => edge.World.RpcSnapshot().Skip(resistAt).Any(frame =>
            StockAbilityProfile.Bonus(frame, EmptyMind, ViralResistance, Resistance, 20, true)),
            "Empty Mind expires before the recovery sample", TimeSpan.FromSeconds(23));
        await CastSamplesAsync("expired", resisted: false);

        await MoveAsync("QAFar", Centre + 300);
        var fizzleWitnesses = context.Helpers.Values.ToDictionary(helper => helper, helper => helper.World.RpcSnapshot().Count);
        var fizzle = samples[0];
        int fizzleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(fizzle.Capture, Handle(enemy)));
        await EvidenceRetestWire.CastBarAsync(actor, fizzle.Id, 4);
        await MoveAsync("QAEnemy", Refusal);
        await EvidenceRetestWire.TimerAsync(actor, fizzleAt, fizzle.Id, AbilityCooldowns.OwnTimer, 2, ended: true);
        var fizzleDamage = DamagePattern(fizzle.Id);
        foreach (var (helper, cursor) in fizzleWitnesses)
        {
            helper.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(helper, cursor, frame => frame.Opcode == 0x149, "Fizzle witness stays in world");
            Check.That(!helper.World.RpcSnapshot().Skip(cursor).Any(frame => fizzleDamage.IsMatch(frame.Bytes)),
                $"In-flight range loss leaves {helper.Handle} untouched");
        }

        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Artillerist framing");
        Console.WriteLine($"ARTILLERIST BOOTSTRAP PASS: exact {string.Join(",", tested)} captures, Archive, splash edges and per-recipient Empty Mind add, mitigation and expiry.");
        return Array.Empty<byte>();

        async Task CastSamplesAsync(string stage, bool resisted)
        {
            foreach (var sample in samples)
            {
                await MoveAsync("QAFar", Centre + sample.Radius * 100);
                await MoveAsync("QAOther", Centre + sample.Radius * 100 + 1);
                int castAt = actor.World.RpcSnapshot().Count;
                int isAt = actor.World.ViewSnapshot().Count;
                int logAt = server.DebugLog().Length;
                var witnesses = context.Helpers.Values.ToDictionary(helper => helper, helper => helper.World.RpcSnapshot().Count);
                actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Capture, Handle(enemy)));
                strength -= sample.Cost;
                await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(isAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                    .Contains((ushort)strength), $"{sample.Id} pays {sample.Cost}", HeadlessPlayer.DefaultTimeout);
                await EvidenceRetestWire.CastBarAsync(actor, sample.Id, 4);
                bool power = resisted && sample.Id == 337;
                if (power) await SetTacticAsync("Power", StockPower);
                await EvidenceRetestWire.TimerAsync(actor, castAt, sample.Id, AbilityCooldowns.OwnTimer, 2);
                await EvidenceRetestWire.TimerAsync(actor, castAt, sample.Group, AbilityCooldowns.GroupTimer, 2);
                var damageForAbility = DamagePattern(sample.Id);
                foreach (var (helper, cursor) in witnesses)
                {
                    if (helper.Handle is "QAEnemy" or "QAFar")
                    {
                        bool protectedEdge = resisted && helper == edge;
                        var expected = Enumerable.Range(sample.DamageMin, sample.DamageMax - sample.DamageMin + 1)
                            .Select(neutral => power ? neutral * 6 / 5 : neutral)
                            .ToDictionary(raw =>
                            {
                                int final = protectedEdge ? DamageAfterResistance(raw) : raw;
                                return Convert.ToHexString(AbilityMessages.Damage(false, sample.Id, actor.Handle, helper.Handle, final, raw - final));
                            }, raw => raw);
                        var hit = await StockAbilityProfile.ObserveAsync(helper, cursor,
                            frame => expected.ContainsKey(Convert.ToHexString(frame.Bytes)), $"{sample.Id} {stage} damage on {helper.Handle}");
                        int raw = expected[Convert.ToHexString(hit.Bytes)];
                        int final = protectedEdge ? DamageAfterResistance(raw) : raw;
                        var trace = new Regex(
                            $@"\[ARTILLERIST-DAMAGE\] ability={sample.Id} base=(\d+) influence=30 tactic={(power ? "Power" : "Speed")} influenced=(\d+) raw={raw}\b");
                        await actor.WaitUntilAsync(() => trace.Matches(server.DebugLog()[logAt..]).Cast<Match>().Any(match =>
                        {
                            int sampled = int.Parse(match.Groups[1].Value);
                            int influenced = int.Parse(match.Groups[2].Value);
                            return influenced == (int)(sampled * (double)1.3f) &&
                                raw == (power ? influenced * 6 / 5 : influenced);
                        }), "The raw hit comes from the logged base, influence and ordinary tactic", HeadlessPlayer.DefaultTimeout);
                        if (protectedEdge)
                            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains(
                                $"[TYPED-RESISTANCE OURS] {sample.Id} type2 total{Resistance} raw{raw} final{final}", StringComparison.Ordinal),
                                "The authoritative typed stage agrees with the reported resisted damage", HeadlessPlayer.DefaultTimeout);
                        Console.WriteLine($"ARTILLERIST DAMAGE {stage}: {sample.Id} {helper.Handle} {(power ? "Power" : "Speed")} raw={raw} final={final} resisted={raw - final}");
                    }
                    else
                    {
                        helper.SendRpc("81 48");
                        await StockAbilityProfile.ObserveAsync(helper, cursor, frame => frame.Opcode == 0x149, "Excluded helper stays in world");
                        Check.That(!helper.World.RpcSnapshot().Skip(cursor).Any(frame => damageForAbility.IsMatch(frame.Bytes)),
                            $"{sample.Id} excludes {helper.Handle}");
                    }
                }
                await EvidenceRetestWire.TimerAsync(actor, castAt, sample.Id, AbilityCooldowns.OwnTimer, 2, ended: true);
            }
        }

        async Task SetTacticAsync(string name, string captured)
        {
            int cursor = server.DebugLog().Length;
            actor.SendRpc(captured);
            await actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains($"Tactic changed to {name} (Normal) for {actor.Handle}", StringComparison.Ordinal),
                $"The stock 42 request changes the Artillerist tactic to {name}", HeadlessPlayer.DefaultTimeout);
        }

        async Task MoveAsync(string name, int z)
        {
            var helper = context.Helpers[name];
            var view = await actor.WaitForViewNamedAsync(name);
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = actor.World.ViewSnapshot().Count;
            helper.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(actor, cursor, view.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);

    private static int DamageAfterResistance(int raw)
    {
        // Equal-level GetDamageReductionValue armor curve with the server's zero-armor normalization.
        const int armorRange = 4 * Level + 20;
        const int zeroArmorFactor = 133 * armorRange - 66 * 20;
        const int protectedFactor = zeroArmorFactor - 66 * Resistance;
        return raw * protectedFactor / zeroArmorFactor;
    }

    private static BytePattern DamagePattern(ushort ability) => BytePattern.Parse("81 67 ?? ?? ?? ?? " +
        WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.DamageTarget)) + " 00 00 10 00 00 " +
        WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.AbilityGameObject(ability))));
}
