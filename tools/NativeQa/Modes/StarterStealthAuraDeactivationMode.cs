using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using static NativeQa.Modes.AwakenedStockStealthSpeedMode;

namespace NativeQa.Modes;

public sealed class StarterStealthAuraDeactivationMode : NativeQaMode
{
    private const int Level = 37, Rank = 19, X = 17000, Y = 495, CloseZ = 7200, BeltZ = 7220, SneakZ = 8000;
    private const int EnemyZ = 6410, FriendX = 17200;
    private const ushort Hacker = CombatStyles.HackerStyle, Spy = CombatStyles.SpyStyle;
    private const ushort Miasma = 61, StaticField = 89, Sneak = 1111, Belt = 1116;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Hacker, Spy, 151, 175, Miasma, StaticField }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    // Complete stock unselected Miasma from PR320 and Static Field from PR574.
    private const string MiasmaUse = "80b93d00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-starter-stealth-aura-deactivation";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, CloseZ, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110",
        "DELETE FROM inventory WHERE charId=110 AND (slot=40 OR goid=460)",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,460,40,1,0)"
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={CloseZ},rotation=128," +
            "innerStrC=1000,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
        $"UPDATE characters SET level={Level},x={X},y={Y},z={EnemyZ},rotation=0,alignment=2,pvpflag=0 WHERE charId=6114",
        $"UPDATE characters SET x={FriendX},y={Y},z={CloseZ},alignment=1,pvpflag=0 WHERE charId=6111",
        "DELETE FROM char_abilities WHERE char_id=6114"
    }, DevCommands: true, hotbarAbilities: new ushort[] { Hacker, Spy, Miasma, StaticField, Sneak },
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(40, 460, "Invisibility Belt") },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "61,89,1111,1116", ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Rank {Rank} starter abilities. Level {Level} is only for the combined {Loadout.Sum(AbilityMemory.Cost)}-point loadout. Outdoor NAV. CSR faces QAEnemy, {((CloseZ - EnemyZ) / 100.0):0.0} m south and facing CSR. QAFriend is 2 m east. Page 2 Hacker/Spy/Miasma/Static Field/Sneak/Belt.",
            "Pick Spy style. At the spawn point, try the Belt with clear selection. Expect one near-enemy line and 80 ba reason 10, before cast, IS, reuse or stealth. Then use the printed BELT STAND command (8.1 m from QAEnemy).",
            "Pick Hacker Style, start Miasma, pick Spy Style, then activate the Belt. Expect 50 IS, 3 s cast, [MODIFIER] 1116/115 -300 and 881 0 for 104 s, and [AURA] 61 off. No new Miasma pulse or 3 IS drain after activation. If time allows, let the Belt expire naturally and record the two removal records. Otherwise toggle it off.",
            "Use the printed SNEAK STAND (15.9 m away). Pick Hacker Style and start Static Field, select Spy, wait for the first 12 IS upkeep, then press Sneak at the next five-second boundary. Expect [AURA] 89 off before the 20 IS Sneak debit, with no later 694 pulse or 12 IS drain, while [MODIFIER] 1111/115 -200 is active. Use slot 5 again to end Sneak. No max [VITALS] changes or interlock [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, EnemyZ), (X, CloseZ), (X, BeltZ), (X, SneakZ), (FriendX, CloseZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, X, Y, CloseZ, x, Y, z),
                $"Verified outdoor stand and sight path ({x},{Y},{z})");
        string beltStand = Teleport(BeltZ), sneakStand = Teleport(SneakZ);
        Console.WriteLine("BELT STAND: " + beltStand);
        Console.WriteLine("SNEAK STAND: " + sneakStand);

        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id =>
            rows.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                rows.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Combined starter trees fit memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await actor.WaitForViewNamedAsync("QAEnemy");
        actor.SendRpc(SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
        int begin = actor.World.RpcSnapshot().Count;
        int logs = server.DebugLog().Length;
        actor.SendRpc(BeltUse);
        await StockAbilityProfile.ObserveAsync(actor, begin, frame => AbilityRefusals.TryDecode(frame.Bytes, out var refusal) &&
            refusal.AbilityID == Belt && refusal.ErrorID == AbilityRefusals.NoClientMessage,
            "Facing enemy at 7.9 m refuses the stock Belt without a client tactic line");
        await actor.WaitForTextAsync("You cannot become invisible near enemies.");
        Check.That(!server.DebugLog()[logs..].Contains("[ABILITY-COST] char=110 ability=1116", StringComparison.Ordinal),
            "Rejected Belt never starts the paid cast");

        await CheckAuraAsync(beltStand, Miasma, MiasmaUse, Belt, BeltUse, -300, 104);
        await CheckAuraAsync(sneakStand, StaticField, StarterPathogenistAuraSightMode.StaticUse, Sneak, SneakUse, -200, 0);
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Stealth aura cleanup framing");
        return Array.Empty<byte>();

        string Teleport(int z)
        {
            Check.That(NavMesh.Locate(1, X, Y, z, out var nav), "Find teleport anchor");
            return $"?tp {X} {Y} {z} {nav.sector:x} {nav.region:x} {nav.polygon:x} - 80";
        }

        async Task CheckAuraAsync(string stand, ushort aura, string auraUse, ushort hidden, string hideUse, int penalty, int seconds)
        {
            actor.SkipReceived();
            actor.SendChat(stand);
            await actor.WaitForTextAsync("Teleported to");
            actor.SendRpc(StarterHackerViralSightMode.StockStyle);
            await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
            int auraStart = server.DebugLog().Length;
            actor.SendRpc(auraUse);
            await actor.WaitForRpcAsync($"80 bd 05 {StockAbilityProfile.LittleEndianHex(aura)} 00 00 00 00 00 01");
            actor.SendRpc(SpyUse);
            await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");
            if (hidden == Sneak)
            {
                await actor.WaitUntilAsync(() => Regex.Matches(server.DebugLog()[auraStart..], @"\[AURA\] 89 pulse 694 ").Count >= 2,
                    "First scheduled Static Field pulse has paid upkeep", TimeSpan.FromSeconds(8));
                await Task.Delay(TimeSpan.FromSeconds(4.8));
            }
            int start = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            int activationStart = server.DebugLog().Length;
            actor.SendRpc(hideUse);
            await StockAbilityProfile.ObserveAsync(actor, start,
                frame => StockAbilityProfile.Bonus(frame, hidden, 115, penalty, seconds, false), $"Stock {hidden} activates");
            await StockAbilityProfile.ObserveAsync(actor, start,
                frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(aura, false)), $"{hidden} ends aura {aura}");
            await Task.Delay(TimeSpan.FromSeconds(5.3));
            string activation = server.DebugLog()[activationStart..];
            int offLog = activation.IndexOf($"[AURA] {aura} off", StringComparison.Ordinal);
            Check.That(offLog >= 0 && !activation[offLog..].Contains($"[AURA] {aura} pulse ", StringComparison.Ordinal),
                $"Aura {aura} has no child pulses after it switches off");
            if (hidden == Sneak)
            {
                Match debit = Regex.Match(activation, @"\[ABILITY-COST\] char=110 ability=1111 paid=20 IS=\d+->(\d+)");
                Check.That(debit.Success && offLog < debit.Index, "Sneak ends Static Field before its IS debit");
                ushort remaining = ushort.Parse(debit.Groups[1].Value);
                var strengths = AwakenedPowerShotMode.InnerStrengthUpdates(actor.World.ViewSnapshot().Skip(views));
                Check.That(strengths.Contains(remaining) && strengths.Min() == remaining,
                    "No Static Field upkeep debit after paying for Sneak");
            }
            int off = actor.World.RpcSnapshot().Count;
            actor.SendRpc(hideUse);
            await StockAbilityProfile.ObserveAsync(actor, off,
                frame => StockAbilityProfile.Bonus(frame, hidden, 115, penalty, seconds, true), $"Stock {hidden} ends");
        }
    }
}
