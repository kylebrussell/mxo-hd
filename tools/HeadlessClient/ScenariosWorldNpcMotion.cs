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

    private static async Task WorldNpcChaseActiveFight(ScenarioContext context)
    {
        WorldNpcCornerFixture.CheckGeometry(context.RepositoryRoot);
        double[] goofOrigin = { 18924, 95, -8852 };
        double[] mushfakeOrigin = { 19615, 95, -8583 };
        var stand = Enumerable.Range(0, 16).Select(step => step * Math.PI / 8)
            .Select(angle => new[] { mushfakeOrigin[0] + 200 * Math.Cos(angle), 95, mushfakeOrigin[2] + 200 * Math.Sin(angle) })
            .First(point => NavMesh.Locate(1, point[0], point[1], point[2], out var at) && !at.indoors &&
                Math.Abs(point[1] - at.floorY - 95) < 1 && WorldNpcCornerFixture.Straight(mushfakeOrigin, point) &&
                WorldNpcCornerFixture.Straight(goofOrigin, point) && GroundDistance(goofOrigin, point) is > 800 and < 1200);
        var seed = OpenWorldFirstLevelFixture.Seed with
        {
            Handle = "ChaseActiveFight", X = stand[0], Y = stand[1], Z = stand[2],
            Level = 2, Experience = 5100, Health = 5000, MaxHealth = 5000
        };
        var actor = (await context.AddPlayersInWorldAsync(seed))[0];
        ViewCreation? SelectedMushfake() => actor.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == 599 && view.Name == "Blackwood Mushfake" &&
            GroundDistance(EngageFoesFixture.Position(view), mushfakeOrigin) < 1);
        await actor.WaitUntilAsync(() => SelectedMushfake() != null, "The world Mushfake at the nearby stand", TimeSpan.FromSeconds(10));
        var mushfake = SelectedMushfake()!;
        OpenWorldCurrentNpc.Current? firstChase = null;
        await actor.WaitUntilAsync(() =>
        {
            try { firstChase = WorldNpcCornerFixture.Observe(actor); }
            catch (InvalidDataException) { return false; }
            return firstChase.Positions.Count > 1 && firstChase.LastMotion == Mob.Animations.RUN;
        }, "Blackwood Goof is already pursuing the player", TimeSpan.FromSeconds(10));
        Check.That(GroundDistance(firstChase!.Position, stand) > MobAggro.MeleeDistanceUnits + 100,
            "The old chase still has ground to cover");
        string current = HandleOf(mushfake), pursuing = HandleOf(firstChase.View);
        actor.SendRpc($"40 {current} 01 6b");
        await actor.WaitForRpcAsync($"46 {current} 01");
        await actor.WaitUntilAsync(() =>
        {
            var goof = WorldNpcCornerFixture.Observe(actor);
            return GroundDistance(goof.Position, stand) <= MobAggro.MeleeDistanceUnits + 1 &&
                goof.LastMotion == Mob.Animations.STAND;
        }, "The old Goof pursuit reaches arm's length and stops", TimeSpan.FromSeconds(10));

        Check.Equal(0, actor.Received($"46 {pursuing} 01").Count, "The pursuing Goof did not take over the newer fight");
        Check.Equal(0, actor.Received($"46 {current} 00").Count, "The newer Mushfake fight stays open");
        actor.SkipReceived();
        uint targetHandle = (uint)mushfake.ViewId | (uint)mushfake.SpawnId << 16;
        byte[] round = (await actor.WaitForRpcAsync("45", TimeSpan.FromSeconds(10))).Bytes;
        Check.That(round.Length >= 40 && BitConverter.ToUInt32(round, round.Length - 40) == targetHandle &&
            BitConverter.ToUInt32(round, round.Length - 36) == targetHandle,
            "A fresh round still addresses the original Mushfake after the Goof arrives");
        Check.Equal(0, actor.Received($"46 {pursuing} 01").Count, "The old chase still cannot replace the opponent");
        Console.WriteLine($"Mara stand {string.Join(",", stand)}: Goof arrived while Mushfake's interlock continued.");
    }

    private static async Task WorldNpcCornerStationary(ScenarioContext context)
    {
        WorldNpcCornerFixture.CheckGeometry(context.RepositoryRoot);
        var end = WorldNpcCornerFixture.Turn;
        var seed = OpenWorldFirstLevelFixture.Seed with
        {
            Handle = "StationaryCornerTarget", X = end[0], Y = end[1], Z = end[2],
            Level = 2, Experience = 5100, Health = 5000, MaxHealth = 5000
        };
        var actor = (await context.AddPlayersInWorldAsync(seed))[0];
        await actor.WaitForRpcAsync("46", TimeSpan.FromSeconds(20));
        await actor.WaitUntilAsync(() => WorldNpcCornerFixture.Distance(WorldNpcCornerFixture.Observe(actor).Position, end) <=
            MobAggro.MeleeDistanceUnits + 1, "The named Goof reached the stationary far-side player", TimeSpan.FromSeconds(3));
        var caught = WorldNpcCornerFixture.Observe(actor);
        Check.That(caught.Positions.Count >= 4, "NPC sent intermediate grounded steps");
        Check.That(caught.Positions.Zip(caught.Positions.Skip(1), WorldNpcCornerFixture.Straight).All(clear => clear),
            "No observed pursuit segment crossed the blocked corner");
        MotionFloor(context, caught.Positions, MobMotion.ChaseSpeed);
        Check.That(context.Server.DebugLog().Contains($"[AGGRO] Blackwood Goof attacks {actor.CharId} "),
            "Interlock started from the selected world Goof");
        Console.WriteLine($"Stationary Mara corner: {caught.Positions.Count} grounded points and interlock with no movement input.");
    }

    private static async Task WorldNpcCornerChase(ScenarioContext context)
    {
        WorldNpcCornerFixture.CheckGeometry(context.RepositoryRoot);
        var start = WorldNpcCornerFixture.Aggro;
        var end = WorldNpcCornerFixture.Turn;
        var seed = OpenWorldFirstLevelFixture.Seed with
        {
            Handle = "CornerTarget", X = start[0], Y = start[1], Z = start[2],
            Level = 2, Experience = 5100, Health = 5000, MaxHealth = 5000
        };
        var actor = (await context.AddPlayersInWorldAsync(seed))[0];
        await actor.WaitUntilAsync(() =>
        {
            try { return WorldNpcCornerFixture.Observe(actor).Positions.Count >= 2; }
            catch (InvalidDataException) { return false; }
        }, "Blackwood Goof has started to pursue before the player turns", TimeSpan.FromSeconds(15));

        for (int move = 1; move <= 6; move++)
        {
            var point = Enumerable.Range(0, 3).Select(i => start[i] + (end[i] - start[i]) * move / 6).ToArray();
            actor.World.SendViewList(DisturbingTrendsFixture.ClientPosition(point));
            await Task.Delay(100);
        }
        var turned = WorldNpcCornerFixture.Observe(actor);
        Check.That(!WorldNpcCornerFixture.Straight(turned.Position, end), "Player reached the obstructed side during pursuit");
        await actor.WaitForRpcAsync("46", TimeSpan.FromSeconds(18));
        await actor.WaitUntilAsync(() => WorldNpcCornerFixture.Distance(WorldNpcCornerFixture.Observe(actor).Position, end) <=
            MobAggro.MeleeDistanceUnits + 1, "The same NPC reached the player on the far side", TimeSpan.FromSeconds(3));
        var caught = WorldNpcCornerFixture.Observe(actor);
        Check.That(caught.Positions.Count > turned.Positions.Count + 1, "NPC sent intermediate movement around the corner");
        MotionFloor(context, caught.Positions, MobMotion.ChaseSpeed);
        Check.That(caught.Positions.Zip(caught.Positions.Skip(1), WorldNpcCornerFixture.Straight).All(clear => clear),
            "No observed pursuit segment crossed the blocked corner");
        Console.WriteLine($"Mara corner: {caught.Positions.Count} grounded points with ordinary interlock at the far marker.");
    }

    private static async Task WorldNpcDeferredFlushCrcStress(ScenarioContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data/sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data/navmesh"));
        var fixture = WorldNpcMotionFixture.Chase(context.RepositoryRoot);
        var approach = fixture.Start;

        var wanderSeed = OpenWorldFirstLevelFixture.Seed with { Handle = "CrcWander", Level = 50 };
        var aggroSeed = OpenWorldFirstLevelFixture.Seed with
        {
            Handle = "CrcAggro", Level = 1, Health = 5000, MaxHealth = 5000,
            X = approach.x, Y = approach.y, Z = approach.z
        };
        var players = await context.AddPlayersInWorldAsync(wanderSeed, aggroSeed);
        var wanderer = players[0];
        var target = players[1];

        // Wait for live wandering motion (deferred Mob.updateAnimation flushes) and active aggro pursuit.
        await wanderer.WaitUntilAsync(() =>
        {
            try { return MotionNpc(wanderer).Positions.Count >= 2; }
            catch (InvalidDataException) { return false; }
        }, "wandering NPC broadcasts deferred movement updates", TimeSpan.FromSeconds(30));

        // Drive heavy multi-block inbound traffic (1..9 Twofish blocks: movement, varied-length chat, 81 48 echoes)
        // from both players concurrently while wandering and aggroing NPCs push deferred flushes.
        for (int i = 0; i < 120; i++)
        {
            double dx = (i % 5) * 4.0;
            wanderer.World.SendViewList(DisturbingTrendsFixture.ClientPosition(new[] { 19403.0 + dx, -705.0, 60701.0 }));
            target.World.SendViewList(DisturbingTrendsFixture.ClientPosition(new[] { approach.x + dx, approach.y, approach.z }));

            int msgLen = 8 + (i % 10) * 12; // spans 49..161+ wire bytes
            string body = new string((char)('a' + (i % 26)), msgLen);
            wanderer.SendChat(body);
            target.SendChat(body);

            if ((i & 3) == 0)
            {
                wanderer.SendRpc("81 48");
                target.SendRpc("81 48");
            }
            await Task.Delay(25);
        }

        await target.WaitUntilAsync(() => target.World.RpcSnapshot().Any(r => r.Bytes.Length > 0 && r.Bytes[0] == 0x46),
            "aggroing world NPC reaches melee interlock during inbound stress", TimeSpan.FromSeconds(30));
        await wanderer.WaitUntilAsync(() =>
        {
            try { return MotionNpc(wanderer).Positions.Count >= 4; }
            catch (InvalidDataException) { return false; }
        }, "wandering NPC keeps moving during inbound stress", TimeSpan.FromSeconds(20));

        wanderer.SkipReceived();
        target.SkipReceived();
        wanderer.SendRpc("81 48");
        target.SendRpc("81 48");
        await wanderer.WaitForRpcAsync("81 49");
        await target.WaitForRpcAsync("81 49");

        var crcDrops = context.Server.OutputLines("CRC didn't match");
        var allDrops = context.Server.OutputLines("Dropping packet:");
        Check.Equal(0, crcDrops.Count, "Zero server inbound CRC drops under wandering/aggroing deferred flushes: " + string.Join(" | ", crcDrops.Take(5)));
        Check.Equal(0, allDrops.Count, "Zero server inbound packet drops under wandering/aggroing deferred flushes: " + string.Join(" | ", allDrops.Take(5)));
        Check.Equal(0, wanderer.World.Problems.Count, "Zero client wire problems for wander observer");
        Check.Equal(0, target.World.Problems.Count, "Zero client wire problems for aggro target");
        Console.WriteLine($"Deferred-flush CRC stress: 0 server drops across 600+ inbound packets (wander frames={MotionNpc(wanderer).Positions.Count}).");
    }
}
