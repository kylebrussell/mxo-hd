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
        internal sealed class Input
        {
            internal ushort ObjectId {get;} internal G.Request Request {get;}
            internal Input(ushort objectId,G.Request request){ObjectId=objectId;Request=request;}
        }
        internal sealed class Plan
        {
            internal IReadOnlyDictionary<ushort,G.Result> Results {get;} internal IReadOnlyList<ushort> Order {get;} internal int Attempts {get;} internal long Milliseconds {get;}
            internal Plan(IDictionary<ushort,G.Result> results,IEnumerable<ushort> order,int attempts,long milliseconds)
            {Results=new ReadOnlyDictionary<ushort,G.Result>(new Dictionary<ushort,G.Result>(results));Order=Array.AsReadOnly(order.ToArray());Attempts=attempts;Milliseconds=milliseconds;}
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
        {
            plan=null;refusal="NoWholePhasePlacement";
            if(inputs==null||inputs.Count==0||inputs.Any(i=>i?.Request==null)||inputs.Select(i=>i.ObjectId).Distinct().Count()!=inputs.Count)return false;
            var watch=Stopwatch.StartNew();int attempts=0;
            var candidates=new Dictionary<ushort,IReadOnlyList<G.BodyCandidate>>();
            foreach(var input in inputs)
            {
                var values=G.BodyCandidates(input.Request,out refusal);if(values.Count==0)return false;candidates.Add(input.ObjectId,values);
            }
            string lastRefusal="NoDisjointBodySet";G.Result[] accepted=null;
            foreach(bool rootWave in new[]{true,false})foreach(var order in Orders(inputs.Count))
            {
                attempts++;var bodies=new G.BodyCandidate[inputs.Count];
                bool Search(int depth)
                {
                    if(depth==order.Length)
                    {
                        if(G.RouteBodySet(bodies,out var result,out var reason)){accepted=result;return true;}
                        lastRefusal=reason;return false;
                    }
                    int index=order[depth];
                    foreach(var body in candidates[inputs[index].ObjectId])
                    {
                        if(rootWave&&!body.RootReachableCentre)continue;
                        if(order.Take(depth).Any(i=>!G.BodiesClear(body,bodies[i])))continue;
                        bodies[index]=body;if(Search(depth+1))return true;
                    }
                    bodies[index]=null;return false;
                }
                if(Search(0))
                {
                    plan=new Plan(inputs.Select((input,i)=>(input.ObjectId,Result:accepted[i])).ToDictionary(x=>x.ObjectId,x=>x.Result),order.Select(i=>inputs[i].ObjectId),attempts,watch.ElapsedMilliseconds);return true;
                }
            }
            refusal="NoWholePhasePlacement:"+lastRefusal+";orders="+attempts;return false;
        }
    }
}
