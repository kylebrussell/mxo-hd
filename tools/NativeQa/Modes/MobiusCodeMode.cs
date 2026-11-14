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
            readyMessages:new[]{"PARTIAL Mobius967: use Actions Mobius Code normally after ?combatdepth mobius "+branch+" next. Control affects only the next967 chance callback. QAEnemy holds ordinary rifle692; use helper ranged request against CSR_Tester BEFORE casting; keep it firing through ten-second expiry. Ordinary contest/damage RNG remain natural. No forced-entry/susceptibility/FX/general immunity claim. See docs/ABILITY-MOBIUS-FREE-ATTACK.md."});
    }
    public override NativeQaModePlan Plan {get;}
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),"QAEnemy","QAEnemy");
        context.Helpers.Add("QAEnemy",enemy);await enemy.EnterWorldAsync();
        enemy.SendRpc("63 01 00 69 00");await enemy.WaitForRpcAsync("65 01 00 69 00");
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
