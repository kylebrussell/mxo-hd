using HeadlessClient;
namespace NativeQa.Modes;

public abstract class MobiusCodeFixtureMode:NativeQaMode
{
    protected MobiusCodeFixtureMode(bool positive)
    {
        string branch=positive?"positive":"negative";
        Plan=new(new ushort[]{2,17,967},17000,2300,Array.Empty<string>(),Array.Empty<string>(),
            new[]{ $"UPDATE characters SET level=50,exp={hds.PlayerProgression.ExperienceForLevel(50)},healthC=2000,healthM=5000,innerStrC=1000,innerStrM=1000,y=495,alignment=1,pvpflag=1 WHERE charId=110; UPDATE characters SET healthC=5000,healthM=5000,y=495,alignment=2,pvpflag=1,x=17300,z=2300 WHERE charId=6114; DELETE FROM inventory WHERE charId IN (110,6114) AND slot IN (1,105); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,692,1,1,0),(6114,692,1,1,0);" },
            DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,Bootstrap:"mobius-ordinary-gun-helper",
            environmentOverrides:new(){["MXO_MOBIUS_QA"]="1"},
            extraQaMetadata:new(){["MobiusPartial"]=true,["MobiusCallbackBranch"]=branch,["MobiusCallbackArm"]="?combatdepth mobius "+branch+" next",["MobiusProcSeconds"]=10,["MobiusCombatRngFixed"]=false},
            readyMessages:new[]{"PARTIAL Mobius967: ?pvpflag on before incoming fire, then use Actions Mobius Code normally after ?combatdepth mobius "+branch+" next. Control affects only the next967 chance callback. QAEnemy holds ordinary rifle692; use helper ranged request against CSR_Tester BEFORE casting; keep it firing through ten-second expiry. Ordinary contest/damage RNG remain natural. No forced-entry/susceptibility/FX/general immunity claim. See docs/ABILITY-MOBIUS-FREE-ATTACK.md."});
    }
    public override NativeQaModePlan Plan {get;}
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy=await HeadlessPlayer.LoginAsync(context.Address,"QAEnemy","QAEnemy");
        context.Helpers.Add("QAEnemy",enemy);await enemy.EnterWorldAsync();
        await MobiusRifleBootstrap.EquipAsync(enemy);
        enemy.SendChat("?pvpflag on");
        return Array.Empty<byte>();
    }
}
public sealed class MobiusCodePositiveMode:MobiusCodeFixtureMode
{
    public MobiusCodePositiveMode():base(true){}
    public override string Name=>"mobius-free-attack-positive";
}
public sealed class MobiusCodeNegativeMode:MobiusCodeFixtureMode
{
    public MobiusCodeNegativeMode():base(false){}
    public override string Name=>"mobius-free-attack-negative";
}

/// <summary>Ordinary rifle equip: opcode63 has byte source/destination, while reply65 uses u16 slots.</summary>
public static class MobiusRifleBootstrap
{
    public static byte[] Request=>new byte[]{0x63,0x01,0x69};
    private static readonly byte[] Success=Convert.FromHexString("650100690000");
    private static readonly byte[] Refusal=Convert.FromHexString("650100010000");
    private static readonly BytePattern RifleInHand=BytePattern.Parse("02 00 02 80 80 80 80 08 b4 02 00 00");
    public static bool HasEquipEvidence(IEnumerable<byte[]> replies,IEnumerable<byte[]> currentViews)=>
        replies.Any(r=>r.SequenceEqual(Success))&&currentViews.Any(RifleInHand.Occurs);
    public static async Task EquipAsync(HeadlessPlayer enemy)
    {
        int rpcCursor=enemy.World.RpcSnapshot().Count,viewCursor=enemy.World.ViewSnapshot().Count;
        enemy.SendRpc(Request);
        await enemy.WaitUntilAsync(()=>
        {
            var replies=enemy.World.RpcSnapshot().Skip(rpcCursor).Select(r=>r.Bytes).ToArray();
            Check.That(!replies.Any(r=>r.SequenceEqual(Refusal)),"Ordinary QAEnemy rifle equip was refused; no ready claim");
            return HasEquipEvidence(replies,enemy.World.ViewSnapshot().Skip(viewCursor));
        },"Successful bag1->weapon105 equip AND current own03 EquippedItemID692",HeadlessPlayer.DefaultTimeout);
    }
}
