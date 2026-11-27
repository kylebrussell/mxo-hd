using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class HealInhalersMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "heal-inhalers";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=128,healthC=200,healthM=500 WHERE charId=110;
            DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 5 AND 10;
            INSERT INTO inventory(charId,goid,slot,count,purity)
            VALUES (110,39757,5,2,0),(110,39758,6,1,0),(110,39759,7,1,0),
                   (110,39760,8,1,0),(110,39761,9,1,0),(110,2399,10,1,0);
            """ }, Array.Empty<string>(), DevCommands: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), no teleport or target. Stay at rest. CSR starts at 200/500 HP. The disposable fixture holds natural regeneration so only the inhaler pulses change HP.",
            "In Inventory, activate Health Inhaler 1.0 in bag 5 via the item menu or double-click. Expect a 1 s cast, one dose left, and no heal on landing. Each tier has one 10 s [MODIFIER] record at stat 108 and heals three times at about 2.5, 5 and 7.5 s. Wait for reuse and expiry before the next dose.",
            "Bag 5: source 716, modifier value 15, [VITALS] HOT raw=4 three times, final HP 212. During reuse try bag 6 or the ordinary Health Pill in bag 10. Expect local reuse refusal or reason 5 with the item kept.",
            "After reuse, activate bags 6 through 9 in order, waiting for expiry after each. Source 717 value 30 heals 8 each to HP 236. Source 718 value 45 heals 11 each to 269. Source 719 value 60 heals 15 each to 314. Source 720 value 90 heals 23 each to 383. Bags 6 through 9 each empty on success.",
            "PacketLog for the five casts has 80 ac cc/cd/ce/cf/d0 02. Shared timer 80 bd 01 c7 02 00 00 00 20 41 01. Each effect removes naturally at expiry. No buff-bar gesture needed."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Inhaler QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal("50 200 500", await db.ScalarAsync("SELECT CONCAT(level,' ',healthC,' ',healthM) FROM characters WHERE charId=110"),
            "CSR meets the level gates and has enough missing HP for all five tiers");
        Check.Equal(6L, await db.LongAsync("SELECT SUM(count) FROM inventory WHERE charId=110 AND goid=slot+39752 AND slot BETWEEN 5 AND 9"),
            "All five tiers are in their bag slots");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc("80 c6 05 00");
        var cast = await actor.WaitForRpcAsync("80 ac cc 02");
        Check.Equal(1f, BitConverter.ToSingle(cast.Bytes, 14), "An inhaler casts in one second");
        await actor.WaitForRpcAsync("69 01 05 00 00 00 00 00 00 00");
        await actor.WaitForRpcAsync("80 bc 1d 00 ?? ?? 00 cc 02 00 00 6c 00 0f 00 00 00 00 00 00 20 41");
        await actor.WaitForRpcAsync(WireFormat.ToHex(AbilityCooldowns.BuildTimer(711,AbilityCooldowns.GroupTimer,10,true)));
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 40 d4 00");
        Check.Equal(1L, await db.LongAsync("SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid=39757"),
            "The second Stage 1 dose is retained");
        return Array.Empty<byte>();
    }
}
