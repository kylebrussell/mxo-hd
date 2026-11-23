using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterCraftedPillRoundTripMode;

namespace NativeQa.Modes;

public sealed class StarterCraftingMovementMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 6400, Info = 17000;
    private const uint Boost = 39413;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 37, 277, 515).Order().ToArray();

    public override string Name => "ability-parity-starter-crafting-movement";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
        $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{CraftingSession.Tool},1,1,0),(110,{CraftingSession.WriterTool},2,1,0)",
        $"DELETE FROM char_abilities WHERE char_id=110 AND (slot=400 OR ability_id={Boost << 7})",
        $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES(110,400,{Boost << 7},1,0)",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110 AND ability_id >= 2147483648"
    }, new[]
    {
        $"UPDATE characters SET y={Y},cash={Info},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId=110"
    }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "969,970", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            $"Level {Level}, CSR ({X},{Y},{Z}) on outdoor NAV floor 400 with open space for a short step. No helpers or targets. Move the game window left of the desktop overlay. Compiler bag 1, Analyser bag 2, one Health Boost 3.0 archive charge, {Info} Info.",
            "Right-click Analyser 2 and choose Activate Item. Expect Writer, typing stance 03 and [MODIFIER] source=970 stat=968 add value=0. Turn in place: the Writer stays open. Without clicking Close, take a short step. Expect the Writer gone, standing stance 00, one matching 970/968 removal and exclusive-off. Reopen and close normally once as a control.",
            "Right-click Compiler 1 > Activate Item. Choose Health Boost 3.0 and click Compile. Once [COMPILE60] Started 39413 appears, take a short step before its 37.5 s timer ends. Expect one 61 01, stance 00, [MODIFIER] source=969 stat=968 remove and exclusive-off. Reopen to confirm the one charge and 17,000 Info remain, close normally. No pill, [VITALS] max change or [PVP] round.",
            "For both movement closes, save PacketLog from the first changed-position 03 through the stance 00 and linked-off/exclusive-off response. Note whether any client window-close RPC, tool flag0 or Writer 81 1a, arrived before or after the response."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Crafting movement needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
            NavMesh.Locate(1, X + 20, Y, Z, out var east) && NavMesh.OpenBetween(1, stand, east) &&
            NavMesh.Locate(1, X, Y, Z + 20, out var north) && NavMesh.OpenBetween(1, stand, north),
            "The crafting stand and short steps lie on the same outdoor NAV floor");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Both crafting tool grants have the shipped legal loaded prerequisites");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        // Replay only retained full stock tool frames here. The local movement regression uses the ordinary 03 handler separately.
        await VerifyAsync(CraftingSession.WriterAbility, WriterOpen, WriterClose);
        await VerifyAsync(CraftingSession.Ability, CompilerOpen, CompilerClose);
        var db = context.OwnedDatabase!;
        Check.Equal(1L, await db.LongAsync("SELECT level FROM char_abilities WHERE char_id=110 AND slot=400"), "The seeded code is untouched by both tool sessions");
        Check.Equal((long)Info, await db.LongAsync("SELECT cash FROM characters WHERE charId=110"), "Opening the two tools spends no Info");
        Check.Equal(0, actor.World.Problems.Count, "The retained stock tool frames replay cleanly");
        return Array.Empty<byte>();

        async Task VerifyAsync(ushort ability, string open, string close)
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(open);
            var added = await LinkedAsync(start, 0);
            start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(close);
            var removed = await LinkedAsync(start, 1);
            Check.Equal(added.RecordId, removed.RecordId, "Explicit close removes the same stock linked record");

            async Task<ManageBonusRecord> LinkedAsync(int cursor, byte operation)
            {
                var frame = await StockAbilityProfile.ObserveAsync(actor, cursor, r =>
                    ManageBonusRecord.TryDecode(r.Bytes, out var modifier) && modifier!.Type == AbilityBuffs.KindLinked &&
                    modifier.SourceId == ability && modifier.TargetId == CraftingSession.Attribute && modifier.AddRemoveType == operation &&
                    modifier.ValueBits == 0 && modifier.DurationBits == 0, $"Crafting linked record {ability}/{operation}");
                ManageBonusRecord.TryDecode(frame.Bytes, out var confirmed);
                return confirmed!;
            }
        }
    }
}
