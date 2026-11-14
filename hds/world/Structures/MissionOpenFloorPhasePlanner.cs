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
        // At most2n orders, first feasible. No source/site reroll, timeout, margin relaxation or post-admission repacking.
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
            foreach(var order in Orders(inputs.Count))
            {
                attempts++;var bodies=new Dictionary<ushort,G.Result>();var prefix=new List<G.Reservation>();bool complete=true;
                foreach(int index in order)
                {
                    var input=inputs[index];var result=G.Plan(G.WithReservations(input.Request,prefix));
                    if(!result.HasPlan){refusal=result.Refusal;complete=false;break;}
                    bodies.Add(input.ObjectId,result);prefix.Add(G.Reservation.From(result,input.ObjectId,false));
                }
                if(!complete)continue;
                var final=new Dictionary<ushort,G.Result>();
                foreach(var input in inputs)
                {
                    var body=bodies[input.ObjectId];var other=inputs.Where(i=>i.ObjectId!=input.ObjectId).Select(i=>G.Reservation.From(bodies[i.ObjectId],i.ObjectId,false)).ToArray();
                    if(!G.RerouteFixedBody(input.Request,body.Surface,body.Centre,other,out var result,out refusal)){complete=false;break;}
                    final.Add(input.ObjectId,result);
                }
                if(complete&&G.ValidateSet(inputs.Select(i=>final[i.ObjectId]).ToArray(),out refusal))
                {plan=new Plan(final,order.Select(i=>inputs[i].ObjectId),attempts,watch.ElapsedMilliseconds);return true;}
            }
            refusal="NoWholePhasePlacement:"+refusal+";orders="+attempts+";ms="+watch.ElapsedMilliseconds;return false;
        }
    }
}
