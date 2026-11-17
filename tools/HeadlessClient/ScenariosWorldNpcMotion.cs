using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static OpenWorldCurrentNpc.Current MotionNpc(HeadlessPlayer p)=>OpenWorldCurrentNpc.Require(p.World.CurrentOwnViewLife(),19403,-705,60701);
    private static double GroundDistance(double[] a,double[] b)=>Math.Sqrt(Math.Pow(a[0]-b[0],2)+Math.Pow(a[2]-b[2],2));
    private static void MotionFloor(ScenarioContext context,IEnumerable<double[]> points,double maximumStep)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data/navmesh"));
        double[]? previous=null;
        foreach(var point in points)
        {
            Check.That(NavMesh.Locate(1,point[0],point[1],point[2],out var at)&&!at.indoors&&Math.Abs(at.floorY+95-point[1])<.1,"Observed NPC movement remains on outdoor floor");
            if(previous!=null)Check.That(GroundDistance(previous,point)<=maximumStep+1,"Every observed step is bounded; no endpoint jump");
            previous=point;
        }
    }
    private static async Task WorldNpcWander(ScenarioContext context)
    {
        var seed=OpenWorldFirstLevelFixture.Seed with {Handle="WanderOne",Level=50};
        var players=await context.AddPlayersInWorldAsync(seed,seed with{Handle="WanderTwo",X=19423});
        var a=players[0];var b=players[1];
        await a.WaitUntilAsync(()=>{try{return MotionNpc(a).Positions.Count>=4;}catch(InvalidDataException){return false;}},"ordinary NPC sends continuous walking updates",TimeSpan.FromSeconds(30));
        await b.WaitUntilAsync(()=>{try{return GroundDistance(MotionNpc(b).Position,MotionNpc(a).Position)<.1;}catch(InvalidDataException){return false;}},"both viewers receive the same motion",TimeSpan.FromSeconds(10));
        bool BothStanding()=>MotionNpc(a).LastMotion==Mob.Animations.STAND&&MotionNpc(b).LastMotion==Mob.Animations.STAND;
        await a.WaitUntilAsync(BothStanding,"both viewers see the NPC stand at the end of a leg",TimeSpan.FromSeconds(15));
        int idleFrames=MotionNpc(a).Positions.Count;
        await Task.Delay(TimeSpan.FromSeconds(5));
        Check.Equal(idleFrames,MotionNpc(a).Positions.Count,"No movement frames during the idle pause");

        await a.WaitUntilAsync(()=>MotionNpc(a).LastMotion==Mob.Animations.WALK,"wandering resumes between pauses",TimeSpan.FromSeconds(15));
        void Select(HeadlessPlayer player)
        {
            var view=MotionNpc(player).View;
            player.SendRpc($"81 51 {Le(view.ViewId)} {view.SpawnId:x2} 00");
        }
        Select(a);
        await a.WaitUntilAsync(BothStanding,"native target RPC stops an active wander leg for both viewers",TimeSpan.FromSeconds(3));
        int heldFrames=MotionNpc(b).Positions.Count;
        Select(b);
        a.SendRpc("81 51 00 00 00 00");
        await Task.Delay(TimeSpan.FromSeconds(16));
        Check.Equal(heldFrames,MotionNpc(b).Positions.Count,"The second viewer holds wander beyond a normal idle pause");
        b.SendRpc("81 51 00 00 00 00");
        await b.WaitUntilAsync(()=>MotionNpc(b).LastMotion==Mob.Animations.WALK,"wandering resumes after the final viewer clears selection",TimeSpan.FromSeconds(20));

        var observed=MotionNpc(a);MotionFloor(context,observed.Positions,MobMotion.WalkSpeed);
        Check.That(observed.Positions.All(p=>GroundDistance(p,new[]{19403d,-705,60701})<=MobMotion.WanderRadius+26),"Wander remains within its spawn neighborhood");
    }
    private static async Task WorldNpcChase(ScenarioContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data/navmesh"));
        var fixture=WorldNpcMotionFixture.Chase(context.RepositoryRoot);var approach=fixture.Start;var origin=fixture.Origin;
        Console.WriteLine($"Chase fixture {fixture.Name} at {string.Join(",",origin)}; player {approach.x},{approach.y},{approach.z}");
        var seed=OpenWorldFirstLevelFixture.Seed with {Handle="ChaseObserver",Level=49,X=approach.x,Y=approach.y,Z=approach.z};
        var observer=(await context.AddPlayersInWorldAsync(seed))[0];
        var actor=(await context.AddPlayersInWorldAsync(seed with {Handle="ChaseTarget",Level=1,Health=5000,MaxHealth=5000}))[0];
        await actor.WaitUntilAsync(()=>actor.World.RpcSnapshot().Any(r=>r.Bytes.Length>0&&r.Bytes[0]==0x46),"NPC initiates close combat after pursuing",TimeSpan.FromSeconds(30));
        var current=OpenWorldCurrentNpc.Require(observer.World.CurrentOwnViewLife(),origin[0],origin[1],origin[2],fixture.Name,fixture.Level,Convert.FromHexString(fixture.Rsi),birthRadius:1);var steps=current.Positions;
        Check.That(steps.Count>=3,"Chase sends intermediate movement to a bystander");
        Check.That(steps.Skip(1).Any(p=>GroundDistance(p,new[]{approach.x,approach.y,approach.z})>250),"NPC moves before it reaches melee distance");
        MotionFloor(context,steps,MobMotion.ChaseSpeed);
        Check.That(GroundDistance(current.Position,new[]{approach.x,approach.y,approach.z})<=MobAggro.MeleeDistanceUnits+1,"NPC closes to melee without teleporting");
    }
}
