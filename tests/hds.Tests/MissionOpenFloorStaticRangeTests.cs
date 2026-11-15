using hds;
using G = hds.MissionOpenFloorResolver;
using P = hds.MissionOpenFloorPhasePlanner;

namespace hds.Tests;

public class MissionOpenFloorStaticRangeTests
{
    private const string Q = "0000000000000000000000000000803f";
    private static G.Point[] Rect(double left, double right, double bottom = -500, double top = 500) =>
        new[] { new G.Point(left, bottom), new G.Point(right, bottom), new G.Point(right, top), new G.Point(left, top) };

    private static G.Request Input(bool right)
    {
        var id = new G.Id(127, 1, 1);
        var scene = new G.StaticScene(1, true, new(-600, 600, -500, 500), -100, 1000,
            new[] { new G.StaticObstacle("closed-partition", 99, 1407, 0, 200, Rect(-20, 20), "literal closed partition") }, "literal complete scene");
        return new G.Request(1, G.SourceModelPresence.Model6Absent, "literal positive absence",
            new(new(right ? 300 : -350, 145, 0), Q, right ? 2u : 1u, 1415),
            new(new(right ? 300 : -350, 0, 0), Q, right ? 2u : 1u, 1415), "literal anchor",
            new OpenFloorModelBounds(3, new(-10, -10, -10), new(10, 10, 10), "literal small body"),
            MissionOpenFloorUsableRange.Profile.InitialClient400(3, 5, new(43, 25, 40), "literal initial400"),
            new[] { new G.Surface(id, Rect(-600, 600), 0, 0, 0) }, new(id, new(-500, 95, 0), "literal unchanged root"),
            right ? new(30, 600, -500, 500) : new(-600, -30, -500, 500), scene);
    }

    [Fact]
    public void InRangeAcrossAClosedPartitionDoesNotAdmitAnOutdoorBody()
    {
        var left = Input(false); var right = Input(true);
        Assert.True(G.TryBodySequence(left, out var a, out string why, out _), why);
        Assert.False(G.TryBodySequence(right, out _, out why, out _));
        Assert.Equal("NoGeometricBodyCandidates",why);
        Assert.NotEmpty(a.Enumerate(true));
        Assert.All(a, body => Assert.True(body.RootReachableCentre));
        Assert.False(P.TryPlan(new[] { new P.Input(1, left), new P.Input(2, right) }, out var plan, out why, out _, P.Limits.Default));
        Assert.Null(plan);
        var oldCentre=new OpenFloorPoint(30,10,0);var oldStanding=new OpenFloorPoint(-30,95,0);
        Assert.True(MissionOpenFloorUsableRange.Test(right.Usable,oldStanding,oldCentre,new OpenFloorOrientation(Q)).Within);
        Assert.True(G.TryReachableRegion(right.World,right.Surfaces,right.Entry,right.Scene,out var region,out why),why);
        Assert.True(region.Contains(new(-100,95,0)));Assert.False(region.Contains(new(100,95,0)));Assert.False(region.Contains(new(-100,495,0)));
    }

    [Fact]
    public void HintTraversalReplaysWithoutDrainingOrChangingTheRetainedAllCandidateFamily()
    {
        Assert.True(G.TryBodySequence(Input(false), out var bodies, out string why, out _), why);
        var all = bodies.ToArray();
        var hints = bodies.Enumerate(true).ToArray(); Assert.NotEmpty(hints);
        int evaluations = bodies.GeometryEvaluations;
        Assert.Equal(hints, bodies.Enumerate(true).ToArray());
        Assert.Equal(evaluations, bodies.GeometryEvaluations);
        Assert.Equal(all, bodies.ToArray());
    }
}
