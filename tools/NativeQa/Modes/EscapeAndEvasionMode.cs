using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class EscapeAndEvasionMode : NativeQaMode
{
    private const ushort Escape = 507, SneakBase = 151, Sneak = 1111, Countermeasures = 293;
    private const int Level = 25, EscapeRank = 23, SneakRank = 19;
    private const int X = 17000, Y = 495, StartZ = 7400, RunEndZ = 6400, ObserverZ = 9000, RevealZ = 7650;
    private static readonly ushort[] Loadout = WithPrerequisites(2, 17, CombatStyles.SpyStyle, SneakBase, Escape);
    private static readonly ushort[] ObserverLoadout = WithPrerequisites(2, 17, Countermeasures);
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-escape-and-evasion";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, StartZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET x={X},y={Y},z={StartZ},rotation=128,alignment=1,pvpflag=0,level={Level}," +
                $"exp={PlayerProgression.ExperienceForLevel(Level)},innerStrC=400,innerStrM=400 WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            $"UPDATE char_abilities SET level={EscapeRank} WHERE char_id=110 AND ability_id={Descriptor(Escape)}",
            $"UPDATE char_abilities SET level={SneakRank} WHERE char_id=110 AND ability_id={Descriptor(SneakBase)}",
            $"UPDATE characters SET x={X},y={Y},z={ObserverZ},rotation=128,alignment=2,pvpflag=0,level=50," +
                $"exp={PlayerProgression.ExperienceForLevel(50)},innerStrC=400,innerStrM=400 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", ObserverLoadout.Select((id, i) => $"(6114,{400 + i},{Descriptor(id)},50,1)"))
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.SpyStyle, Sneak, Escape }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "507", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory,
            ["NativeCondition"] = "Capture the first direct stock 507 request." },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. 507 rank {EscapeRank}, Sneak 151 rank {SneakRank}. " +
                $"CSR ({X},{Y},{StartZ}) faces south along the open street. QAEnemy ({X},{Y},{ObserverZ}) faces south and starts 16 m from CSR. Page 2 Spy/Sneak/Escape and Evasion.",
            "Select Spy in slot 1. At HOME use ordinary Sneak in slot 2 and capture 1111, SL 19 and pose 09. Take a two-second forward sample on the verified south lane. " +
                "Toggle ordinary Sneak off with the same slot, confirm standing, then return HOME using the console coordinates.",
            "Look for Escape and Evasion in Actions > Abilities (the shipped group is Sniper). Try page 2 slot 3 if the entry is missing or silent. " +
                "Save the complete first stock 507 request. Another ability ID or silence leaves 507 unverified.",
            "For a direct 507 expect the 2 s cast, current 20 IS fallback, no own reuse, [STEALTH] CSR_Tester 507 on (bits 2, SL 25), the Sneaking pose, " +
                "507 stat records 877 +25, 209 +25 and state 881, and own MovementScale +0.25. Take the same two-second forward sample and retain the start/stop positions and elapsed time.",
            "Press slot 3 again to release 507. Expect paired removals, standing, and MovementScale zero. Return HOME and repeat with the active Status row's Dismiss action if it is offered. " +
                "Capture the actual dismissal request or note that the client did not offer the action.",
            """For reveal, activate 507 at HOME with QAEnemy still at its start. Send helper {"actor":"QAEnemy","action":"escape-and-evasion-reveal"}. It uses stock Countermeasures 293 and approaches to (17000,495,7650), facing CSR. Expect SA 93 / SL 25, 507 off (revealed), standing, MovementScale zero and a fresh observer view. No PvP rounds."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        int midpoint = (ObserverZ + RevealZ) / 2;
        foreach (int z in new[] { StartZ, RunEndZ, ObserverZ, midpoint, RevealZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, StartZ, X, Y, z), $"Verified outdoor stand and sight ({X},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, StartZ, out var home), "HOME NAV anchor");
        Console.WriteLine($"HOME: ?tp {X} {Y} {StartZ} {home.sector:x} {home.region:x} {home.polygon:x} - 80");

        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await PvpOpponentsFixture.EnterAsync(observer);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && owned[Escape] == EscapeRank && owned[SneakBase] == SneakRank &&
            !owned.ContainsKey(Sneak) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                owned.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Legal Escape and Evasion and ordinary Sneak profile");

        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await observer.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        // The bootstrap contains only the previously captured ordinary Sneak and awareness inputs.
        int before = actor.World.RpcSnapshot().Count;
        int previousViews = observer.World.CreationSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await StockAbilityProfile.ObserveAsync(actor, before,
            frame => StockAbilityProfile.Bonus(frame, Sneak, 209, SneakRank, 0, false), "Ordinary stock Sneak adds its own rank 19");
        await RevealAsync(observer);
        await StockAbilityProfile.ObserveAsync(actor, before,
            frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Sneak, false)),
            "Ordinary Sneak terminates after a real reveal");
        await observer.WaitUntilAsync(() => observer.World.CreationSnapshot().Skip(previousViews).Any(view => view.Name == actor.Handle),
            "The observer regains the revealed view", HeadlessPlayer.DefaultTimeout);
        Check.That(context.ActualServer!.DebugLog().Contains($"QAEnemy sees {actor.Handle} (SA 93 SL {SneakRank}", StringComparison.Ordinal),
            "The positive reveal uses the actual ordinary Sneak rank");
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Stealth control packet framing");
        return Array.Empty<byte>();
    }

    private static uint Descriptor(ushort id) => 0x80000000u | ((uint)id << 10);
    private static ushort[] WithPrerequisites(params ushort[] roots) => roots
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    internal static async Task RevealAsync(HeadlessPlayer observer)
    {
        if (observer.Handle != "QAEnemy")
            throw new InvalidOperationException("Use the seeded QAEnemy observer.");

        int before = observer.World.RpcSnapshot().Count;
        observer.SendRpc(StarterStealthObserverMode.StockCountermeasures);
        await StockAbilityProfile.ObserveAsync(observer, before,
            frame => StockAbilityProfile.Bonus(frame, Countermeasures, 204, 80, 30, false), "Countermeasures adds 80 awareness");
        foreach (int z in new[] { (ObserverZ + RevealZ) / 2, RevealZ })
        {
            var coordinates = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes);
            observer.World.SendViewList(Convert.FromHexString("02000108").Concat(coordinates).Concat(new byte[] { 0, 0 }).ToArray());
            observer.SkipReceived();
            observer.SendRpc("81 48");
            await observer.WaitForRpcAsync("81 49");
        }
    }
}
