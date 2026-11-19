using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class OverhandSmashMode : NativeQaMode
{
    private const ushort AbilityId = 145;
    public override string Name => "ability-parity-starter-overhand-smash";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 132, AbilityId }, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=19,exp={PlayerProgression.ExperienceForLevel(19)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=15 WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "At level 19 with the rank-15 loadout, select Melee Dummy, enter unarmed Close Combat, switch to Karate and land Overhand Smash. Save the ordinary stock request, DoubleOverheadSmash playback, and landed damage (7 in the disposable fixture).",
            "Capture the target Melee Accuracy and Melee Defense reductions (-10 each for 10 seconds). Record Enraged if it occurs; the stored chance is 10%."
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "145", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        await caster.EnterWorldAsync();
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        await Open(caster, dummy);
        Use(caster, dummy, 132);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 84 00 00 00");
        var landed = new Regex($@"\[ABILITY\] 145 lands on view {dummy.ViewId} for (\d+)\b");
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Use(caster, dummy, AbilityId);
            try
            {
                await caster.WaitUntilAsync(() => landed.IsMatch(server.DebugLog()), "Overhand Smash lands in normal Karate interlock", TimeSpan.FromSeconds(13));
                break;
            }
            catch (TimeoutException) when (attempt < 3) { }
        }
        var log = server.DebugLog();
        Check.That(landed.IsMatch(log), "Overhand Smash landed");
        var damage = int.Parse(landed.Match(log).Groups[1].Value);
        Check.Equal(7, damage, "Landed Overhand Smash uses its shipped physical base in this Karate fixture");
        Check.That(log.Contains("(DoubleOverheadSmash)", StringComparison.Ordinal),
            "Landed Overhand Smash retains its existing named interlock animation");
        Check.That(log.Contains("[EFFECTS] 145 on Melee Dummy: Stat 522 -10 10 s, Stat 1009 -10 10 s", StringComparison.Ordinal),
            "Landed Overhand Smash retains both ten-second target penalties");
        Console.WriteLine($"  Overhand Smash landed for {damage}, retaining its animation and both target penalties");
        return Array.Empty<byte>();
    }
    private static string Handle(ViewCreation target) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));

    private static async Task Open(HeadlessPlayer caster, ViewCreation target)
    {
        string handle = Handle(target);
        caster.SkipReceived();
        caster.World.SendRpcs(
            WireFormat.FromHex($"80 b9 11 00 {handle} 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 34 09 7f 00 00 00 00 ff 08 6b 00 00 00 00 00"),
            WireFormat.FromHex($"40 {handle} 01 6b"));
        await caster.WaitForRpcAsync("4e 01");
        await caster.WaitForRpcAsync($"40 {handle} 00 6b");
        await caster.WaitForRpcAsync("47");
    }

    private static void Use(HeadlessPlayer caster, ViewCreation target, ushort id) =>
        caster.SendRpc(new AbilityUseRecord(id, DuelPackets.Handle(target.ViewId, target.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());

}
