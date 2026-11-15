using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>OURS order-independent server interpretation of explicit native load batches; no implicit mutations.</summary>
    internal static class AbilityLoadQualification
    {
        internal static string Refusal(IReadOnlyList<AbilityMemory.Row> rows,IReadOnlyList<AbilityMemory.Row> accepted,
            long experience,out bool levelRefused,AbilityPrerequisites.Graph graph=null)
        {
            levelRefused=false;graph=graph??AbilityPrerequisites.Current;
            bool Real(AbilityMemory.Row r)=>r.Slot<520 && AbilityMemory.IsAbility(r.Descriptor) && graph.Find(r.Ability)!=null;
            var current=rows.Where(r=>r.Loaded&&Real(r)).ToArray();
            var currentIds=current.Select(r=>r.Ability).ToHashSet();
            var additions=accepted.Where(r=>Real(r)&&!currentIds.Contains(r.Ability)).ToArray();
            var added=additions.Select(r=>r.Ability).ToHashSet();
            if(added.Count==0)return null; //Legacy invalid graphs/no-op requests are not repaired or blocked.
            var closures=added.ToDictionary(id=>id,id=>graph.Closure(id));
            if(added.Any(id=>closures[id].ContainsKey(id)))return "Cannot load a cyclic ability prerequisite batch.";
            var roots=added.Where(id=>!added.Any(other=>other!=id&&closures[other].ContainsKey(id))).ToArray();
            if(roots.Length==0)return "Cannot identify a valid ability prerequisite root.";
            int characterLevel=PlayerProgression.LevelForExperience(1,experience);
            foreach(ushort root in roots)
                if(graph.Find(root).CharacterLevel>characterLevel)
                {levelRefused=true;return "Your character level is too low to load that ability.";}
            var parents=new Dictionary<ushort,int>();
            foreach(ushort root in roots)foreach(var edge in closures[root])
                if(!parents.TryGetValue(edge.Key,out int old)||edge.Value>old)parents[edge.Key]=edge.Value;
            if(added.Any(id=>!roots.Contains(id)&&!parents.ContainsKey(id)))return "Cannot qualify every new ability in this batch.";
            //Native auto-included archived parents have a training cap, not their own selected-root ReqCharLevel gate.
            foreach(var row in additions)
                if(!roots.Contains(row.Ability)&&row.Level>characterLevel)
                    return "An archived prerequisite is trained above your character level.";
            var final=current.Concat(accepted.Where(Real)).ToArray();
            foreach(var edge in parents)
                if(!final.Any(r=>r.Ability==edge.Key && unchecked((short)r.Level)>=edge.Value))
                    return "Load the required owned prerequisite abilities at their required training levels.";
            return null;
        }
    }
}
