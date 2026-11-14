using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using G=hds.MissionOpenFloorResolver;
namespace hds
{
    internal static class MissionOpenFloorPhasePlanner
    {
        // OURS computational admission policy, not a retail geometry or feasibility claim.
        internal const int SearchPolicyVersion=2;
        internal sealed class Limits
        {
            internal int BodyNodes {get;} internal int RouteGraphs {get;}
            internal Limits(int bodyNodes,int routeGraphs){if(bodyNodes<1||routeGraphs<1)throw new ArgumentOutOfRangeException();BodyNodes=bodyNodes;RouteGraphs=routeGraphs;}
            internal static readonly Limits Default=new Limits(4096,32);
        }
        internal sealed class Work {internal int BodyNodes,RouteGraphs,StaticRootGraphs;}
        internal sealed class Input
        {
            internal ushort ObjectId {get;} internal G.Request Request {get;}
            internal Input(ushort objectId,G.Request request){ObjectId=objectId;Request=request;}
        }
        internal sealed class Plan
        {
            internal IReadOnlyDictionary<ushort,G.Result> Results {get;} internal IReadOnlyList<ushort> Order {get;} internal int Attempts {get;} internal long Milliseconds {get;}
            internal long BodyNodes {get;} internal long RouteGraphs {get;}
            internal int StaticRootGraphs {get;} internal bool DirectSingle {get;}
            internal Plan(IDictionary<ushort,G.Result> results,IEnumerable<ushort> order,int attempts,long milliseconds,long bodyNodes,long routeGraphs,int staticRootGraphs=0,bool directSingle=false)
            {Results=new ReadOnlyDictionary<ushort,G.Result>(new Dictionary<ushort,G.Result>(results));Order=Array.AsReadOnly(order.ToArray());Attempts=attempts;Milliseconds=milliseconds;BodyNodes=bodyNodes;RouteGraphs=routeGraphs;StaticRootGraphs=staticRootGraphs;DirectSingle=directSingle;}
        }
        // Bounded deterministic search family: every authored cyclic rotation, then reverse rotations.
        // At most2n orders per root/all-candidate wave, first feasible. No source/site reroll, timeout, margin relaxation or post-admission repacking.
        internal static IEnumerable<int[]> Orders(int count)
        {
            if(count<1)yield break;var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(bool reverse in new[]{false,true})for(int shift=0;shift<count;shift++)
            {
                var order=Enumerable.Range(0,count).Select(i=>(shift+(reverse?count-i:i))%count).ToArray();
                if(seen.Add(string.Join(",",order)))yield return order;
            }
        }
        internal static bool TryPlan(IReadOnlyList<Input> inputs,out Plan plan,out string refusal)
            =>TryPlan(inputs,out plan,out refusal,out _,Limits.Default);
        internal static bool TryPlan(IReadOnlyList<Input> inputs,out Plan plan,out string refusal,out Work work,Limits limits,bool directSingle=true)
        {
            work=new Work();var measured=work;if(limits==null)throw new ArgumentNullException(nameof(limits));
            plan=null;refusal="NoWholePhasePlacement";
            if(inputs==null||inputs.Count==0||inputs.Any(i=>i?.Request==null)||inputs.Select(i=>i.ObjectId).Distinct().Count()!=inputs.Count)return false;
            var watch=Stopwatch.StartNew();int attempts=0;
            if(inputs.Count==1&&directSingle)
            {
                var input=inputs[0];var direct=G.Plan(input.Request,limits.BodyNodes,limits.RouteGraphs,out int nodes,out int graphs);
                measured.BodyNodes=nodes;measured.RouteGraphs=graphs;
                if(direct.HasPlan&&G.ValidateRouteIds(input.Request,direct.RoutePolygons,direct.Route)&&G.ValidateSet(new[]{direct},out _))
                {
                    plan=new Plan(new Dictionary<ushort,G.Result>{{input.ObjectId,direct}},new[]{input.ObjectId},1,watch.ElapsedMilliseconds,nodes,graphs,0,true);refusal=null;return true;
                }
                if(direct.Refusal?.StartsWith("SearchExhausted:",StringComparison.Ordinal)==true){refusal=direct.Refusal;return false;}
            }
            var candidates=new Dictionary<ushort,IReadOnlyList<G.BodyCandidate>>();
            foreach(var input in inputs)
            {
                var values=G.BodyCandidates(input.Request,out refusal,out bool graphBuilt);if(graphBuilt)measured.StaticRootGraphs++;
                if(values.Count==0){refusal=refusal??"NoGeometricBodyCandidates";return false;}candidates.Add(input.ObjectId,values);
            }
            string lastRefusal="NoDisjointBodySet",exhausted=null;G.Result[] accepted=null;
            foreach(bool rootWave in new[]{true,false})foreach(var order in Orders(inputs.Count))
            {
                attempts++;var bodies=new G.BodyCandidate[inputs.Count];
                bool Search(int depth)
                {
                    if(depth==order.Length)
                    {
                        if(measured.RouteGraphs>=limits.RouteGraphs){exhausted="SearchExhausted:RouteGraphs";return false;}measured.RouteGraphs++;
                        if(G.RouteBodySet(bodies,out var result,out var reason)){accepted=result;return true;}
                        lastRefusal=reason;return false;
                    }
                    int index=order[depth];
                    foreach(var body in candidates[inputs[index].ObjectId])
                    {
                        if(rootWave&&!body.RootReachableCentre)continue;
                        if(measured.BodyNodes>=limits.BodyNodes){exhausted="SearchExhausted:BodyNodes";return false;}measured.BodyNodes++;
                        if(order.Take(depth).Any(i=>!G.BodiesClear(body,bodies[i])))continue;
                        bodies[index]=body;if(Search(depth+1))return true;if(exhausted!=null)return false;
                    }
                    bodies[index]=null;return false;
                }
                if(Search(0))
                {
                    plan=new Plan(inputs.Select((input,i)=>(input.ObjectId,Result:accepted[i])).ToDictionary(x=>x.ObjectId,x=>x.Result),order.Select(i=>inputs[i].ObjectId),attempts,watch.ElapsedMilliseconds,measured.BodyNodes,measured.RouteGraphs,measured.StaticRootGraphs);return true;
                }
                if(exhausted!=null){refusal=exhausted;return false;}
            }
            refusal="NoWholePhasePlacement:"+lastRefusal+";orders="+attempts;return false;
        }
    }
}
