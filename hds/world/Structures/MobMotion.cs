using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>One movement clock for visible ordinary NPCs. Mission actors and pets have their own controllers.</summary>
    public static class MobMotion
    {
        // Preserve the old wander travel-time calculation. Chase speed is a local gameplay default.
        public const double WalkSpeed = .176 * 500 / .9586, ChaseSpeed = 400, WanderRadius = 500;
        private const int MinimumIdleSeconds = 8, MaximumIdleSeconds = 15;
        private const double SelectionGraceSeconds = 2;
        private const double ChaseReplanDistance = 100;
        private sealed class Motion
        {
            public double Updated, WaitUntil;
            public double[] Goal;
            public WorldClient Target;
            public NavPoint[] Path;
            public double[] PathTarget;
            public int Waypoint;
            public int Life;
            public bool Walking;
            public Random Random;
        }
        private static readonly object sync = new object();
        private static readonly Dictionary<Mob,Motion> moving = new Dictionary<Mob,Motion>();
        internal static int? QaSeed(string dev,string database,string raw) => dev=="1" && database!=null &&
            database.StartsWith("mxotest_",StringComparison.Ordinal) && database.Length>8 &&
            database.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_') && int.TryParse(raw,out int seed) ? seed : (int?)null;
        private static Motion New(Mob mob,double now)
        {
            var seed=QaSeed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                Environment.GetEnvironmentVariable("MXO_QA_MOB_WANDER_SEED"));
            var random=seed.HasValue?new Random(unchecked(seed.Value^(int)mob.getEntityId())):new Random();
            return new Motion{Updated=now,WaitUntil=now+random.Next(MinimumIdleSeconds,MaximumIdleSeconds+1),
                Life=mob.DeathGeneration,Random=random};
        }
        public static bool Chasing(Mob mob) { lock(sync)return moving.TryGetValue(mob,out var state)&&state.Target!=null; }
        internal static void ResetFixtureMotion(Mob mob) { lock (sync) moving.Remove(mob); }
        private static double[] Position(Mob mob)=>new[]{mob.getXPos(),mob.getYPos(),mob.getZPos()};
        private static double[] Position(WorldClient client)
        {
            double x=0,y=0,z=0;NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(),ref x,ref y,ref z);
            return new[]{x,y,z};
        }
        internal static bool Ordinary(Mob mob)=>mob!=null&&mob.missionInstance==0&&mob.pet==null&&!mob.getIsDead()&&mob.getIsSpawned();
        internal static bool Step(ushort district,double[] from,double[] target,double seconds,double speed,out NavPoint next)
        {
            next=default;
            if(from?.Length!=3||target?.Length!=3||from.Any(v=>!double.IsFinite(v))||target.Any(v=>!double.IsFinite(v))||
                !double.IsFinite(seconds)||seconds<=0||!double.IsFinite(speed)||speed<=0||Math.Abs(from[1]-target[1])>150)return false;
            double dx=target[0]-from[0],dz=target[2]-from[2],distance=Math.Sqrt(dx*dx+dz*dz);
            if(distance<.01)return false;
            double fraction=Math.Min(1,speed*Math.Min(seconds,1)/distance);
            double x=from[0]+dx*fraction,z=from[2]+dz*fraction;
            if(!NavMesh.Locate(district,from[0],from[1],from[2],out var origin)||
                !NavMesh.Locate(district,x,from[1],z,out var at)||Math.Abs(at.floorY-origin.floorY)>150)return false;
            next=new NavPoint{x=x,y=at.floorY+95,z=z,location=at};
            // Check the whole segment, not only its endpoint; a wall can have mapped floor on both sides.
            return NavMesh.HasSightPath(district,from[0],from[1],from[2],next.x,next.y,next.z);
        }
        internal static bool BeginChase(WorldClient client,Mob mob)
        {
            if(!Ordinary(mob)||client?.playerData==null)return false;
            lock(sync)
            {
                if(!moving.TryGetValue(mob,out var state))moving[mob]=state=New(mob,TimeUtils.GetSimTimeSeconds());
                if(state.Target!=null&&!ReferenceEquals(state.Target,client))return false;
                var from=Position(mob);var target=Position(client);
                if(!NavMesh.HasSightPath(mob.getDistrict(),from[0],from[1],from[2],target[0],target[1],target[2]) ||
                    !Step(mob.getDistrict(),from,target,.5,ChaseSpeed,out _))
                {
                    if(!PlanChase(mob.getDistrict(), state, from, target))return false;
                }
                else state.Path = null;
                state.Target=client;state.Goal=null;return true;
            }
        }

        private static bool PlanChase(ushort district, Motion state, double[] from, double[] target)
        {
            state.Path = NavMesh.WalkingPath(district, from, target);
            state.PathTarget = target;
            state.Waypoint = 0;
            return state.Path.Length != 0;
        }

        private static double DistanceSquared(double[] a, double[] b)
        {
            double dx = a[0] - b[0], dz = a[2] - b[2];
            return dx * dx + dz * dz;
        }

        private static bool ChaseGoal(ushort district, Motion state, double[] from, double[] target, bool straight)
        {
            if (straight)
            {
                MobAggro.StopPoint(target[0], target[2], from[0], from[2], MobAggro.MeleeDistanceUnits, out double x, out double z);
                state.Goal = new[] { x, target[1], z };
                state.Path = null;
                return true;
            }

            bool routeExpired = state.Path == null || state.Waypoint >= state.Path.Length ||
                DistanceSquared(state.PathTarget, target) >= ChaseReplanDistance * ChaseReplanDistance;
            if (routeExpired && !PlanChase(district, state, from, target)) return false;
            while (state.Waypoint < state.Path.Length)
            {
                var goal = state.Path[state.Waypoint].Position;
                if (DistanceSquared(from, goal) > 0.0001)
                {
                    state.Goal = goal;
                    return true;
                }
                state.Waypoint++;
            }
            return false;
        }
        private static void Stop(Mob mob,Motion state,double now,bool publishArrival=false)
        {
            if(state.Walking||publishArrival)
            {
                mob.currentState=(int)Mob.statusList.STANDING;
                mob.updateAnimation((byte)Mob.Animations.STAND);
            }
            state.Walking=false;state.Goal=null;state.Target=null;state.Path=null;
            state.WaitUntil=now+state.Random.Next(MinimumIdleSeconds,MaximumIdleSeconds+1);
        }
        internal static void Tick(List<WorldClient> clients,Dictionary<ulong,Mob> mobs,double now)
        {
            var visible=new HashSet<Mob>();
            var selected=new HashSet<Mob>();
            foreach(var client in clients)
                if(client?.playerData!=null&&client.Alive&&client.playerData.getOnWorld()&&!client.playerData.waitForRPCShutDown)
                {
                    var data=client.playerData;
                    ushort targetView=data.currentSelectedTargetViewId;
                    ushort targetSpawn=data.currentSelectedTargetSpawnId;
                    foreach(var view in client.viewMan.Snapshot())
                    {
                        if(!view.viewCreated||view.viewNeedsToBeDeleted||!mobs.TryGetValue(view.entityId,out var mob)||
                            !Ordinary(mob)||mob.getDistrict()!=data.getDistrictId())continue;
                        visible.Add(mob);
                        // The native client selects the NPC when opening its radial.
                        if(!data.isDead&&view.ViewID==targetView&&view.spawnId==targetSpawn)selected.Add(mob);
                    }
                }
            lock(sync)
            {
                foreach(var old in moving.Keys.Where(m=>!visible.Contains(m)).ToArray())moving.Remove(old);
                foreach(var mob in visible)
                {
                    if(!moving.TryGetValue(mob,out var state)||state.Life!=mob.DeathGeneration)moving[mob]=state=New(mob,now);
                    double elapsed=now-state.Updated;state.Updated=now;
                    if(mob.getCombatTarget()!=null){state.Goal=null;state.Target=null;state.Path=null;state.Walking=false;continue;}
                    if(state.Target==null&&selected.Contains(mob))
                    {
                        if(state.Goal!=null||state.Walking)Stop(mob,state,now);
                        state.WaitUntil=Math.Max(state.WaitUntil,now+SelectionGraceSeconds);
                        continue;
                    }
                    if(!ControlActions.CanMove(TimedEffects.ForMob(mob).Effects))
                    {
                        if(state.Walking)
                        {
                            mob.currentState=(int)Mob.statusList.STANDING;
                            mob.updateAnimation((byte)Mob.Animations.STAND);
                            state.Walking=false;
                        }
                        continue;
                    }
                    var from=Position(mob);double speed=WalkSpeed;
                    if(state.Target!=null)
                    {
                        var target=state.Target;
                        if(!target.Alive||target.playerData.isDead||!target.playerData.getOnWorld()||target.playerData.waitForRPCShutDown||
                            target.playerData.getDistrictId()!=mob.getDistrict()||Stealth.Current(target)!=null)
                        {Stop(mob,state,now);continue;}
                        var to=Position(target);
                        if(Math.Abs(from[1]-to[1])>150||Math.Pow(to[0]-mob.xBase,2)+Math.Pow(to[2]-mob.zBase,2)>25000000)
                        {Stop(mob,state,now);continue;}
                        bool straight=NavMesh.HasSightPath(mob.getDistrict(),from[0],from[1],from[2],to[0],to[1],to[2]);
                        if(straight&&MobAggro.ReachedMeleeDistance(DistanceSquared(to,from)))
                        {
                            var view=target.viewMan.FindViewForEntity(mob.getEntityId());Stop(mob,state,now);
                            if(view?.viewCreated==true)MobAggro.Attack(target,mob,view,to[0],to[1],to[2]);continue;
                        }
                        if(!ChaseGoal(mob.getDistrict(),state,from,to,straight)){Stop(mob,state,now);continue;}
                        speed=ChaseSpeed;
                    }
                    else if(state.Goal==null&&Mob.WanderEnabled&&now>=state.WaitUntil)
                    {
                        for(int attempt=0;attempt<12;attempt++)
                        {
                            double angle=state.Random.NextDouble()*Math.PI*2,radius=WanderRadius*Math.Sqrt(state.Random.NextDouble());
                            if(NavMesh.Project(mob.getDistrict(),mob.xBase+Math.Cos(angle)*radius,mob.yBase,mob.zBase+Math.Sin(angle)*radius,
                                out var goal,maxDistance:25,reachableFrom:from,requireDirect:true)&&!goal.location.indoors&&Math.Abs(goal.y-from[1])<=150)
                            {state.Goal=new[]{goal.x,goal.y,goal.z};break;}
                        }
                        if(state.Goal==null)state.WaitUntil=now+MinimumIdleSeconds;
                    }
                    if(state.Goal==null)continue;
                    if(!Step(mob.getDistrict(),from,state.Goal,elapsed,speed,out var next)){Stop(mob,state,now);continue;}
                    double[] goalAt=state.Goal;
                    mob.setXPos(next.x);mob.setYPos(next.y);mob.setZPos(next.z);
                    mob.setRotation(MobAggro.YawTowards(from[0],from[2],goalAt[0],goalAt[2]));
                    if(state.Target==null&&Math.Pow(next.x-goalAt[0],2)+Math.Pow(next.z-goalAt[2],2)<1)
                    {
                        Stop(mob,state,now,publishArrival:true);
                        continue;
                    }
                    mob.currentState=(int)Mob.statusList.WALKING;state.Walking=true;mob.updateAnimation((byte)Mob.Animations.WALK);
                }
            }
        }
    }
}
