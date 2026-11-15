using System.Reflection;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;

public class MissionTopologySpaceFilterTests
{
    private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly MethodInfo Convex=typeof(G).GetMethod("Convex",Flags)!;
    private static readonly MethodInfo Portal=typeof(G).GetMethods(Flags).Single(m=>m.Name=="Portal"&&m.GetParameters().Length==2);
    private static readonly MethodInfo Component=typeof(G).GetMethods(Flags).Single(m=>m.Name=="ConnectedComponent"&&m.GetParameters().Length==3);
    private static G.Point[] Rect(double x,double z)=>new[]{new G.Point(x,z),new G.Point(x+100,z),new G.Point(x+100,z+100),new G.Point(x,z+100)};
    private static G.Surface[] OldComponent(IReadOnlyList<G.Surface> surfaces,G.Surface start)
    {
        var all=surfaces.Where(s=>s.Flat&&s.FloorC==start.FloorC&&(bool)Convex.Invoke(null,new object[]{s.Vertices})!).OrderBy(s=>s.Identity).ToArray();
        var found=new HashSet<G.Id>{start.Identity};var queue=new Queue<G.Surface>();queue.Enqueue(start);
        while(queue.Count>0)
        {
            var current=queue.Dequeue();
            foreach(var surface in all)
                if(!found.Contains(surface.Identity)&&Portal.Invoke(null,new object[]{current,surface})!=null)
                {found.Add(surface.Identity);queue.Enqueue(surface);}
        }
        return all.Where(s=>found.Contains(s.Identity)).ToArray();
    }
    [Fact] public void IndoorOtherRoomsCannotChangeOldOrderedComponentForAnyStart()
    {
        var surfaces=new List<G.Surface>();
        for(ushort room=1;room<=8;room++)for(ushort poly=0;poly<5;poly++)
            surfaces.Add(new(new(209,room,poly),Rect(poly*100,0),0,0,0,true));
        surfaces.Add(new(new(210,1,0),Rect(0,100),0,0,0,true));
        surfaces.Add(new(new(209,1,9),Rect(0,100),0,0,100,true));
        surfaces.Reverse();
        foreach(var start in surfaces)
        {
            var expected=OldComponent(surfaces,start);
            var actual=(G.Surface[])Component.Invoke(null,new object[]{(ushort)1,surfaces,start})!;
            Assert.Equal(expected.Select(s=>s.Identity),actual.Select(s=>s.Identity));
            Assert.All(actual,s=>Assert.True(G.SameSpace(start,s)));
        }
    }
    [Fact] public void OutdoorsStillConnectsAcrossSectorAndRegionIdentities()
    {
        var a=new G.Surface(new(209,1,1),Rect(0,0),0,0,0,false);
        var b=new G.Surface(new(210,99,2),Rect(100,0),0,0,0,false);
        var indoor=new G.Surface(new(209,1,3),Rect(0,100),0,0,0,true);
        var surfaces=new[]{indoor,b,a};
        foreach(var start in new[]{a,b})
        {
            var actual=(G.Surface[])Component.Invoke(null,new object[]{(ushort)1,surfaces,start})!;
            Assert.Equal(OldComponent(surfaces,start).Select(s=>s.Identity),actual.Select(s=>s.Identity));
            Assert.Equal(2,actual.Length);
        }
    }
    [Fact] public void InvalidGeometryInAnotherRoomIsStillRejectedBeforeTopologyLookup()
    {
        var start=new G.Surface(new(209,1,1),Rect(0,0),0,0,0,true);
        var invalid=new G.Surface(new(209,2,1),new[]{new G.Point(double.NaN,0),new G.Point(1,0),new G.Point(1,1)},0,0,0,true);
        var root=new G.Root(start.Identity,new(50,95,50),"literal root");
        var scene=new G.StaticScene(1,true,new(-100,300,-100,300),-100,1000,Array.Empty<G.StaticObstacle>(),"literal complete scene");
        Assert.False(G.TryReachableRegion(1,new[]{start,invalid},root,scene,out _,out string why));
        Assert.Equal("InvalidReachableRegionInputs",why);
        var duplicate=new G.Surface(start.Identity,Rect(100,0),0,0,0,true);
        Assert.False(G.TryReachableRegion(1,new[]{start,duplicate},root,scene,out _,out why));
        Assert.Equal("InvalidReachableRegionInputs",why);
    }
}
