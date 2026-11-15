using System.Collections;
using System.Reflection;
using System.Text.Json;
using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public class MissionOpenFloorCandidateDedupTests
{
    private static string Bits(double n)=>BitConverter.DoubleToInt64Bits(n).ToString("X16");
    [Theory] [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void EarlyDedupRetainsTheExactOrderedLateDistinctSequenceIncludingRejectedDuplicates(bool rotated,bool rootAtAnchor)
    {
        const string identity="0000000000000000000000000000803f",yaw="00000000f304353f00000000f304353f";
        string q=rotated?yaw:identity;var id=new G.Id(127,1,1);G.Point[] rect={new(-500,-500),new(500,-500),new(500,500),new(-500,500)};
        G.Point[] obstruction={new(100,-150),new(160,-150),new(160,150),new(100,150)};
        var r=new G.Request(1,G.SourceModelPresence.Model6Absent,"literal absence",new(new(0,145,0),q,1,1415),new(new(0,0,0),q,1,1415),"literal source",
            new OpenFloorModelBounds(3,new(-10,-10,-20),new(10,10,20),"literal asymmetric body"),MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"literal400"),
            new[]{new G.Surface(id,new[]{new G.Point(-500,-500),new G.Point(0,-500),new G.Point(0,500),new G.Point(-500,500)},0,0,0),new G.Surface(new(127,1,2),new[]{new G.Point(0,-500),new G.Point(500,-500),new G.Point(500,500),new G.Point(0,500)},0,0,0)},new(id,new(rootAtAnchor?0:-300,95,0),"literal root"),new(-500,500,-500,500),
            new(1,true,new(-500,500,-500,500),-100,1000,new[]{new G.StaticObstacle("other",99,1407,0,200,obstruction,"literal bound")},"literal complete scene"));
        string input=r.Fingerprint;var type=typeof(G);const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var prepared=type.GetMethod("Prepare",flags)!.Invoke(null,new object[]{r})!;var context=prepared.GetType().GetField("Item1")!.GetValue(prepared)!;Assert.NotNull(context);
        var method=type.GetMethod("GeometricCandidatesCore",flags)!;
        string Sequence(bool expanded,bool early)
        {
            var values=((IEnumerable)method.Invoke(null,new object[]{context,expanded,early})!).Cast<object>().ToArray();Assert.NotEmpty(values);
            object Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
            return JsonSerializer.Serialize(values.Select(value=>new{
                Id=new[]{((G.Surface)Field(value,"Surface")).Identity.Sector,((G.Surface)Field(value,"Surface")).Identity.Region,((G.Surface)Field(value,"Surface")).Identity.Polygon},
                Centre=new[]{Bits(((G.Point)Field(value,"Centre")).X),Bits(((G.Point)Field(value,"Centre")).Z)},
                Shape=((G.Point[])Field(value,"Shape")).Select(p=>new[]{Bits(p.X),Bits(p.Z)}).ToArray(),Distance=Bits((double)Field(value,"Distance"))}).ToArray());
        }
        Assert.Equal(Sequence(true,false),Sequence(true,true));Assert.Equal(Sequence(false,false),Sequence(false,true));Assert.Equal(input,r.Fingerprint);
    }
}
