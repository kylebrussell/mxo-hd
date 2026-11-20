using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderUtilityFamilyMode : NativeQaMode
{
    private const ushort UtilityCoder = 604, Technician = 613, Master = 1108;
    private static readonly (ushort Stat, int Value)[] TechnicianStats = { (106, 100), (180, 20), (181, 20), (1009, 3) };
    private static readonly (ushort Stat, int Value)[] MasterStats = { (106, 150), (180, 20), (181, 20), (1009, 6) };
    // Complete stock Technician and Master self requests from native PacketLog 0656bb8b378a.
    private const string StockTechnician = "80b96502000000000000000000000000000000000000000000000000785bc80001000000086b0000000000";
    private const string StockMaster = "80b95404000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-starter-coder-utility-family";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, UtilityCoder, Technician, 620 },
        24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET y=495,healthC=500,healthM=500,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { UtilityCoder, Technician, Master },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "604,613,1108", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "At rest with no target, press page 2 slot 1 Utility Coder (604) and save the first full stock request. Expect 20 IS cost and 30 s own reuse. Max HP rises 500 to 550, melee and ballistic resistance both +20.",
            "Press slot 2 Utility Technician. Capture all three 604 modifier removals and new 613 adds. Max HP becomes 600 and resistance stays +20 each. Melee Defense becomes +3. If the client holds the press for reuse, wait for the slot.",
            "Use slot 3 for granted Utility Master. Max HP becomes 650 and melee defense becomes +6. Toggle it off after a few seconds and capture the return to 500 and removal of both resistances. Compare the source IDs and keep the original 604 packet."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]);
        Check.That(archive.Any(row => AbilityCatalog.IdOf(row.Descriptor) == UtilityCoder) &&
            archive.All(row => AbilityCatalog.IdOf(row.Descriptor) != Master),
            "Utility Coder is owned and Utility Master is only a grant");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Master, remove: false),
            "Utility Master is registered through its normal parent after readiness");
        await ActivateAsync(Technician, StockTechnician, TechnicianStats, 600);
        int replacement = actor.World.RpcSnapshot().Count;
        await ActivateAsync(Master, StockMaster, MasterStats, 650);
        await ModifiersAsync(Technician, TechnicianStats, replacement, remove: true);

        await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.15));
        int removed = actor.World.RpcSnapshot().Count;
        int views = actor.World.ViewSnapshot().Count;
        actor.SendRpc(StockMaster);
        await ModifiersAsync(Master, MasterStats, removed, remove: true);
        await MaximumAsync(views, 500);
        Check.Equal(0, actor.World.Problems.Count, "Utility class replay framing");
        return Array.Empty<byte>();

        async Task ActivateAsync(ushort id, string request, (ushort Stat, int Value)[] stats, ushort max)
        {
            int sent = actor.World.RpcSnapshot().Count;
            int before = actor.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await ModifiersAsync(id, stats, sent, remove: false);
            await MaximumAsync(before, max);
            await EvidenceRetestWire.TimerAsync(actor, sent, id, AbilityCooldowns.OwnTimer, 30);
        }

        async Task ModifiersAsync(ushort id, (ushort Stat, int Value)[] stats, int from, bool remove)
        {
            foreach (var (stat, value) in stats)
                await StockAbilityProfile.ObserveAsync(actor, from,
                    packet => StockAbilityProfile.Bonus(packet, id, stat, value, 0, remove),
                    $"Utility class {id} stat {stat}, remove={remove}");
        }

        Task MaximumAsync(int from, ushort max)
        {
            string health = StockAbilityProfile.LittleEndianHex(max);
            var maximumOnly = BytePattern.Parse($"02 00 02 80 80 01 {health}");
            var maximumAndClamp = BytePattern.Parse($"02 00 02 80 80 81 {health} 40 {health}");
            return actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(from)
                .Any(record => maximumOnly.Occurs(record) || maximumAndClamp.Occurs(record)),
                $"Active Utility class publishes maximum HP {max}", HeadlessPlayer.DefaultTimeout);
        }
    }
}
