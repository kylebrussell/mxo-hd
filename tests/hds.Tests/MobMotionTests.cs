using hds;

namespace hds.Tests;

public class MobMotionTests
{
    static void Load()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"data/sector_properties.csv")))root=Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data/navmesh"));
    }
    [Fact]
    public void OrdinaryWorldApproachMovesInBoundedGroundedStepsWithoutCatchingUpAfterAPause()
    {
        Load();var start=new[]{19403d,-705,60401};var target=new[]{19403d,-705,60701};
        Assert.True(MobMotion.Step(1,start,target,.5,100,out var next));
        Assert.Equal(19403,next.x,3);Assert.Equal(-705,next.y,3);Assert.Equal(60451,next.z,3);
        Assert.True(NavMesh.Locate(1,next.x,next.y,next.z,out var at));Assert.Equal(at.floorY+95,next.y);
        Assert.True(MobMotion.Step(1,start,target,30,100,out var delayed));Assert.Equal(60501,delayed.z,3);
    }
    [Theory]
    [InlineData(0,100,-705)][InlineData(-1,100,-705)][InlineData(.5,0,-705)][InlineData(.5,100,-305)]
    public void InvalidTicksAndOtherFloorsNeverProduceMovement(double seconds,double speed,double targetY)
    {
        Load();Assert.False(MobMotion.Step(1,new[]{19403d,-705,60401},new[]{19403d,targetY,60701},seconds,speed,out _));
    }
    [Fact]
    public void WanderSeedCannotAffectNormalDatabasesOrNonDeveloperSessions()
    {
        Assert.Null(MobMotion.QaSeed("0","mxotest_motion","42"));Assert.Null(MobMotion.QaSeed("1","reality_hd","42"));
        Assert.Null(MobMotion.QaSeed("1","mxotest_","42"));Assert.Null(MobMotion.QaSeed("1","mxotest_a;bad","42"));
        Assert.Equal(42,MobMotion.QaSeed("1","mxotest_motion","42"));
    }
    [Fact]
    public void MissionActorsAndSimulacraNeverUseTheOrdinaryWorldMovementClock()
    {
        var mob=new Mob();Assert.True(MobMotion.Ordinary(mob));mob.missionInstance=47;Assert.False(MobMotion.Ordinary(mob));
        mob.missionInstance=0;mob.setIsDead(true);Assert.False(MobMotion.Ordinary(mob));
    }
    [Fact]
    public void NativeAndHeadlessChaseUseAnActualWalkableAmbientSpawn()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"data/npc_spawns/OpenWorld.csv")))root=Path.GetDirectoryName(root)!;
        var fixture=HeadlessClient.WorldNpcMotionFixture.Chase(root);
        Assert.True(MobMotion.Step(1,fixture.Origin,new[]{fixture.Start.x,fixture.Start.y,fixture.Start.z},.5,MobMotion.ChaseSpeed,out var next));
        Assert.False(next.location.indoors);Assert.InRange(fixture.Level,(byte)1,(byte)6);
    }

}
