using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerPacketLossMode : NativeQaMode
{
    private const int Level = 30, X = 17000, Y = 495, Z = 6400, StartingIs = 1600;
    private const ushort Style = CombatStyles.HackerStyle, Control = 59, EmptyMind = 8;
    internal const int Resistance = 45;
    private const int StyleBonus = 18, LinkedBonus = 9;
    private static readonly ushort[] Hotbar = { Style, 947, 948 };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Control, 947, 948).Order().ToArray();
    private static readonly ushort[] Defender = StockAbilityProfile.Profile(2, 17, EmptyMind).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (string Handle, uint Id, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, Z + 300, 2),
        ("QAFar", 6112, X + 125, Z + 300, 2),
        ("QAFriend", 6111, X + 150, Z, 1),
        ("QAOther", 6113, X, Z + 2001, 2)
    };

    public override string Name => "ability-parity-hacker-packet-loss";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=30000,healthM=30000 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,Z+300)},innerStrC={StartingIs} WHERE charId=110",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Defender.Select((id, i) =>
            $"(6112,{i},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Organization} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: Hotbar, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "947,948", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full Trojan Horse profile at {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{Z+300}) on clear outdoor NAV. QAFriend ({X+150},{Y},{Z}) is friendly. QAOther ({X},{Y},{Z+2001}) is at 20.01 m. QAFar ({X+125},{Y},{Z+300}) is the resistance control.",
            "Select Hacker Style on page 2 slot 1. Try slots 2 and 3 with no target, /target QAFriend, then /target QAOther. A sent request returns 24 or 34. The native client may suppress it locally. No IS or timer should change.",
            $"Select /target QAEnemy and press slot 2 Packet Loss 1.0. Save the first full stock request. Expect 2 s cast, 20 IS (1600→1580), and 4 s own/shared 682. After reuse press slot 3 Packet Loss 2.0 and save its request. Expect 2 s cast, 24 IS (1580→1556), and 4 s own/shared 682. With +{StyleBonus} style and +{LinkedBonus} linked influence, neutral [PVP] damage is {Influenced(62)}–{Influenced(299)} and {Influenced(100)}–{Influenced(419)} to QAEnemy only.",
            $"On /target QAFar ask helper action aura-resist. Check source=8 stat=157 +{Resistance} for 20 s. Use slots 2 and 3 after shared reuse. For each [TYPED-RESISTANCE OURS] 947 or 948, confirm type2/total{Resistance}, raw in its documented damage range, and final below raw. Only the selected helper should lose HP. No [VITALS] maximum change.",
            "After each shared reset, repeat slot 2 on QAEnemy until the natural 10% Root proc, then wait for removal. Expect source=947 stat=99 add 0 for 10 s and a matching removal, with no 734. Repeat slot 3 on QAFar until the natural 15% Stun. Expect source=948 stat=102 and 734/681 10 s add/removes. Random damage and state procs stay natural. The fixed disposable roll applies only to the hit contest. If one state does not proc in the time available, report it separately."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Packet Loss needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && at.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Visible outdoor NAV stand ({x},{Y},{z})");

        var server = context.ActualServer ?? throw new InvalidOperationException("Packet Loss needs a disposable server.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Complete ranked Packet Loss profile");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Handle);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Handle} is on its documented stand");
        }

        // The first 947/948 requests are reserved for stock QA. Bootstrap replays captured Style, 59 and Empty Mind.
        int styleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, styleAt,
            frame => StockAbilityProfile.Bonus(frame, Style, 172, StyleBonus, 0, false), "Hacker Style Viral Damage");
        await StockAbilityProfile.ObserveAsync(actor, 0,
            frame => StockAbilityProfile.Bonus(frame, 95, 1031, LinkedBonus, 0, false), "Transmit Virus linked Viral Damage");
        var enemy = await actor.WaitForViewNamedAsync("QAEnemy");
        var friend = await actor.WaitForViewNamedAsync("QAFriend");
        string capture = StarterHackerViralSightMode.Samples[2].Capture;
        await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, Control, capture, enemy, friend);
        await CastControl("QAEnemy", 0);
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(context.Helpers["QAFar"], Resistance);
        await CastControl("QAFar", Resistance);

        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(player => player.World.Problems.Count),
            "Packet Loss control bootstrap framing");
        Console.WriteLine("PACKET LOSS BOOTSTRAP PASS: captured Style, Logic Blast and Empty Mind.");
        return Array.Empty<byte>();

        async Task CastControl(string target, int resistance)
        {
            var recipient = context.Helpers[target];
            int targetAt = recipient.World.RpcSnapshot().Count;
            int sourceAt = actor.World.RpcSnapshot().Count;
            var view = await actor.WaitForViewNamedAsync(target);
            uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
            actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(capture, handle));
            await EvidenceRetestWire.CastBarAsync(actor, Control, 2);
            await EvidenceRetestWire.TimerAsync(actor, sourceAt, Control, AbilityCooldowns.OwnTimer, 4);
            await EvidenceRetestWire.TimerAsync(actor, sourceAt, AbilityCooldowns.VirusDamage1, AbilityCooldowns.GroupTimer, 4);
            int raw = Influenced(17), final = TypedAbilityResistance.Amount(raw, Level, Level, resistance);
            byte[] expected = AbilityMessages.Damage(false, Control, actor.Handle, recipient.Handle, final, raw - final);
            await StockAbilityProfile.ObserveAsync(recipient, targetAt, frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                $"Captured Logic Blast control deals {final} to {target}");
            await EvidenceRetestWire.TimerAsync(actor, sourceAt, Control, AbilityCooldowns.OwnTimer, 4, ended: true);
        }
    }

    private static int Influenced(int raw) => (int)(raw * (double)(1 + (StyleBonus + LinkedBonus) / 100f));
}
