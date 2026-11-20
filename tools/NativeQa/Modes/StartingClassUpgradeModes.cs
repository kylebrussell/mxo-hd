using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public abstract class StartingClassUpgradeMode : NativeQaMode
{
    protected sealed record Upgrade(ushort Parent, ushort Ability, string Label, string Request);
    private const int Level = 19;
    protected abstract IReadOnlyList<Upgrade> Samples { get; }

    public override NativeQaModePlan Plan => new(
        Samples.SelectMany(sample => AbilityPrerequisites.Current.Closure(sample.Ability).Keys.Append(sample.Parent))
            .Concat(new ushort[] { 2, 17 }).Distinct().Order().ToArray(),
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={CombatFixtureLoadout.GroundY} WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110"
        },
        DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            $"Verify these granted actions at level 19: {string.Join(", ", Samples.Select(sample => sample.Label))}. Stay out of interlock.",
            Samples.Any(sample => sample.Ability == 1060)
                ? "Select Melee Dummy for Operative Upgrade and clear the target for the other upgrades. Add the granted actions to the hotbar."
                : "Clear selection and add the granted upgrade to the hotbar.",
            "For each, activate, click its active slot at +4 s to switch it off, click inactive at +6 s and check that reuse holds. Click again after the original 30 s. Keep the activation timer, on/off and modifier records."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var actor = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]);
        var loadout = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]);
        Check.That(loadout.Sum(row => AbilityMemory.Cost(AbilityCatalog.IdOf(row.Descriptor))) <= AbilityMemory.ForLevel(Level),
            "The level-19 class upgrade profile fits memory");
        Check.That(Samples.All(sample => !archive.Any(row => AbilityCatalog.IdOf(row.Descriptor) == sample.Ability)),
            "All tested upgrades are granted, with no owned child rows");

        await actor.EnterWorldAsync();
        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        var proof = new List<object>();
        var activations = new Dictionary<ushort, byte[]>();
        foreach (var sample in Samples)
        {
            ushort id = sample.Ability;
            await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(id, false), $"{sample.Label} granted at readiness");
            byte[] use = Convert.FromHexString(sample.Request);
            if (BinaryPrimitives.ReadUInt32LittleEndian(use.AsSpan(4)) != 0)
                BinaryPrimitives.WriteUInt32LittleEndian(use.AsSpan(4), DuelPackets.Handle(dummy.ViewId, dummy.SpawnId));
            activations.Add(id, use);

            byte[] timer = AbilityCooldowns.BuildTimer(id, AbilityCooldowns.OwnTimer, 30, true);
            byte[] on = AbilityBuffs.BuildExclusive(id, true);
            byte[] off = AbilityBuffs.BuildExclusive(id, false);
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(use);
            var reuse = await Observe(actor, start, timer, $"{sample.Label} starts 30-second reuse");
            var enabled = await Observe(actor, start, on, $"{sample.Label} switches on");
            var received = actor.World.RpcSnapshot().Skip(start).ToList();
            Check.That(received.IndexOf(reuse) < received.IndexOf(enabled), $"{sample.Label} timer precedes the on record");
            var modifiers = AbilityCatalog.Find(id).Modifiers.Where(modifier => modifier.Target != 0 &&
                modifier.Number("WhenEnabled", -1) == 2).ToArray();
            foreach (var modifier in modifiers)
                await StockAbilityProfile.ObserveAsync(actor, start, packet => StockAbilityProfile.Bonus(
                    packet, id, modifier.Target, (int)modifier.Number("MinValue"), 0, remove: false),
                    $"{sample.Label} adds shipped stat {modifier.Target}");
            var added = BonusRecords(actor, start, id, remove: false);
            Check.Equal(modifiers.Length, added.Length, $"{sample.Label} applies only the declared modifier adds");

            await Task.Delay(TimeSpan.FromSeconds(1));
            int stop = actor.World.RpcSnapshot().Count;
            actor.SendRpc(use);
            var disabled = await Observe(actor, stop, off, $"{sample.Label} switches off during reuse");
            await actor.WaitUntilAsync(() => BonusRecords(actor, stop, id, remove: true).Length == modifiers.Length,
                $"{sample.Label} modifier removals", HeadlessPlayer.DefaultTimeout);
            var removed = BonusRecords(actor, stop, id, remove: true);
            Check.That(added.Select(record => record.Locator).Order().SequenceEqual(removed.Select(record => record.Locator).Order()),
                "The same active modifiers are removed");

            await Task.Delay(TimeSpan.FromSeconds(1));
            int retry = actor.World.RpcSnapshot().Count;
            actor.SendRpc(use);
            var rejection = await StockAbilityProfile.ObserveAsync(actor, retry, packet => packet.Bytes.AsSpan().StartsWith(
                new byte[] { 0x80, 0xba, (byte)id, (byte)(id >> 8), 0, 0, 0x6b, 0x05 }),
                $"{sample.Label} inactive reactivation is refused during reuse");
            Check.Equal(1, actor.World.RpcSnapshot().Skip(start).Count(packet => packet.Bytes.AsSpan().SequenceEqual(timer)),
                "Off and refused inactive use keep the original timer");
            proof.Add(new { id, Request = Convert.ToHexString(use), Reuse = reuse.Hex, On = enabled.Hex, Off = disabled.Hex, Denied = rejection.Hex });
        }

        foreach (var sample in Samples)
        {
            ushort id = sample.Ability;
            byte[] expiry = AbilityCooldowns.BuildTimer(id, AbilityCooldowns.OwnTimer, 30, false);
            var expired = await actor.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(expiry),
                $"{sample.Label} original timer expires", TimeSpan.FromSeconds(34));
            int restart = actor.World.RpcSnapshot().Count;
            actor.SendRpc(activations[id]);
            var restarted = await Observe(actor, restart, AbilityBuffs.BuildExclusive(id, true),
                $"{sample.Label} can activate again after the original expiry");
            proof.Add(new { id, Expiry = expired.Hex, Reactivated = restarted.Hex });
        }
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "starting-class-upgrades.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();
    }

    private static Task<RpcRecord> Observe(HeadlessPlayer actor, int cursor, byte[] bytes, string description) =>
        StockAbilityProfile.ObserveAsync(actor, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(bytes), description);

    private static ManageBonusRecord[] BonusRecords(HeadlessPlayer actor, int cursor, ushort ability, bool remove) =>
        actor.World.RpcSnapshot().Skip(cursor)
            .Select(packet => ManageBonusRecord.TryDecode(packet.Bytes, out var record) ? record : null)
            .OfType<ManageBonusRecord>()
            .Where(record => record.SourceId == ability && record.Type == 0x15 && record.AddRemoveType == (remove ? 1 : 0)).ToArray();
}

public sealed class StartingOperativeAndMartialUpgradeMode : StartingClassUpgradeMode
{
    public override string Name => "ability-parity-starting-operative-martial-toggles";
    protected override IReadOnlyList<Upgrade> Samples { get; } = new Upgrade[]
    {
        // CSR stock PacketLogs 606366a42641:1470, 0291e4b5eae0:1880 and 074ab3d3d6fe:5015.
        new(143, 1060, "Operative Upgrade", "80b92404040002000000000000000000000000000000000000000000d557377c20000000086b0000000000"),
        new(154, 1061, "Soldier Upgrade", "80b92504000000000000000000000000000000000000000000000000d557377c20000000086b0000000000"),
        new(137, 1062, "Martial Arts Initiate Upgrade", "80b92604000000000000000000000000000000000000000000000000785cc20001000000086b0000000000")
    };
}

public sealed class StartingNetworkHackerUpgradeMode : StartingClassUpgradeMode
{
    public override string Name => "ability-parity-starting-network-hacker-toggle";
    protected override IReadOnlyList<Upgrade> Samples { get; } = new Upgrade[]
    {
        // CSR stock PacketLog c59bfd3cc0aa:1594.
        new(64, 1078, "Network Hacker Upgrade", "80b93604000000000000000000000000000000000000000000000000d557377c20000000086b0000000000")
    };
}
