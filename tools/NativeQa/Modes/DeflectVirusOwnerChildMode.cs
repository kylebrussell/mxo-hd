using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class DeflectVirusOwnerChildMode : NativeQaMode
{
    private const ushort Parent = 378, Child = 693;
    private const int Level = 30, X = 17000, Y = 495, Z = 6400;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 1006, 383, Parent).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-hacker-deflect-virus-child";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                "rotation=0,innerStrC=600,innerStrM=800 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Parent }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "693", ["NativeCloseableRows"] = 0 }, readyMessages: new[]
        {
            $"Level {Level}, complete {Memory}/{AbilityMemory.ForLevel(Level)} prerequisite memory. Verified outdoor NAV at ({X},{Y},{Z}), away from WinterFrost. Stay at this stand and leave selection clear.",
            "Press Deflect Virus 1.0 on page 2 slot 1 once. Expect 2 s cast, 10 IS, parent own reuse 10 s then exclusive on, followed by owner 693 exclusive on and a separate 10 s own timer. [MODIFIER] source693 stat1009 +14 and stat183 +28, each 5 s.",
            "At the first five-second refresh the owner gets child off then on, with one running child timer. At the next refresh the first child timer clears and restarts. The parent consumes 7 IS at each five-second refresh.",
            "After that second refresh, press page 2 slot 1 again to toggle the parent off. Expect parent off, then 693 off and matching modifier removals when the remaining pulse expires. The last child timer clears without restarting. Save the PacketLog. The disposable replay also exercises the captured 80 bb dismissal."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
            "Verified outdoor Deflect Virus stand");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Complete ranked Deflect Virus profile");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

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
        await StockAbilityProfile.ObserveAsync(actor, start, packet => StockAbilityProfile.Bonus(packet, Child, 1009, 14, 5, false),
            "Owner gets the five-second melee defense bonus");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => StockAbilityProfile.Bonus(packet, Child, 183, 28, 5, false),
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
