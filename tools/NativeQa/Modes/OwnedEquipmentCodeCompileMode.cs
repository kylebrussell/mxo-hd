using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class OwnedEquipmentCodeCompileMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;

    public override string Name => "compile-owned-equipment-code";
    public override NativeQaModePlan Plan { get; } = new(StockAbilityProfile.Profile(2, 17, 277).Order().ToArray(), X, Z,
        Array.Empty<string>(), new[] { """
            UPDATE characters SET level=50,y=495,rotation=128,cash=500 WHERE charId=110;
            UPDATE char_abilities SET level=50 WHERE char_id=110 AND (ability_id & 0xffffffff)=2147767296;
            DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96;
            INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,1271,1,1,0);
            DELETE FROM char_abilities WHERE char_id=110 AND slot=400;
            INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
                VALUES (110,400,328320,2,0,'Redpill Special code with two owned charges');
            """ }, Array.Empty<string>(),
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] {
            "Mara Central (17000,495,2300), facing hardline 152. Info starts at 500. Open Inventory, right-click Code Compiler in bag 1 and choose Activate Item.",
            "Find Redpill Special (2565) under weapons. Compile once. Expect about 3 s, request 60 05 0a 00 00 90 01 00 01 01 and result 61 00. The archive charge count goes 2 to 1, one revolver appears in the bag and Info goes 500 to 499.",
            "Close Compiler. Right-click the booth ahead, choose Loading Area and confirm. In Equipment > Code Archive select the remaining Redpill Special code and Compile. Expect 61 00, the archive row disappears, a second revolver and Info 499 to 498. An Item field of zero in the Loading Area is valid."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Compile QA requires a disposable database.");
        string data = Path.Combine(context.RepositoryRoot ?? throw new InvalidOperationException("Missing world data path."), "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        HardlineNetwork.Load(Path.Combine(data, "hardline_network.csv"));
        var booth = HardlineNetwork.Get(1, 152) ?? throw new InvalidOperationException("Mara Central hardline is missing.");
        Check.That(booth.spotX == X && booth.spotY == Y && booth.spotZ == Z && NavMesh.Locate(1, X, Y, Z, out _),
            "CSR starts at the shipped Mara Central arrival stand on NAV");
        Check.Equal(1L, await db.LongAsync(
            "SELECT COUNT(*) FROM char_abilities WHERE char_id=110 AND slot=400 AND ability_id=328320 AND level=2 AND is_loaded=0"),
            "The ordinary Redpill Special code has two charges");
        Check.Equal(1L, await db.LongAsync(
            "SELECT COUNT(*) FROM inventory WHERE charId=110 AND slot=1 AND goid=1271 AND count=1"),
            "An owned Code Compiler is in bag 1");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        byte[] archive = actor.CharacterData[7];
        Check.That(Enumerable.Range(0, archive.Length / 6).Any(index =>
            BitConverter.ToUInt16(archive, index * 6) == 400 && BitConverter.ToUInt32(archive, index * 6 + 2) == 328322),
            "Margin publishes the exact ordinary descriptor and two direct charges");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await Scenarios.ProveOwnedEquipmentCodeAsync(actor, db);
        return Array.Empty<byte>();
    }
}
