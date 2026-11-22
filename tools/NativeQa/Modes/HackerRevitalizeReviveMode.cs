using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerRevitalizeReviveMode : NativeQaMode
{
    private const int Level = 24, X = 17000, Y = 495, Z = 6400;
    private const ushort Revitalize = 326, Revive = 375;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, CombatStyles.HackerStyle, Revitalize, Revive }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-hacker-revitalize-revive";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,17200,Z)},alignment=1,pvpflag=0,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET x=17200,y={Y},z={Z},alignment=1,pvpflag=0," +
                "healthC=777,healthM=777,innerStrC=155,innerStrM=333 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, Revitalize, Revive },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "0",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_HEALTH_PARITY_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "326,375", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces downed QAFriend at (17200,{Y},{Z}) on verified outdoor NAV.",
            "Choose Hacker Style on page 2 slot 1, /target QAFriend and press slot 2 Revitalize RSI. Capture the first stock request. Expect 100 IS, 4 s cast, 45 s reuse and a 326 approval offer while QAFriend remains down. Submit helper {\"actor\":\"QAFriend\",\"action\":\"approve\",\"accept\":true}, then watch QAFriend stand in place and read the verified 326 outcome.",
            "Submit helper {\"actor\":\"QAFriend\",\"action\":\"chat\",\"text\":\"?die\"} and wait until QAFriend is down again. Reselect with /target QAFriend and use page 2 slot 3 Revive RSI. Capture the first stock request. Expect 100 IS, 6 s cast, 30 s reuse and a 375 approval offer. Approve QAFriend again and watch its in-place stand. Each accepted offer restores full live maximum HP and IS, as printed by the helper response. No PvP rounds or relocation."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, 17200 })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Visible outdoor resurrection stand ({x},{Y},{Z})");

        const string name = "QAFriend";
        var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
        context.Helpers.Add(name, helper);
        await helper.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        _ = await AbilityHealthParityMode.ReadMaximaAsync(helper);
        helper.SendChat("?die");
        await helper.WaitForViewUpdateAsync(2, "01 28 ?? 40 05 00 00 10");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(parent =>
                archive.TryGetValue(parent.Key, out ushort rank) && rank >= parent.Value)), "Legal rank-24 resurrection loadout");
        await actor.EnterWorldAsync();
        await actor.WaitForViewNamedAsync(helper.Handle);
        return Array.Empty<byte>();
    }

    internal static async Task<object> ApproveAsync(HeadlessPlayer helper, byte[] reply)
    {
        ushort ability = BitConverter.ToUInt16(reply, 2);
        Check.That(ability is Revitalize or Revive, "Expected one of the two resurrection offers");
        var maxima = await AbilityHealthParityMode.ReadMaximaAsync(helper);
        int cursor = helper.World.ViewSnapshot().Count;
        string le = StockAbilityProfile.LittleEndianHex(ability);
        var motion = BytePattern.Parse($"02 00 01 28 ?? 40 00 3f {le}");
        var vitals = BytePattern.Parse($"02 00 02 80 80 80 d1 00 {StockAbilityProfile.LittleEndianHex(maxima.InnerStrength)} {StockAbilityProfile.LittleEndianHex(maxima.Health)}");
        helper.SendRpc(reply);
        await helper.WaitUntilAsync(() =>
        {
            var updates = helper.World.ViewSnapshot().Skip(cursor).ToArray();
            return updates.Any(motion.Occurs) && updates.Any(vitals.Occurs);
        }, "The approved resurrection stands in place at full live maxima", HeadlessPlayer.DefaultTimeout);
        await helper.WaitForRpcAsync($"81 67 ?? ?? ?? ?? 3e 28 00 c6 00 {le} 00 00");
        Console.WriteLine($"RESURRECTION QA {ability} {helper.Handle} hp={maxima.Health} is={maxima.InnerStrength} stand=63");
        return new { Ability = ability, helper.Handle, maxima.Health, maxima.InnerStrength, StandMotion = 63 };
    }
}
