using HeadlessClient;
namespace hds.Tests;
public class MissionComputer1407ContactTests
{
    [Fact] public void ResolvedComputerRequiresNewCreationAndExactCommittedPoseIncludingHeightAndQuaternion()
    {
        const string q="0000000000000000000000000000803F";
        ViewCreation Computer(ushort id,double y,string quat)=>new(id,3,(byte)id,new Dictionary<string,byte[]>{
            ["Position"]=BitConverter.GetBytes(10d).Concat(BitConverter.GetBytes(y)).Concat(BitConverter.GetBytes(30d)).ToArray(),["Orientation"]=Convert.FromHexString(quat)});
        var views=new List<ViewCreation>{Computer(1,2025.003776550293,q)};int cursor=views.Count;
        Assert.Null(MissionComputer1407Fixture.ComputerSince(views,cursor,10,2025.003776550293,30,q));
        views.Add(Computer(2,2145,q)); // old raw marker height must not satisfy a resolved target
        views.Add(Computer(3,2025.003776550293,"000000000000803F0000000000000000"));
        Assert.Null(MissionComputer1407Fixture.ComputerSince(views,cursor,10,2025.003776550293,30,q));
        views.Add(Computer(4,2025.003776550293,q));
        var actual=MissionComputer1407Fixture.ComputerSince(views,cursor,10,2025.003776550293,30,q)!;
        Assert.Equal((ushort)4,actual.ViewId);Assert.Equal((byte)4,actual.SpawnId);
        Assert.Null(MissionComputer1407Fixture.ComputerSince(views,-1,10,2025.003776550293,30,q));
    }
    static ViewCreation Contact(ushort view,byte spawn,uint id=8)=>new(view,6520,spawn,new Dictionary<string,byte[]>{{"ContactID",BitConverter.GetBytes(id)}});
    [Fact] public void SameSessionReturnWaitsForFreshDiggerRatherThanHistoricalHandle()
    {
        var history=new List<ViewCreation>{Contact(9,7)};
        int beforeReturn=history.Count;
        Assert.Equal((ushort)9,MissionComputer1407Fixture.ContactSince(history,0)!.ViewId); // initial current startup role
        Assert.Null(MissionComputer1407Fixture.ContactSince(history,beforeReturn));
        history.Add(Contact(13,11,7));Assert.Null(MissionComputer1407Fixture.ContactSince(history,beforeReturn));
        history.Add(Contact(14,12));var actual=MissionComputer1407Fixture.ContactSince(history,beforeReturn)!;
        Assert.Equal((ushort)14,actual.ViewId);Assert.Equal((byte)12,actual.SpawnId);
        // Actual source Digger33000/-100950 is outside CheckViews.DespawnRange11000 at this Office exit.
        Assert.True(Math.Sqrt(Math.Pow(42200-33000,2)+Math.Pow(-88700-(-100950),2))>11000);
    }
    [Fact] public void CurrentOfferWindowKeepsExactSingleDespiteIdenticalPriorOffer()
    {
        const string actual="8096000001000F00000800000000000A0052657472696576616C00";
        var prior=new RpcRecord(1,0,Convert.FromHexString(actual));var current=new RpcRecord(2,0,Convert.FromHexString(actual));
        var history=new[]{prior,current};
        Assert.Throws<InvalidOperationException>(()=>MissionComputer1407Fixture.RetrievalSince(history,0));
        Assert.Same(current,MissionComputer1407Fixture.RetrievalSince(history,1));
        Assert.Throws<InvalidOperationException>(()=>MissionComputer1407Fixture.RetrievalSince(history,2));
    }
}
