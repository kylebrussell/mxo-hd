using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
namespace hds
{
    /// <summary>Separately qualified loaded-parent registrations; never archive ownership.</summary>
    public static class AbilityGrantRegistration
    {
        public const ushort Parent = 356, Child = 1087;
        internal readonly struct Stamp : IEquatable<Stamp>
        {
            internal readonly int World; internal readonly long Transport;
            internal Stamp(int world,long transport){World=world;Transport=transport;}
            public bool Equals(Stamp other)=>World==other.World && Transport==other.Transport;
            public override bool Equals(object obj)=>obj is Stamp other && Equals(other);
            public override int GetHashCode()=>HashCode.Combine(World,Transport);
            public static bool operator ==(Stamp a,Stamp b)=>a.Equals(b);
            public static bool operator !=(Stamp a,Stamp b)=>!a.Equals(b);
        }
        internal readonly struct Relation : IEquatable<Relation>
        {
            internal readonly ushort Source,Target,Level;
            internal Relation(ushort source,ushort target,ushort level){Source=source;Target=target;Level=level;}
            public bool Equals(Relation other)=>Source==other.Source && Target==other.Target && Level==other.Level;
            public override bool Equals(object obj)=>obj is Relation other && Equals(other);
            public override int GetHashCode()=>HashCode.Combine(Source,Target,Level);
            public static bool operator ==(Relation a,Relation b)=>a.Equals(b);
            public static bool operator !=(Relation a,Relation b)=>!a.Equals(b);
        }
        internal sealed class Presentation
        {
            private Stamp? stamp;
            private bool ready;
            private readonly Dictionary<ushort,Relation> shown = new Dictionary<ushort,Relation>();
            internal List<byte[]> Reconcile(Stamp current, bool loadingComplete, IEnumerable<Relation> desired)
            {
                if (stamp != current) { stamp=current; ready=false; shown.Clear(); }
                if (loadingComplete) ready=true;
                var output=new List<byte[]>(); if(!ready)return output;
                // Native grants are keyed by child, not by contributor. Retain an existing contributor while valid.
                var groups=desired.Distinct().GroupBy(r=>r.Target).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>r.Source).ToArray());
                var next=groups.ToDictionary(g=>g.Key,g=>shown.TryGetValue(g.Key,out var old)&&g.Value.Contains(old)?old:g.Value[0]);
                foreach(var old in shown.Values.OrderBy(r=>r.Target))
                    if(!next.TryGetValue(old.Target,out var replacement) || replacement!=old)
                    {
                        if(!next.ContainsKey(old.Target))output.Add(ServerPackets.BuildAbilityUnload(old.Target));
                        output.Add(Record(old,true));
                    }
                foreach(var entry in next.Values.OrderBy(r=>r.Target))
                    if(!shown.TryGetValue(entry.Target,out var old) || old!=entry)
                    {
                        output.Add(ServerPackets.BuildAbilityLoad(entry.Target,entry.Level,AbilityProgression.HiddenSlot));
                        output.Add(Record(entry,false));
                    }
                shown.Clear();foreach(var entry in next)shown.Add(entry.Key,entry.Value);
                return output;
            }
        }
        private sealed class Holder
        {
            internal readonly Presentation State=new Presentation();
            internal readonly AbilityShadowPresentation.Presentation Shadow=new AbilityShadowPresentation.Presentation();
        }
        private static readonly ConditionalWeakTable<WorldClient,Holder> holders=new ConditionalWeakTable<WorldClient,Holder>();
        // Historical 25 capture-qualified zero pairs stay separate from scaled Sneak and native-qualified tools.
        internal static IReadOnlyList<(ushort Source,ushort Target)> ObservedPairs { get; } = Array.AsReadOnly(new (ushort Source,ushort Target)[] {
            (51,1094),
            (64,1078),
            (67,1085),
            (83,1082),
            (92,1089),
            (98,1077),
            (137,1062),
            (143,1060),
            (154,1061),
            (236,1097),
            (257,1063),
            (263,1065),
            (270,1067),
            (310,1083),
            (334,1084),
            (335,1086),
            (356,1087),
            (369,1092),
            (390,1079),
            (410,1069),
            (426,1064),
            (439,1093),
            (469,1088),
            (490,1091),
            (533,1066)
        });
        internal static IReadOnlyList<(ushort Source,ushort Target)> ObservedScaledPairs { get; } = Array.AsReadOnly(
            new (ushort Source,ushort Target)[]{(AbilitySneakGrant.Parent,AbilitySneakGrant.Child)});
        // Stored175 grants1116; native tool admission reads the live actor's grant registry, not Margin alone.
        // Primary contract: research/abilities/invisibility-tool-registration.md. No new level formula.
        internal static IReadOnlyList<(ushort Source,ushort Target)> NativeToolPairs { get; } =
            Array.AsReadOnly(new (ushort Source,ushort Target)[] { (175,1116) });
        // Publish source-qualified zero grants from the GOB through the loaded registry.
        private static readonly (ushort Source, ushort Target)[] UtilityMasterPairs = { (620, 1108) };
        private static readonly (ushort Source, ushort Target)[] SpyClassPairs = { (155, 1113) };
        private static readonly (ushort Source, ushort Target)[] PhysicianPairs = { (418, 1098) };
        private static readonly (ushort Source, ushort Target)[] UpgradeArtistPairs = { (318, 1095) };
        private static readonly (ushort Source, ushort Target)[] RavagerAndUpgradeMasterPairs = { (470, 1081), (425, 1096) };
        internal static IEnumerable<(ushort Source,ushort Target)> RegisteredPairs =>
            ObservedPairs.Concat(NativeToolPairs).Concat(UtilityMasterPairs).Concat(SpyClassPairs)
                .Concat(PhysicianPairs).Concat(UpgradeArtistPairs).Concat(RavagerAndUpgradeMasterPairs);
        // Separate late live-base candidate; Type45 bookkeeping alone is not native Actions visibility proof.
        internal static IReadOnlyList<(ushort Source,ushort Target)> ReadyPresentationPairs { get; } =
            Array.AsReadOnly(new (ushort Source,ushort Target)[]{(290,1117)});
        public static bool Handles(ushort parent,ushort child)
        {
            parent=AbilityCatalog.Canonical(parent);child=AbilityCatalog.Canonical(child);
            if(AbilitySneakGrant.Matches(parent,child))return true;
            if(!RegisteredPairs.Concat(ReadyPresentationPairs).Any(p=>p.Source==parent && p.Target==child))return false;
            return ZeroGrant(AbilityCatalog.Find(parent),child);
        }
        internal static bool ZeroGrant(AbilityCatalog.Entry parent,ushort child)
        {
            var modifiers=parent?.Modifiers.Where(m=>
                AbilityCatalog.IdOf((uint)m.Number("TargetAbilityGrant"))==child &&
                m.Number("WhenEnabled",-1)==0 && m.Number("DurationType",-1)==2).ToArray();
            return modifiers?.Length==1 && modifiers[0].Number("ValueFunction")==0 &&
                modifiers[0].Number("MinValue")==0 && modifiers[0].Number("MaxValue")==0 &&
                AbilityValues.TryValue(modifiers[0],0,0,out int value) && value==0;
        }
        // Literal80bc45, locator(child<<8)|3, source, child, grant-level, add/remove, duration0.
        internal static byte[] Record(Relation relation,bool off)
        {
            var p=new PacketContent();p.AddHexBytes("80bc45");p.AddUint32(((uint)relation.Target<<8)|3,1);
            p.AddUint32(relation.Source,1);p.AddUint16(relation.Target,1);p.AddUint32(relation.Level,1);
            p.AddByte(off?(byte)1:(byte)0);p.AddFloat(0,1);return p.ReturnFinalPacket();
        }
        public static void LoadedChanged(WorldClient client)=>Reconcile(client,false);
        public static void LoadingComplete(WorldClient client,byte[] body)
        {
            // NativeOnEnterWorld6216cc56..78 sends exactly0c01 AFTER live avatar/inventory initialization.
            if(body?.Length==1 && body[0]==1)Reconcile(client,true);
        }
        private static void Reconcile(WorldClient client,bool complete)
        {
            if(client?.playerData==null || client.TransportFence==null || client.messageQueue==null)return;
            Apply(client,complete,new Stamp(client.playerData.worldGeneration,client.TransportFence.Capture()));
        }
        internal static void Apply(WorldClient client,bool complete,Stamp stamp)
        {
            var data=client.playerData;var holder=holders.GetOrCreateValue(client);
            lock(holder)
                client.TransportFence.TryRun(stamp.Transport,()=>
                {
                    if(!ReferenceEquals(client.playerData,data)||data.worldGeneration!=stamp.World || !data.getOnWorld() || data.worldChangePending ||
                       data.waitForRPCShutDown || data.DistrictIsLA())return;
                    lock(data.passives)
                    {
                        var desired=RegisteredPairs.Where(p=>data.passives.Loaded.ContainsKey(p.Source) && Handles(p.Source,p.Target))
                            .Select(p=>new Relation(p.Source,p.Target,0));
                        foreach(var pair in ObservedScaledPairs)
                            if(data.passives.Loaded.TryGetValue(pair.Source,out ushort learned)&&AbilitySneakGrant.TryLevel(pair.Source,pair.Target,learned,out ushort level))
                                desired=desired.Append(new Relation(pair.Source,pair.Target,level));
                        foreach(var packet in holder.State.Reconcile(stamp,complete,desired))client.messageQueue.AddWorldBoundRpc(packet,stamp.World,stamp.Transport);
                        if(Handles(AbilityShadowPresentation.Parent,AbilityShadowPresentation.Child))
                        {
                            var shadow=new AbilityShadowPresentation.Snapshot(data,data.passives.Loaded.ContainsKey(AbilityShadowPresentation.Parent),
                                data.passives.ShadowOwned,data.passives.ShadowEmitted);
                            foreach(var packet in holder.Shadow.Reconcile(data,stamp,complete,shadow))client.messageQueue.AddWorldBoundRpc(packet,stamp.World,stamp.Transport);
                        }
                        data.passives.ShadowEmitted=default;
                    }
                });
        }
    }
}
