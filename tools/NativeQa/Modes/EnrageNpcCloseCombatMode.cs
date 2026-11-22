using hds;
using HeadlessClient;
using static HeadlessClient.EnrageNpcCloseCombatFixture;

namespace NativeQa.Modes;

public sealed class EnrageNpcCloseCombatMode : NativeQaMode
{
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private const ushort Coder = 1008;

    public override string Name => "ability-parity-enrage-armed-npc";
    public override NativeQaModePlan Plan { get; } = new(Abilities, CasterX, CasterZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={CasterX},y={Y},z={CasterZ}," +
                $"rotation={MobAggro.YawTowards(CasterX,CasterZ,NpcX,NpcZ)},healthC=5000,healthM=5000,innerStrC=250,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET level={HelperLevel},exp={PlayerProgression.ExperienceForLevel(HelperLevel)},x={HelperX},y={Y},z={HelperZ}," +
                $"rotation={MobAggro.YawTowards(HelperX,HelperZ,NpcX,NpcZ)},healthC=5000,healthM=5000 WHERE charId=6111",
            $"UPDATE char_abilities SET level=LEAST(level,{HelperLevel}) WHERE char_id=6111",
            $"DELETE FROM char_abilities WHERE char_id=6111 AND ability_id NOT IN ({0x80000000u | (2u << 10)},{0x80000000u | (17u << 10)})"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Ability }, testMobFixture: "enrage-armed-blackwood.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "588", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full ranked prerequisite profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({CasterX},{Y},{CasterZ}) faces the only NPC, {NpcName} ({NpcX},{Y},{NpcZ}), 9 m away. QAFriend ({HelperX},{Y},{HelperZ}) is 7 m from the NPC and offset from the click line. All three stands are on clear outdoor NAV.",
            "Stay at the spawn stand. Select Coder Style (page 2 slot 1), single-click Blackwood Mushfake, then cast Enrage 1.0 (slot 2). Save the complete stock 80 b9 request. Expect a 2 s cast, [ABILITY-COST] 27 IS (250 to 223), 28 s reuse and [MODIFIER] source=588 stat=93 add value=0 duration=12s with zero direct HP damage.",
            "Leave both players in place and let the fight hold. About 3 s after landing, expect one [ENRAGE] source=588 seek for char=6111, then [AGGRO] Blackwood Mushfake pursues and attacks 6111. The armed Blackwood visibly runs to QAFriend and starts close combat. Look for the helper's 46 flag 01. CSR stays free to move and receives no opening; expect no opens fire line and no second Enrage seek during the interlock.",
            "After [MODIFIER] source=588 stat=93 remove, send helper JSON {\"actor\":\"QAFriend\",\"action\":\"stop\"}. This sends 44 00 to release the existing interlock. Expect [COMBAT] Combat ended: AttackerFled, the same helper target handle with 46 flag 00 and no new Enrage seek."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Enrage QA requires a disposable server.");
        CheckGeometry(context.RepositoryRoot ?? throw new InvalidOperationException("Enrage QA requires repository NAV."));
        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(helper.Handle, helper);
        await VerifyStartupAsync(helper, HelperLevel, HelperX, HelperZ);

        if (context.StockProbe is { } actor)
        {
            var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
                .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
            Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Abilities) &&
                Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                    archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Full ranked Enrage profile");
            await VerifyStartupAsync(actor, Level, CasterX, CasterZ);
        }
        NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        Console.WriteLine("ARMED ENRAGE NATIVE BOOTSTRAP PASS: startup only, live rifle carrier and idle helper on verified NAV.");
        return Array.Empty<byte>();

        async Task VerifyStartupAsync(HeadlessPlayer player, ushort expectedLevel, int x, int z)
        {
            await player.EnterWorldAsync();
            var self = player.World.CreationSnapshot().Last(view => view.ViewId == 2);
            Check.That(At(self, x, z) && self.Attributes["Level"][0] == expectedLevel,
                $"{player.Handle} starts at the declared stand and level");
            await FindNpcAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(server, player);
            Check.That(player.World.Problems.Count == 0 && !player.World.RpcSnapshot().Any(record => record.Opcode == 0x46),
                $"{player.Handle} is idle and startup framing is clean");
        }
    }
}
