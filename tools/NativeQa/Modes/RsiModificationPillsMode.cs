using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class RsiModificationPillsMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "rsi-modification-pills";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET y=495,rotation=128 WHERE charId=110;
            UPDATE rsivalues SET sex=0,hat=0,hair=0 WHERE charid=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6,97);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,46954,5,1,0),(110,46758,6,1,0);
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), no teleport. Start out of combat. Open Inventory and note CSR's hairstyle in the preview. Keep the game window clear of the desktop overlay.",
            "Try the female hairstyle pill in bag 6 using the item's right-click Use/Activate Item. Confirm if prompted. Stock may refuse locally. If it sends a request, expect visible incompatible-item chat, no cast, no change, and bag 6 stays occupied.",
            "Use the male hairstyle pill in bag 5 from the same inventory menu. Confirm if prompted. Expect a 3 s cast (80 ac dd 04), then bag 5 disappears (5f 05 00 01), visible RSI-updated chat and a new hairstyle without any clothing change.",
            "Log out normally and relog to the same fixture. The new hairstyle should persist, bag 5 should stay empty, and the unused bag 6 pill should remain.",
            "Logs: 80 c6 05 00 or 80 b9 dd 04 with item flag and bag 5, [RSI PILL] Applied char=110 item=46954 slot=5 remaining=0, and an RSI-only self view update. Hair descriptor 105 -> 107."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("RSI QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _), "Mara Central stand is on NAV");
        Check.Equal("0 0 0", await db.ScalarAsync("SELECT CONCAT(sex,' ',hat,' ',hair) FROM rsivalues WHERE charid=110"),
            "Male base hairstyle is visible");
        Check.Equal(2L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND " +
            "((slot=5 AND goid=46954) OR (slot=6 AND goid=46758)) AND count=1"), "Both expected items are in their bag slots");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        actor.SendRpc("80 c6 06 00");
        await actor.WaitForTextAsync("not compatible with your character");
        actor.SendRpc("80 c6 05 00");
        var cast = await actor.WaitForRpcAsync("80 ac dd 04");
        Check.Equal(3f, BitConverter.ToSingle(cast.Bytes, 14), "Shipped RSI ability casts for three seconds");
        await actor.WaitForRpcAsync("5f 05 00 01");
        await actor.WaitForTextAsync("Your RSI has been updated.");
        Check.Equal(2L, await db.LongAsync("SELECT hair FROM rsivalues WHERE charid=110"),
            "Male hairstyle descriptor 107 resolves to ordinal 2");
        Check.Equal(1L, await db.LongAsync("SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=6 AND goid=46758"),
            "The incompatible pill remains");
        return Array.Empty<byte>();
    }
}
