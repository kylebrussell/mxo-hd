using HeadlessClient;
using hds;
namespace hds.Tests;
public class MervFollowupApproachTests
{
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
