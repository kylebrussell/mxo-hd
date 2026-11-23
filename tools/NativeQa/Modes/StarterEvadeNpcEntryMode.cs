using System.Globalization;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterEvadeNpcEntryMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, StartZ = 6400, EntryZ = 6750, NpcZ = 6900;
    private const string Npc = "QA Corrupted";
    private const string StockEvade = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private static readonly ushort[] Loaded = StockAbilityProfile.Profile(2, 17, EvasionShield.Parent).Order().ToArray();

    public override string Name => "ability-parity-starter-evade-npc-entry";
    public override NativeQaModePlan Plan { get; } = new(Loaded, X, StartZ, Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={StartZ}," +
            "rotation=0,innerStrC=59,innerStrM=59 WHERE charId=110",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM inventory WHERE charId=110 AND slot IN (105,107)"
    }, Array.Empty<string>(), DevCommands: true, hotbarAbilities: new[] { EvasionShield.Parent },
        testMobFixture: "starter-evade-npc-entry.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "1", ["MXO_AGGRO_RANGE"] = "2", ["MXO_MOB_WANDER"] = "0",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "217", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loaded.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, outdoor NAV CSR ({X},{Y},{StartZ}) faces level 11 {Npc} ({X},{Y},{NpcZ}). Start outside the NPC detection band.",
            "Choose page 2 slot 1 Evade Combat at rest. Expect [MODIFIER] 217/624, a full 255 shield and 40 IS spent (59→19). Walk straight toward the NPC, then stop within 2 m near z6750. Let the NPC start each attempt.",
            "The first two NPC attempts each give the defender's own reason-14 result. Expect cost 7 and 24 shield damage: IS 19→12, shield 255→194, then IS 12→5, shield 194→133. The NPC action clock separates the attempts by at least 5 s. Neither attempt starts interlock.",
            "The next attempt reports required=7 available=5, leaves 5 IS, removes 217/624, clears the shield and starts the 30 s own reuse. That attempt is resisted too. The next ordinary aggro opens the NPC interlock."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { StartZ, EntryZ, NpcZ })
        {
            Check.That(NavMesh.Locate(1, X, Y, z, out var nav) && !nav.indoors && nav.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, StartZ, X, Y, z), $"Outdoor NAV and sight at ({X},{Y},{z})");
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        var npc = await actor.WaitForViewNamedAsync(Npc);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockEvade);
        await StockAbilityProfile.ObserveAsync(actor, cursor,
            f => StockAbilityProfile.Bonus(f, EvasionShield.Parent, EvasionShield.StateAbility, 0, 0, false),
            "Stock Evade publishes state 624 before reaching the NPC");

        var self = actor.World.CreationSnapshot().Last(v => v.ViewId == 2);
        string selfHandle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(self.ViewId, self.SpawnId)));
        byte[] response = WireFormat.FromHex($"4d 0e 01 01 00 {selfHandle}");
        int logAt = server.DebugLog().Length;
        int attackAt = actor.World.RpcSnapshot().Count;
        byte[] entry = new[] { (float)X, (float)Y, (float)EntryZ }.SelectMany(BitConverter.GetBytes).ToArray();
        actor.World.SendViewList(Convert.FromHexString("02000108").Concat(entry).Concat(new byte[] { 0, 0 }).ToArray());

        await ResistAsync(12, 194, "resistance IS 19->12 cost=7; shield 255->194 damage=24");
        await ResistAsync(5, 133, "resistance IS 12->5 cost=7; shield 194->133 damage=24");
        await StockAbilityProfile.ObserveAsync(actor, attackAt, f =>
            StockAbilityProfile.Bonus(f, EvasionShield.Parent, EvasionShield.StateAbility, 0, 0, true), "IS shortfall releases Evade");
        await EvidenceRetestWire.TimerAsync(actor, attackAt, EvasionShield.Parent, AbilityCooldowns.OwnTimer, 30);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(attackAt).Count(f => f.Bytes.AsSpan().SequenceEqual(response)) >= 3,
            "The NPC's last shield attempt still uses the defender refusal", HeadlessPlayer.DefaultTimeout);
        Check.That(!actor.World.RpcSnapshot().Skip(attackAt).Any(f => f.Opcode == 0x40),
            "No interlock starts with the three refused entries");

        string results = server.DebugLog()[logAt..];
        Check.That(results.Contains($"NPC {Npc} level=11", StringComparison.Ordinal) &&
            results.Contains("resistance shortfall required=7 available=5", StringComparison.Ordinal), "NPC level and actual remaining IS");
        double[] attempts = Regex.Matches(results, @"\[COMBAT-NPC\] action QA Corrupted at ([\d.]+)")
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Take(3).ToArray();
        Check.Equal(3, attempts.Length, "Three charged/shortfall NPC action slots");
        Check.That(attempts.Zip(attempts.Skip(1)).All(pair => pair.Second - pair.First >= MobAggro.RetrySeconds - .001),
            "The NPC action clock prevents repeated shield drains inside normal aggro cadence");

        string npcHandle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(npc.ViewId, npc.SpawnId)));
        await actor.WaitForRpcAsync($"40 {npcHandle} 00 6b");
        Check.Equal(0, actor.World.Problems.Count, "NPC shield entry framing");
        Console.WriteLine("NPC EVADE BOOTSTRAP PASS: ordinary aggro, two paid refusals, one shortfall and later normal interlock.");
        return Array.Empty<byte>();

        async Task ResistAsync(ushort strength, byte shield, string log)
        {
            var own = PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfInnerStrength, PlayerViewAttributes.U16(strength)),
                (30, PlayerViewAttributes.U8(shield)));
            await actor.WaitForViewUpdateAsync(2, WireFormat.ToHex(own[2..]));
            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains(log, StringComparison.Ordinal),
                log, HeadlessPlayer.DefaultTimeout);
        }
    }
}
