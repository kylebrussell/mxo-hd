using hds;
namespace HeadlessClient;
/// <summary>Current-life ambient observer using qualified typed frames, without resynchronization.</summary>
public static class OpenWorldCurrentNpc
{
    public sealed record Current(ViewCreation View,double[] Position,bool Dead,long Life);
    sealed class LiveView { public ViewCreation View=null!; public Point? Position; public int Birth,PositionSection; public string PositionBytes=""; public bool DynamicBirth,Dead,GenerationKnown; }
    sealed record Point(double X,double Y,double Z);
    public static Current Require(OwnViewLifeSnapshot stream,double x,double y,double z,bool dead=false)
    {
        if(!stream.Current||!stream.OwnViewReady||stream.OwnBirthSection<0||stream.Events.Any(e=>e.Life!=stream.Life))throw new InvalidDataException("No current own life");
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
            if (!ownBirthSeen) throw new InvalidDataException("Current own-view birth must precede ambient objects");
            int cursor=start;
            while(cursor<section.Length)
            {
                var birth=creations.FirstOrDefault(c=>c.Offset==cursor);
                if(birth!=null)
                {
                    var view=birth.Creation;
                    if(live.TryGetValue(view.ViewId,out var previous)&&previous.View.SpawnId==view.SpawnId&&
                        previous.View.Goid==view.Goid&&previous.View.Attributes.Count==view.Attributes.Count&&
                        previous.View.Attributes.All(a=>view.Attributes.TryGetValue(a.Key,out var bytes)&&bytes.SequenceEqual(a.Value)))
                    {cursor+=birth.Length;continue;}
                    if(previous!=null)retired.Add((previous.View.ViewId,previous.View.SpawnId));
                    Point? position=view.Attributes.TryGetValue("Position",out var p)&&p.Length==24
                        ?new(BitConverter.ToDouble(p),BitConverter.ToDouble(p,8),BitConverter.ToDouble(p,16)):null;
                    live[view.ViewId]=new LiveView {View=view,Position=position,Birth=e.Section,PositionSection=e.Section,
                        PositionBytes=p==null?"":Convert.ToHexString(p),DynamicBirth=section[cursor+2]==0x0c,
                        Dead=view.Attributes.TryGetValue("IsDead",out var dead)&&dead.Length==1&&dead[0]!=0,
                        GenerationKnown=!retired.Contains((view.ViewId,view.SpawnId))};
                    cursor+=birth.Length;continue;
                }
                ushort? goid=cursor+2<=section.Length&&live.TryGetValue(BitConverter.ToUInt16(section,cursor),out var addressed)
                    ?addressed.View.Goid:cursor+2<=section.Length&&BitConverter.ToUInt16(section,cursor)==2?(ushort)12:null;
                if(!MervFollowupViewFrames.TryRead(section,cursor,goid,out var frame))
                {
                    // An ambiguous current-life stream cannot be repaired by a later NPC birth.
                    // Only a wholly new qualified own life starts a separate observation.
                    throw new InvalidDataException($"Unknown/truncated current-life frame at section{e.Section} offset{cursor}");
                }
                if(frame.Type==MervFollowupViewFrames.Kind.Delete)
                {if(live.Remove(frame.View,out var removed))retired.Add((removed.View.ViewId,removed.View.SpawnId));}
                else if(live.TryGetValue(frame.View,out var current))
                {
                    if(frame.PositionOffset>=0)
                    {
                        int pos=cursor+frame.PositionOffset;
                        current.Position=new(BitConverter.ToSingle(section,pos),BitConverter.ToSingle(section,pos+4),BitConverter.ToSingle(section,pos+8));
                        current.PositionSection=e.Section;current.PositionBytes=Convert.ToHexString(section.AsSpan(pos,12));
                    }
                    if(frame.Type==MervFollowupViewFrames.Kind.Death)current.Dead=true;
                }
                cursor+=frame.Length;
            }
        }
        if (!ownBirthSeen) throw new InvalidDataException("No current own-view birth");
        var candidates=live.Values.Where(v=>v.View.Goid==599&&v.View.Name=="Chopper Runner"&&v.DynamicBirth&&v.GenerationKnown&&v.View.Attributes.TryGetValue("Position",out var raw)&&raw.Length==24&&Math.Pow(BitConverter.ToDouble(raw)-x,2)+Math.Pow(BitConverter.ToDouble(raw,16)-z,2)<=250000&&Math.Abs(BitConverter.ToDouble(raw,8)-y)<1&&v.View.Attributes.TryGetValue("MoreInfoID",out var rsi)&&rsi.SequenceEqual(Convert.FromHexString(x==19403?"5E010088":"5D010088"))).ToArray();
        if(candidates.Length!=1)throw new InvalidDataException("Expected one original ambient generation");
        var chosen=candidates[0];var view=chosen.View;var pos=chosen.Position;
        if(pos==null||!new[]{pos.X,pos.Y,pos.Z}.All(double.IsFinite)||chosen.Dead!=dead||!view.Attributes.TryGetValue("Level",out var level)||!level.SequenceEqual(new byte[]{1})||view.Attributes.ContainsKey("MissionKey")||!stream.Current)throw new InvalidDataException("Ambient level/death/life/mission scope differs");
        return new(view,new[]{pos.X,pos.Y,pos.Z},chosen.Dead,stream.Life);
    }
}
