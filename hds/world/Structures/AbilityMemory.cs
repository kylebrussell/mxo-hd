using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>Native memory costs/capacity, with explicit OURS server batch admission policy.</summary>
    public static class AbilityMemory
    {
        private static readonly int[] Capacity = { 0,16,24,27,30,33,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,
            51,52,53,54,55,56,57,58,59,60,62,64,66,68,70,72,74,76,78,80,82,84,86,88,90,92,94,96,98,100 };
        public static int ForLevel(int level) => Capacity[Math.Clamp(level,1,50)];
        public static int ForExperience(long experience) => ForLevel(PlayerProgression.LevelForExperience(1,experience));
        // Exclude code-storage and other descriptor families BEFORE extracting an ability ID.
        public static bool IsAbility(uint descriptor) => (descriptor & 0xfc000000u) == 0x80000000u;
        public static int Cost(ushort id) => (int)(AbilityCatalog.Find(id)?.Number("Ability/InventoryMemCost") ?? 0);
        public sealed class Row
        {
            public uint Key { get; }
            public ushort Slot { get; }
            public uint Descriptor { get; }
            public ushort Level { get; }
            public bool Loaded { get; }
            public Row(uint key,ushort slot,uint descriptor,ushort level,bool loaded)
            { Key=key; Slot=slot; Descriptor=descriptor; Level=level; Loaded=loaded; }
            public Row WithLoaded(bool loaded) => new Row(Key,Slot,Descriptor,Level,loaded);
            public ushort Ability => AbilityCatalog.Canonical(AbilityCatalog.IdOf(Descriptor));
        }
        public sealed class Result
        {
            public Row[] Accepted { get; }
            public Row[] Loaded { get; }
            public string Refusal { get; }
            public int Used { get; }
            public int Maximum { get; }
            public bool LevelRefused { get; }
            public Result(Row[] accepted,Row[] loaded,string refusal,int used,int maximum,bool levelRefused)
            { Accepted=accepted; Loaded=loaded; Refusal=refusal; Used=used; Maximum=maximum; LevelRefused=levelRefused; }
        }
        public static HashSet<ushort> Membership(IEnumerable<Row> loaded)
        {
            var ids=loaded.Where(r=>IsAbility(r.Descriptor)).Select(r=>r.Ability).ToHashSet();
            var pending=new Queue<ushort>(ids);
            while(pending.Count>0) foreach(ushort grant in AbilityProgression.GrantsOf(pending.Dequeue()))
                if(ids.Add(grant)) pending.Enqueue(grant);
            return ids;
        }
        public static Result Plan(IReadOnlyList<Row> rows, IReadOnlyList<AbilityProgression.LoadoutEntry> requested,
            bool unload, long experience, int characterLevel)
        {
            var accepted=new List<Row>();
            bool levelRefused=false;
            foreach(var request in requested)
            {
                // The archive has no unique-slot constraint. Never update/adopt an ambiguous slot.
                var slot=rows.Where(r=>r.Slot==request.Slot).ToArray();
                if(request.Slot>=AbilityProgression.HiddenSlot || slot.Length!=1) continue;
                var row=slot[0];
                if(!IsAbility(row.Descriptor) || row.Ability!=AbilityCatalog.Canonical(request.Ability)) continue;
                if(!unload && AbilityCatalog.Find(row.Ability)==null) { levelRefused=true; continue; }
                if(!accepted.Any(r=>r.Key==row.Key)) accepted.Add(row);
            }
            var loaded=rows.Where(r=>r.Loaded).ToArray();
            var current=loaded.Where(r=>IsAbility(r.Descriptor) && AbilityCatalog.Find(r.Ability)!=null)
                .Select(r=>r.Ability).ToHashSet();
            int used=current.Sum(Cost),maximum=ForExperience(experience);
            if(!unload)
            {
                string qualification=AbilityLoadQualification.Refusal(rows,accepted,experience,out bool rootLevelRefused);
                levelRefused|=rootLevelRefused;
                if(qualification!=null)return new Result(Array.Empty<Row>(),loaded,qualification,used,maximum,levelRefused);
            }
            bool positive=accepted.Any(r=>!current.Contains(r.Ability) && Cost(r.Ability)>0);
            var desired=new HashSet<ushort>(current);
            foreach(var row in accepted) { if(unload) desired.Remove(row.Ability); else desired.Add(row.Ability); }
            bool unknown=loaded.Any(r=>IsAbility(r.Descriptor) && AbilityCatalog.Find(r.Ability)==null);
            string refusal=!unload && positive && (unknown || desired.Sum(Cost)>maximum)
                ? unknown ? "Cannot load additional memory while an unknown loaded ability exists. Unload that ability first."
                    : "The requested abilities exceed your memory capacity." : null;
            if(refusal!=null) return new Result(Array.Empty<Row>(),loaded,refusal,used,maximum,levelRefused);
            var keys=accepted.Select(r=>r.Key).ToHashSet();
            var next=rows.Select(r=>keys.Contains(r.Key)?r.WithLoaded(!unload):r).Where(r=>r.Loaded).ToArray();
            return new Result(accepted.ToArray(),next,null,next.Where(r=>IsAbility(r.Descriptor) && AbilityCatalog.Find(r.Ability)!=null).Select(r=>r.Ability).Distinct().Sum(Cost),maximum,levelRefused);
        }
    }
}
