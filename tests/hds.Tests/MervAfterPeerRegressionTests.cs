using hds;
using HeadlessClient;
namespace hds.Tests;
public class MervAfterPeerRegressionTests
{
    private static ViewCreation View(double x,double y,double z,bool friendly,byte spawn=1)
    {
        var a=new Dictionary<string,byte[]>{["CharacterName"]=System.Text.Encoding.ASCII.GetBytes("Agent\0"),["Position"]=new[]{x,y,z}.SelectMany(BitConverter.GetBytes).ToArray(),["IsEnemy"]=new[]{(byte)(friendly?0:1)}};
        if(friendly){a["TalkDefaultable"]=new byte[]{1};a["DontAttack"]=new byte[]{1};}return new(7,599,spawn,a);
    }
    [Fact] public void DuplicateNamesFloorSeparationAndSideReplacementCannotAliasCurrentNpc()
    {
        var m=new MissionProgress.SavedMob{Identity="actual-npc6",Name="Agent",X=100,Y=2095,Z=300,Friendly=false,Hostile=false};
        Assert.True(MervNpcViewFixture.AliveMatches(m,View(100,2095,300,false)));
        Assert.False(MervNpcViewFixture.AliveMatches(m,View(101,2095,300,false)));
        Assert.False(MervNpcViewFixture.AliveMatches(m,View(100,495,300,false)));
        Assert.False(MervNpcViewFixture.AliveMatches(m,View(100,2095,300,true,2)));
        m.Dead=true;Assert.False(MervNpcViewFixture.AliveMatches(m,View(100,2095,300,false)));
        byte[] death=Convert.FromHexString("070007011D0D000100002B00250000").Concat(new[]{100f,2095f,300f}.SelectMany(BitConverter.GetBytes)).ToArray();
        Assert.True(MervNpcViewFixture.CorpseMatches(m,View(100,2095,300,false),new[]{death}));
        Assert.False(MervNpcViewFixture.CorpseMatches(m,View(100,2095,300,false) with{ViewId=8},new[]{death}));
    }
    [Fact] public void ConvertedMissingWitnessRefusesWhileBoundFurnitureIsExplicitlyWeaker()
    {
        var source=new StaticWorldObject{type=BitConverter.GetBytes((ushort)1415)};
        Assert.True(MervNpcViewFixture.RequiresWitness(new(){Goid=3,Spot=source}));
        Assert.False(MervNpcViewFixture.RequiresWitness(new(){Goid=1415,Spot=source}));
    }
    private static byte[] Total(byte op,uint amount,uint type)=>new byte[]{0x80,op}.Concat(BitConverter.GetBytes(amount)).Concat(BitConverter.GetBytes(type)).ToArray();
    [Theory][InlineData(false,25u)][InlineData(true,25u)][InlineData(false,0u)][InlineData(true,0u)]
    public void IndependentEarnedLedgerRequiresCurrentClaimAndExplicitRelogBaselines(bool claimed,uint cash)
    {
        var window=Convert.FromHexString("811900000000100019000000000000000100000000088F1A0000");
        BitConverter.GetBytes(cash).CopyTo(window,8);
        var records=new List<byte[]>{Total(0xe5,100,0),Total(0xe4,1000,0),window,Total(0xe4,1000+cash,0),
            Convert.FromHexString(claimed?"81200C0000000000000000000100000000088F1A0000":"81200C0000000000000000000000"),
            Convert.FromHexString("80A20000000000000000005B0000004400000000000000000000"),Total(0xe6,68,2),Total(0xe5,168,2),Total(0xe4,1091+cash,2),Total(0xe5,168,0),Total(0xe4,1091+cash,0)};
        var ops=new[]{new MervLedgerOperation("claim","Loot",2,5),new MervLedgerOperation("exit","Exit",5,9),new MervLedgerOperation("relog","Relog",9,11)};
        if(!claimed)
        {Assert.Throws<InvalidDataException>(()=>MervRewardLedger.Reconcile(records,ops,100,1000,new uint[]{68},new uint[]{91},new HashSet<int>{9,10}));return;}
        var ledger=MervRewardLedger.Reconcile(records,ops,100,1000,new uint[]{68},new uint[]{91},new HashSet<int>{9,10});
        Assert.Equal((long)cash,ledger.LootInformation);Assert.Equal(168u,ledger.Experience);Assert.Equal(1091+cash,ledger.Information);
        records[10]=Total(0xe4,1207,0);Assert.Throws<InvalidDataException>(()=>MervRewardLedger.Reconcile(records,ops,100,1000,new uint[]{68},new uint[]{91},new HashSet<int>{9,10}));
    }
}
