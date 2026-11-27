using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class OptimizerConsumablesMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, DummyX = 17200;

    public override string Name => "optimizer-consumables";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, 10 }, X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=64,healthC=5000,healthM=5000,innerStrC=350,innerStrM=500 WHERE charId=110;
            UPDATE char_abilities SET level=50 WHERE char_id=110;
            DELETE FROM inventory WHERE charId=110 AND slot IN (5,6,7,107);
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,47111,5,2,0),(110,47112,6,1,0),(110,47113,7,1,0);
            """ }, Array.Empty<string>(), testMobFixture: "awakened-power-shot-cap.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), no teleport. CSR is unarmed and faces the Melee Dummy (17200,495,2300) two metres east. Stay at rest for the three inventory activations.",
            "Use bag 5 Code Optimizer Inhaler. Expect 1 s cast, one inhaler left and [MODIFIER] source=1275 stat=117 add value=+10 duration=300s. Immediate repeat keeps the inhaler. Activate Hyper-Dodge from Actions > Awakened and expect [ABILITY-COST] ability=10 paid=18.",
            "Use bag 6 Code Optimizer Hypo. Expect 1 s cast, bag empty, source 1276 stat 109 +30 for 300 s, and [VITALS] maxIS 500 -> 530.",
            "Use bag 7 Overdrive Hypo. Save the first complete request (the GOB has internal ID 1276 and triggered template 1277). Expect 1 s cast, one spend, source 1277 stat 159/171/172/1034 +8 for 300 s, and source 1277 Enrage 93 for 300 s. Each item has its own 30 s timer. The Overdrive FX stays quiet at rest.",
            "Use the dummy's right-click Close Combat, strafe and reselect if needed. In the live interlock expect Overdrive FX about every 4 s (07 00 00 c6). Try stock Withdraw once. Expect the local control to be blocked or [ENRAGE] withdraw suppressed if it sends 44."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Optimizer QA requires a disposable database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path.");
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out _) && NavMesh.Locate(1, DummyX, Y, Z, out _) &&
            NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "CSR and the dummy have clear NAV stands");
        Check.Equal(4L, await db.LongAsync("SELECT SUM(count) FROM inventory WHERE charId=110 AND goid=slot+47106 AND slot BETWEEN 5 AND 7"),
            "The optimizer items are in bags 5 through 7");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var dummy = await actor.WaitForViewNamedAsync("Melee Dummy");
        actor.SendRpc("80 c6 05 00");
        await actor.WaitForRpcAsync("80 ac fb 04");
        await actor.WaitForRpcAsync("80 bc 15 00 ?? ?? 00 fb 04 00 00 75 00 0a 00 00 00 00 00 00 96 43");
        actor.SendRpc("80 b9 0a 00 02 00");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("[ABILITY-COST] char=110 ability=10 paid=18 "),
            "The Inhaler reduces Hyper-Dodge's IS cost", HeadlessPlayer.DefaultTimeout);
        actor.SendRpc("80 c6 06 00");
        await actor.WaitForRpcAsync("80 bc 15 00 ?? ?? 00 fc 04 00 00 6d 00 1e 00 00 00 00 00 00 96 43");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("[VITALS] char=110 maxIS 500 -> 530"),
            "The Hypo raises max IS to 530", HeadlessPlayer.DefaultTimeout);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        actor.SendRpc($"40 {handle} 00 6b");
        await actor.WaitForRpcAsync("47");
        actor.SendRpc("80 c6 07 00");
        await actor.WaitForRpcAsync("80 bc 11 00 ?? ?? 00 fd 04 00 00 5d 00 00 00 00 00 00 00 00 96 43");
        // The first scheduled update is four seconds out. This wait proves the interlocked cast's initializer.
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 07 00 00 c6", TimeSpan.FromSeconds(2.5));
        return Array.Empty<byte>();
    }
}
