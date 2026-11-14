using HeadlessClient;
using hds;
namespace hds.Tests;
public class MervFollowupApproachTests
{
    [Fact] public void HistoricalWaitingLineCannotExitTheNewPhaseOrCompletedMission()
    {
        Assert.True(MervFollowupApproachFixture.OwnsWaitingPhase(new(){Mission=301203,Phase=1},301203,1));
        Assert.False(MervFollowupApproachFixture.OwnsWaitingPhase(new(){Mission=301203,Phase=2},301203,1));
        Assert.False(MervFollowupApproachFixture.OwnsWaitingPhase(new(){Mission=301204,Phase=1},301203,1));
        Assert.False(MervFollowupApproachFixture.OwnsWaitingPhase(null,301203,1));
    }
    [Fact] public void OmittedComputerCreationStateUsesItsShippedDefault()
    {
        var attrs=new Dictionary<string,byte[]>();var view=new ViewCreation(10,3,1,attrs);
        Assert.Equal(ObjectStates.DefaultState(3),MervFollowupApproachFixture.InitialState(view));
        attrs["CurrentState"]=BitConverter.GetBytes(123u);Assert.Equal(123u,MervFollowupApproachFixture.InitialState(view));
    }
    [Fact] public void FriendlyOmissionAndReusedGenerationAreHandledBeforeIdentityMatching()
    {
        var mob=new MissionProgress.SavedMob{Name="Friendly",X=1,Y=95,Z=2,Friendly=true};
        var attrs=new Dictionary<string,byte[]>{{"Position",new[]{1d,95d,2d}.SelectMany(BitConverter.GetBytes).ToArray()},{"CharacterName",System.Text.Encoding.UTF8.GetBytes("Friendly\0")},{"DontAttack",new byte[]{1}},{"MissionKey",new byte[]{1,42,0,2}}};
        var old=new ViewCreation(42,599,1,attrs);Assert.True(MervFollowupApproachFixture.NpcMatches(old,mob,42));
        var reused=new ViewCreation(42,599,2,new Dictionary<string,byte[]>());
        Assert.DoesNotContain(MervFollowupApproachFixture.CurrentGenerations(new[]{old,reused},0),v=>MervFollowupApproachFixture.NpcMatches(v,mob,42));
        Assert.False(MervFollowupApproachFixture.NpcMatches(old,mob,43));
        var hostile=new MissionProgress.SavedMob{Name="Friendly",X=1,Y=95,Z=2,Friendly=false,Hostile=false};
        attrs["IsEnemy"]=new byte[]{1};Assert.True(MervFollowupApproachFixture.NpcMatches(old,hostile,42));
        Assert.False(MervFollowupApproachFixture.NpcMatches(old,mob,42));
    }
    [Fact] public void CurrentDoorLookupRefusesHistoricalNearbyAndAmbiguousDoors()
    {
        string Row(string key,int x)=>$"1,0,{key},0,A301,0,{x},545,200,0,0";
        Assert.Equal(1u,MervFollowupApproachFixture.DoorAt(new[]{Row("01000000",100),Row("02000000",101)},new[]{100d,545,200}));
        Assert.Throws<InvalidDataException>(()=>MervFollowupApproachFixture.DoorAt(new[]{Row("01000000",101)},new[]{100d,545,200}));
        Assert.Throws<InvalidDataException>(()=>MervFollowupApproachFixture.DoorAt(new[]{Row("01000000",100),Row("02000000",100)},new[]{100d,545,200}));
    }
    [Theory] [InlineData(3,1412,true)] [InlineData(548,1410,true)] [InlineData(552,1410,true)] [InlineData(326,326,false)]
    public void AdoptedPropsCannotFallBackToUnqualifiedLegacyCoordinates(ushort concrete,ushort source,bool expected)
        =>Assert.Equal(expected,MervFollowupApproachFixture.RequiresWitness(new(){Goid=concrete,Spot=new(){type=BitConverter.GetBytes(source)}}));
    [Fact] public void ActualViewRequiresAllThreeCoordinatesAndQuaternion()
    {
        var centre=new[]{1d,2d,3d};var q="0000000000000000000000000000803F";
        var attrs=new Dictionary<string,byte[]>{{"Position",centre.SelectMany(BitConverter.GetBytes).ToArray()},{"Orientation",Convert.FromHexString(q)}};
        Assert.True(MervFollowupApproachFixture.Matches(new(10,552,1,attrs),552,centre,q));
        attrs["Position"]=new[]{1d,99d,3d}.SelectMany(BitConverter.GetBytes).ToArray();
        Assert.False(MervFollowupApproachFixture.Matches(new(10,552,1,attrs),552,centre,q));
        attrs["Position"]=centre.SelectMany(BitConverter.GetBytes).ToArray();attrs["Orientation"]=new byte[16];
        Assert.False(MervFollowupApproachFixture.Matches(new(10,552,1,attrs),552,centre,q));
    }
}
