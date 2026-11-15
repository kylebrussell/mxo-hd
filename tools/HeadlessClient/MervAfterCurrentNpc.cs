using System.Text.Json;
using hds;
namespace HeadlessClient;
/// <summary>Package-local current-life NPC evidence; never changes views, ACKs or mission state.</summary>
public static class MervAfterCurrentNpc
{
    public sealed record Scope(int Mission,int Phase,ushort Instance,string MissionIdentity,long Started,ushort Npc,string Descriptor);
    public sealed record Point(double X,double Y,double Z);
    public sealed record CurrentView(ViewCreation View,Point Position,int CreationSection,int PositionSection,string PositionBytes,long OwnLife,bool Retained);
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static MissionProgress.SavedMob Owned(MissionProgress.SavedState state,ushort npc)
    {
        if(state.Version is not (1 or 2)||state.Mission<=0||state.Instance==0||state.Phase<0||state.Started<=0||
            !Guid.TryParseExact(state.Identity,"N",out _)||!state.Flags.GetValueOrDefault("entered")||state.Flags.GetValueOrDefault("failed"))
            throw new InvalidDataException("No current entered owned NPC mission scope");
        var actor=state.Mobs.Single(m=>m.Npc==npc);
        if(!Guid.TryParseExact(actor.Identity,"N",out _)||!new[]{actor.X,actor.Y,actor.Z}.All(double.IsFinite))throw new InvalidDataException("Invalid saved NPC UUID/pose");
        return actor;
    }
    public static Scope Capture(MissionProgress.SavedState state,ushort npc)
    {
        var actor=Owned(state,npc);
        return new(state.Mission,state.Phase,state.Instance,state.Identity,state.Started,npc,
            JsonSerializer.Serialize(new{actor.Identity,actor.Name,actor.Phase,actor.Friendly,actor.Dead,actor.X,actor.Y,actor.Z},Fields));
    }
    private sealed class LiveView
    {
        internal ViewCreation View;
        internal Point? Position;
        internal int Birth, PositionSection;
        internal string PositionBytes;
        internal bool GenerationKnown = true;
        internal bool Dead;
    }

    public static CurrentView Require(OwnViewLifeSnapshot stream, Scope scope, MissionProgress.SavedState state)
    {
        var actor=Owned(state,scope.Npc);
        if(Capture(state,scope.Npc)!=scope)throw new InvalidDataException("NPC owned mission/phase/UUID/pose changed");
        Point expected=new(actor.X,actor.Y,actor.Z);
        if (!stream.Current || !stream.OwnViewReady || stream.OwnBirthSection < 0 || stream.Events.Any(e => e.Life != stream.Life))
            throw new InvalidDataException("No current own-view life; previous-life events are inadmissible");
        var live = new Dictionary<ushort, LiveView>();
        var retired = new HashSet<(ushort View, byte Spawn)>();
        bool ownBirthSeen = false;
        foreach (var e in stream.Events)
        {
            if (e.Section < stream.OwnBirthSection) continue;
            byte[] section = e.Bytes;
            var creations = WireFormat.FindCreationEvents(section);
            int start = 0;
            if (e.Section == stream.OwnBirthSection)
            {
                var own = creations.LastOrDefault(c => c.Creation.ViewId == 2 && c.Creation.Goid == 12);
                if (own == null) throw new InvalidDataException("Current own-view birth is absent");
                ownBirthSeen = true;
                start = own.Offset + own.Length;
            }
            if (!ownBirthSeen) throw new InvalidDataException("Current own-view birth must precede carried objects");
            var known = new List<(int Start, int End, WireFormat.CreationEvent? Birth, ushort? Deleted)>();
            foreach (var creation in creations.Where(c => c.Offset >= start))
                known.Add((creation.Offset, creation.Offset + creation.Length, creation, null));
            for (int at = start; at + 7 <= section.Length; at++)
            {
                if (creations.Any(c => at >= c.Offset && at < c.Offset + c.Length)) continue;
                if (section[at] == 1 && section[at + 1] == 0 && section[at + 2] == 1 && section[at + 3] == 1 && section[at + 4] == 0)
                {
                    known.Add((at, at + 7, null, BitConverter.ToUInt16(section, at + 5)));
                    at += 6;
                }
            }
            foreach (var record in known.OrderBy(k => k.Start))
            {
                if (record.Deleted.HasValue)
                {
                    if (live.Remove(record.Deleted.Value, out var removed)) retired.Add((removed.View.ViewId, removed.View.SpawnId));
                    continue;
                }
                var view = record.Birth!.Creation;
                if (live.TryGetValue(view.ViewId, out var previous) && previous.View.SpawnId == view.SpawnId &&
                    previous.View.Goid == view.Goid && previous.View.Attributes.Count == view.Attributes.Count &&
                    previous.View.Attributes.All(a => view.Attributes.TryGetValue(a.Key, out var bytes) && bytes.SequenceEqual(a.Value)))
                    continue; // retransmitted identical birth is not a new generation or position
                if (previous != null) retired.Add((previous.View.ViewId, previous.View.SpawnId));
                Point? position = view.Attributes.TryGetValue("Position", out var p) && p.Length == 24
                    ? new Point(BitConverter.ToDouble(p), BitConverter.ToDouble(p, 8), BitConverter.ToDouble(p, 16)) : null;
                live[view.ViewId] = new LiveView { View=view, Position=position, Birth=e.Section,
                    PositionSection=e.Section, PositionBytes=p == null ? "" : Convert.ToHexString(p),
                    Dead=view.Attributes.TryGetValue("IsDead",out var dead)&&dead.Length==1&&dead[0]!=0,
                    GenerationKnown=!retired.Contains((view.ViewId, view.SpawnId)) };
            }
            bool movements = start == 0 && section.Length > 0 && section.Length % 18 == 0 &&
                Enumerable.Range(0, section.Length / 18).All(j => section[j * 18 + 2] == 2 && section[j * 18 + 3] == 0x0e);
            if (movements)
            {
                for (int at = 0; at < section.Length; at += 18)
                    if (live.TryGetValue(BitConverter.ToUInt16(section, at), out var current))
                    {
                        current.Position = new Point(BitConverter.ToSingle(section, at + 6), BitConverter.ToSingle(section, at + 10), BitConverter.ToSingle(section, at + 14));
                        current.PositionSection=e.Section; current.PositionBytes=Convert.ToHexString(section.AsSpan(at + 6, 12));
                    }
            }
            else if(start==0&&section.Length==27&&section[2]==7&&section[3]==1&&section[4]==0x1d&&
                (BitConverter.ToUInt16(section,5)==13||BitConverter.ToUInt16(section,5)==5))
            {
                if(live.TryGetValue(BitConverter.ToUInt16(section),out var current))
                {current.Position=new(BitConverter.ToSingle(section,15),BitConverter.ToSingle(section,19),BitConverter.ToSingle(section,23));
                 current.Dead=true;current.PositionSection=e.Section;current.PositionBytes=Convert.ToHexString(section.AsSpan(15,12));}
            }
            else
            {
                // Only unparsed gaps can invalidate position evidence; creation bytes are not movement records.
                var gaps = new List<byte[]>(); int at = start;
                foreach (var record in known.OrderBy(k => k.Start))
                {
                    if (record.Start > at) gaps.Add(section[at..record.Start]);
                    at = Math.Max(at, record.End);
                }
                if (at < section.Length) gaps.Add(section[at..]);
                foreach (var current in live.Values)
                    if (gaps.Any(g => BytePattern.IndexOf(g, BitConverter.GetBytes(current.View.ViewId), 0) >= 0))
                    {
                        current.Position=null; current.PositionSection=e.Section; current.PositionBytes="";
                        if (gaps.Any(g => BytePattern.IndexOf(g, new byte[] { 1, 0, 0x0c }, 0) >= 0 ||
                                          BytePattern.IndexOf(g, new byte[] { 1, 0, 8 }, 0) >= 0)) current.GenerationKnown=false;
                    }
            }
        }
        if (!ownBirthSeen) throw new InvalidDataException("No current own-view birth");
        var candidates = live.Values.Where(v => v.View.Goid == 599 && v.View.Name == actor.Name &&
            v.Position!=null && Math.Abs(v.Position.X-actor.X)<1 && Math.Abs(v.Position.Y-actor.Y)<1 && Math.Abs(v.Position.Z-actor.Z)<1).ToArray();
        if (candidates.Length != 1) throw new InvalidDataException("Expected exactly one current live carried generation");
        var selected = candidates[0];
        var v=selected.View;
        bool enemy=v.Attributes.TryGetValue("IsEnemy",out var side)&&side.Length==1&&side[0]==1;
        bool safe=v.Attributes.TryGetValue("DontAttack",out var noAttack)&&noAttack.Length==1&&noAttack[0]==1;
        byte[] expectedKey={(byte)actor.Phase,(byte)state.Instance,(byte)(state.Instance>>8),2};
        if(enemy==actor.Friendly || actor.Friendly&&!safe ||
            !v.Attributes.TryGetValue("MissionKey",out var key)||!key.SequenceEqual(expectedKey) ||
            actor.Dead!=selected.Dead)throw new InvalidDataException("NPC current side/mission key/death mismatch");
        var latest = selected.Position;
        if (!selected.GenerationKnown || latest == null || !double.IsFinite(latest.X) || !double.IsFinite(latest.Y) || !double.IsFinite(latest.Z) ||
            Math.Abs(latest.X - expected.X) >= 1 || Math.Abs(latest.Y - expected.Y) >= 1 || Math.Abs(latest.Z - expected.Z) >= 1)
            throw new InvalidDataException("Latest current live carried position does not match current owned XYZ");
        if (!stream.Current) throw new InvalidDataException("Own-view life changed while reading carried generation");
        return new CurrentView(selected.View, latest, selected.Birth, selected.PositionSection, selected.PositionBytes,
            stream.Life, selected.Birth < stream.Events.Count);
    }
}
