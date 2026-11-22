using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerDoctorArbalestViralMode : NativeQaMode
{
    private const int Level = 43, X = 17000, Y = 495, Z = 6400;
    private const ushort Style = CombatStyles.HackerStyle, Control = 59, EmptyMind = 8;
    internal const int Resistance = 64;
    private const int StartingIs = 700;
    private static readonly ushort[] Hotbar = { Style, 238, 311, 312, 313 };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Hotbar.Concat(new ushort[] { 2, 17, Control }).ToArray())
        .Order().ToArray();
    private static readonly ushort[] Defender = StockAbilityProfile.Profile(2, 17, EmptyMind).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (string Handle, uint Id, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, Z + 300, 2),
        ("QAFar", 6112, X + 125, Z + 300, 2),
        ("QAFriend", 6111, X + 150, Z, 1),
        ("QAOther", 6113, X, Z + 2001, 2)
    };

    public override string Name => "ability-parity-hacker-doctor-arbalest-viral";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,Z+300)},innerStrC={StartingIs},innerStrM=1000 WHERE charId=110",
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
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "238,311,312,313", ["NativeCloseableRows"] = 4, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, fully ranked {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{Z+300}) at 3 m. QAFar ({X+125},{Y},{Z+300}) is the resistance control. QAOther ({X},{Y},{Z+2001}) is 20.01 m away, and QAFriend ({X+150},{Y},{Z}) is friendly. Outdoor NAV with no ambient NPCs.",
            "Select Hacker Style in page 2 slot 1. Without a target, on /target QAFriend, and on /target QAOther, try slots 2–5. Client-side suppression is acceptable, with reason 24 for no hostile/ally or 34 for range if sent. Expect no cast, IS, timer or damage on any refusal.",
            "Select /target QAEnemy. Cast slot 2 Harmful Code 2.0, then slot 3 Logic Blast 2.0 after the 682 shared timer ends. Slots 4 and 5 are Logic Barrage 3.0 and Logic Cannon 1.0. Retain the first full stock request for each slot. IS 700→670→662→652→634. Cast / own reuse / shared reuse: 238 = 4/8/4 (682), 311 = 2/4/4 (682), 312 = 2/4/4 (683), 313 = 8/15/15 (684). The 313 slot requires no active combat.",
            "The base damage is 33/31/40/18. With the seeded Hacker Style and loaded profile expect [PVP] hits 44/42/54/24 on the selected QAEnemy only, with no splash or interlock.",
            "After the timers clear select /target QAFar. Request QAFar action aura-resist and confirm source=8 stat=157 add +64 for 20 s. Immediately cast slots 3, 5, 4, then 2. Expect typed raw/final 42/34, 24/19, 54/44, 44/36, with the same costs/timers. Casting 311 before 313 lets 682 clear inside the Empty Mind window. Repeat any missed resisted sample on a fresh 20-second window after the helper's 60-second reuse.",
            "Finally enter an ordinary PvP interlock with QAEnemy using its right-click Close Combat, and press 313. Expect local outside-combat refusal or reason 21, with no cast/cost. The earlier outside-combat result carries the positive. [VITALS] maxima should stay fixed."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Doctor/Arbalest needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && at.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Visible outdoor NAV stand ({x},{Y},{z})");

        var server = context.ActualServer ?? throw new InvalidOperationException("Doctor/Arbalest needs a disposable server.");
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
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Complete ranked Doctor/Arbalest profile");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var views = new Dictionary<string, ViewCreation>();
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Handle);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Handle} is on the documented stand");
            views.Add(stand.Handle, view);
        }

        // This path uses only previously captured style, sibling and resistance requests.
        int styleCursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, styleCursor,
            frame => StockAbilityProfile.Bonus(frame, Style, 172, 24, 0, false), "Hacker Style contributes +24 Viral Damage");
        await StockAbilityProfile.ObserveAsync(actor, 0,
            frame => StockAbilityProfile.Bonus(frame, 95, 1031, 12, 0, false), "Loaded Transmit Virus contributes +12 linked Viral Damage");
        string stockControl = StarterHackerViralSightMode.Samples[2].Capture;
        await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, Control, stockControl,
            views["QAEnemy"], views["QAFriend"]);
        await CastControl("QAEnemy", 23, 0);
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(context.Helpers["QAFar"], Resistance);
        await CastControl("QAFar", 18, 5);
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(player => player.World.Problems.Count),
            "Doctor/Arbalest stock-control bootstrap framing");
        Console.WriteLine("DOCTOR/ARBALEST BOOTSTRAP PASS: captured Hacker Style, 59 and Empty Mind.");
        return Array.Empty<byte>();

        async Task CastControl(string target, int damage, int absorbed)
        {
            var recipient = context.Helpers[target];
            int targetCursor = recipient.World.RpcSnapshot().Count;
            int casterCursor = actor.World.RpcSnapshot().Count;
            var view = views[target];
            uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
            actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(stockControl, handle));
            await EvidenceRetestWire.CastBarAsync(actor, Control, 2);
            await EvidenceRetestWire.TimerAsync(actor, casterCursor, Control, AbilityCooldowns.OwnTimer, 4);
            await EvidenceRetestWire.TimerAsync(actor, casterCursor, AbilityCooldowns.VirusDamage1, AbilityCooldowns.GroupTimer, 4);
            byte[] expected = AbilityMessages.Damage(false, Control, actor.Handle, recipient.Handle, damage, absorbed);
            await StockAbilityProfile.ObserveAsync(recipient, targetCursor, frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                $"Captured Logic Blast control deals {damage} to {target}");
            await EvidenceRetestWire.TimerAsync(actor, casterCursor, Control, AbilityCooldowns.OwnTimer, 4, ended: true);
        }
    }
}
