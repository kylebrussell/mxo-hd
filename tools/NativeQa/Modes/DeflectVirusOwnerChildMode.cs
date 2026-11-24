using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public class DeflectVirusOwnerChildMode : NativeQaMode
{
    private const ushort Parent = 378, Child = 693;
    private const int Rank = 30, X = 17000, Y = 495, Z = 6400;
    private readonly int level, melee, viral;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 1006, 383, Parent).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public DeflectVirusOwnerChildMode() : this(30, 14, 28) { }

    protected DeflectVirusOwnerChildMode(int level, int melee, int viral)
    {
        this.level = level;
        this.melee = melee;
        this.viral = viral;
        Plan = new(Loadout, X, Z, Array.Empty<string>(),
            new[] { $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110" },
            new[]
            {
                $"UPDATE characters SET level={level},exp={PlayerProgression.ExperienceForLevel(level)},x={X},y={Y},z={Z}," +
                    "rotation=0,innerStrC=600,innerStrM=800 WHERE charId=110"
            }, DevCommands: true, hotbarAbilities: new[] { Parent }, environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
            }, extraQaMetadata: new() { ["ScopedStockRows"] = "693", ["NativeCloseableRows"] = level == Rank ? 0 : 1 }, readyMessages: new[]
            {
                $"Caster level {level}, loaded rank {Rank}, complete {Memory}/{AbilityMemory.ForLevel(level)} prerequisite memory. Verified outdoor NAV at ({X},{Y},{Z}), away from WinterFrost. Stay at this stand and leave selection clear.",
                $"Press Deflect Virus 1.0 on page 2 slot 1 once. Expect 2 s cast, 10 IS, parent own reuse 10 s then exclusive on, followed by owner 693 exclusive on and a separate 10 s own timer. [MODIFIER] source693 stat1009 +{melee} and stat183 +{viral}, each 5 s.",
                "At the first five-second refresh the owner gets child off then on, with one running child timer. At the next refresh the first child timer clears and restarts. The parent consumes 7 IS at each five-second refresh.",
                "After that second refresh, press page 2 slot 1 again to toggle the parent off. Expect parent off, then 693 off and matching modifier removals when the remaining pulse expires. The last child timer clears without restarting. Save the PacketLog. The disposable replay also exercises the captured 80 bb dismissal.",
                "The level-50 mode keeps the same rank-30 cast as the earlier stock sample. The two archived values cannot both come from one caster level under the shipped scales. The fixture verifies the authored caster-level model with ordinary cast and expiry."
            });
    }

    public override string Name => "ability-parity-hacker-deflect-virus-child";
    public override NativeQaModePlan Plan { get; }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "Verified outdoor Deflect Virus stand");
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 0, inside: false)}");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loaded[Parent] == Rank && Memory <= AbilityMemory.ForLevel(level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Complete ranked Deflect Virus profile");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        var child = AbilityCatalog.Find(Child)!;
        var meleeField = child.Modifiers.Single(modifier => modifier.Target == 1009);
        var viralField = child.Modifiers.Single(modifier => modifier.Target == 183);
        Check.That(AbilityValues.TryValue(meleeField, Rank, level, out int authoredMelee) && authoredMelee == melee &&
            AbilityValues.TryValue(viralField, Rank, level, out int authoredViral) && authoredViral == viral,
            "Shipped scales reproduce the expected pair from caster level");
        Check.That(!Enumerable.Range(1, byte.MaxValue).Any(candidate =>
            AbilityValues.TryValue(meleeField, Rank, candidate, out int m) && m == 37 &&
            AbilityValues.TryValue(viralField, Rank, candidate, out int v) && v == 99),
            "No single caster level under the shipped scales reproduces both historical magnitudes");

        int start = actor.World.RpcSnapshot().Count;
        actor.SendRpc(HackerCallbacksMode.StockDeflectVirus);
        await EvidenceRetestWire.CastBarAsync(actor, Parent, 2);
        await Match(start, AbilityBuffs.BuildExclusive(Parent, true), "Parent on");
        byte[] childOn = AbilityBuffs.BuildExclusive(Child, true);
        byte[] childOff = AbilityBuffs.BuildExclusive(Child, false);
        byte[] timerOn = AbilityCooldowns.BuildTimer(Child, AbilityCooldowns.OwnTimer, 10, true);
        byte[] timerOff = AbilityCooldowns.BuildTimer(Child, AbilityCooldowns.OwnTimer, 10, false);
        await Match(start, childOn, "Child on");
        await Match(start, timerOn, "Child own timer starts");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => StockAbilityProfile.Bonus(packet, Child, 1009, melee, 5, false),
            "Owner gets the five-second melee defense bonus");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => StockAbilityProfile.Bonus(packet, Child, 183, viral, 5, false),
            "Owner gets the five-second viral defense bonus");

        var removed = await Match(start, childOff, "The first child refresh removes the last exclusive record");
        await Match(actor.World.RpcSnapshot().IndexOf(removed) + 1, childOn, "New child after the first refresh");
        Check.Equal(1, actor.World.RpcSnapshot().Skip(start).Count(packet => packet.Bytes.AsSpan().SequenceEqual(timerOn)),
            "The first pulse leaves the existing child timer running");
        var cleared = await Match(start, timerOff, "The original child timer clears");
        var restarted = await Match(actor.World.RpcSnapshot().IndexOf(cleared) + 1, timerOn, "The child timer restarts");

        int dismiss = actor.World.RpcSnapshot().IndexOf(restarted) + 1;
        actor.SendRpc(HackerCallbacksMode.StockDismissDeflect);
        await Match(dismiss, AbilityBuffs.BuildExclusive(Parent, false), "Parent off after the retained stock dismissal");
        await Match(dismiss, childOff, "The final child expires");
        await StockAbilityProfile.ObserveAsync(actor, dismiss,
            packet => StockAbilityProfile.Bonus(packet, Child, 1009, melee, 5, true), "Matching final melee removal");
        await StockAbilityProfile.ObserveAsync(actor, dismiss,
            packet => StockAbilityProfile.Bonus(packet, Child, 183, viral, 5, true), "Matching final viral removal");
        await Match(dismiss, timerOff, "The final child timer clears");
        Check.Equal(2, actor.World.RpcSnapshot().Skip(start).Count(packet => packet.Bytes.AsSpan().SequenceEqual(timerOn)),
            "The dismissed aura never restarts child reuse");
        Check.Equal(0, actor.World.Problems.Count, "Deflect Virus transport framing");
        Console.WriteLine("DEFLECT VIRUS CHILD PASS: owner exclusive refresh, independent reuse, and retirement.");
        return Array.Empty<byte>();

        Task<RpcRecord> Match(int cursor, byte[] bytes, string description) => StockAbilityProfile.ObserveAsync(actor, cursor,
            packet => packet.Bytes.AsSpan().SequenceEqual(bytes), description);
    }
}

public sealed class DeflectVirusCasterLevelMode : DeflectVirusOwnerChildMode
{
    public DeflectVirusCasterLevelMode() : base(50, 23, 47) { }

    public override string Name => "ability-parity-deflect-virus-caster-level";
}
