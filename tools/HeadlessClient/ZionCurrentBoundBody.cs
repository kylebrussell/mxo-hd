using System.Text.Json;
using hds;
namespace HeadlessClient;

/// <summary>Read-only qualification of one already-published bound mission body in the current own-view life.</summary>
public static class ZionCurrentBoundBody
{
    public sealed record Scope(int Mission,int Phase,ushort Instance,string Identity,long Started,ushort ObjectId,string Descriptor,string Site);
    public sealed record Result(ViewCreation View,long OwnLife,int CreationSection,uint StaticBinding,string RawCreation);
    private sealed class Live
    {
        internal required ViewCreation View;
        internal required string Raw;
        internal uint? Binding;
        internal int Birth;
        internal bool Known=true;
    }
    private static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    private static string Descriptor(MissionProgress.SavedObject o)=>JsonSerializer.Serialize(new{o.Id,o.Goid,o.Spot,o.OpenFloor},Fields);
    private static string Site(MissionProgress.SavedState s)=>JsonSerializer.Serialize(new{s.SiteDoors,s.OpenFloorSites,s.OpenFloorCurrentSiteIndex,s.Waypoints,s.Exits},Fields);
    private static MissionProgress.SavedObject Bound(MissionProgress.SavedState s,ushort id)
    {
        if(s.Version!=1||s.Mission<=0||s.Phase<0||s.Instance==0||s.Started<=0||s.Flags==null||!Guid.TryParseExact(s.Identity,"N",out _)||
            !s.Flags.GetValueOrDefault("entered")||s.Flags.GetValueOrDefault("failed"))throw new InvalidDataException("No current entered owned mission scope");
        var o=s.Objects.Single(x=>x.Id==id);
        if(o.OpenFloor!=null||o.Spot?.type==null||(o.Spot.type.Length!=2&&o.Spot.type.Length!=4)||BitConverter.ToUInt16(o.Spot.type)!=o.Goid||(o.Spot.mxoStaticId==0||o.Spot.mxoStaticId==uint.MaxValue)||
            !new[]{o.Spot.pos_x,o.Spot.pos_y,o.Spot.pos_z}.All(double.IsFinite)||o.Spot.quat?.Length!=32)
            throw new InvalidDataException("Current bound native source required; missing free witness is never admissible");
        _=Convert.FromHexString(o.Spot.quat);return o;
    }
    public static Scope Capture(MissionProgress.SavedState state,ushort id)
    {var o=Bound(state,id);return new(state.Mission,state.Phase,state.Instance,state.Identity,state.Started,id,Descriptor(o),Site(state));}

    public static Result Require(OwnViewLifeSnapshot stream,Scope scope,MissionProgress.SavedState current)
    {
        var o=Bound(current,scope.ObjectId);
        if(current.Mission!=scope.Mission||current.Phase!=scope.Phase||current.Instance!=scope.Instance||current.Identity!=scope.Identity||current.Started!=scope.Started||
            Descriptor(o)!=scope.Descriptor||Site(current)!=scope.Site)throw new InvalidDataException("Bound mission/phase/source/site scope changed");
        if(!stream.Current||!stream.OwnViewReady||stream.OwnBirthSection<0||stream.Events.Any(e=>e.Life!=stream.Life))throw new InvalidDataException("No current own-view life");
        var live=new Dictionary<ushort,Live>();var retired=new HashSet<(ushort,byte)>();bool ownSeen=false;
        foreach(var e in stream.Events)
        {
            if(e.Section<stream.OwnBirthSection)continue;
            byte[] bytes=e.Bytes;var creations=WireFormat.FindCreationEvents(bytes);int at=0;
            if(e.Section==stream.OwnBirthSection)
            {
                var own=creations.LastOrDefault(c=>c.Creation.ViewId==2&&c.Creation.Goid==12)??throw new InvalidDataException("Actual current own2 birth absent");
                at=own.Offset+own.Length;ownSeen=true;
            }
            if(!ownSeen)throw new InvalidDataException("Own2 must precede mission bodies");
            while(at<bytes.Length)
            {
                var birth=creations.FirstOrDefault(c=>c.Offset==at);
                if(birth!=null)
                {
                    var v=birth.Creation;string raw=Convert.ToHexString(bytes.AsSpan(at,birth.Length));
                    if(!live.TryGetValue(v.ViewId,out var previous)||previous.Raw!=raw)
                    {
                        if(previous!=null)retired.Add((previous.View.ViewId,previous.View.SpawnId));
                        live[v.ViewId]=new Live{View=v,Raw=raw,Binding=bytes[at+2]==8?BitConverter.ToUInt32(bytes,at+5):null,
                            Birth=e.Section,Known=!retired.Contains((v.ViewId,v.SpawnId))};
                    }
                    at+=birth.Length;continue;
                }
                if(at+7<=bytes.Length&&bytes.AsSpan(at,5).SequenceEqual(new byte[]{1,0,1,1,0}))
                {if(live.Remove(BitConverter.ToUInt16(bytes,at+5),out var removed))retired.Add((removed.View.ViewId,removed.View.SpawnId));at+=7;continue;}
                // Exact existing PacketsUtils.createTeleportPacket layout. A teleport-id byte equal to a furniture
                // view id is not a furniture record. This observes the producer; it changes no normal03 policy.
                if(at+28<=bytes.Length&&bytes.AsSpan(at,4).SequenceEqual(new byte[]{2,0,1,0x1c})){at+=28;continue;}
                if(at+18<=bytes.Length&&bytes[at+2]==2&&bytes[at+3]==0x0e)
                {if(live.TryGetValue(BitConverter.ToUInt16(bytes,at),out var moving)&&moving.View.Goid!=599)moving.Known=false;at+=18;continue;}
                if(at+2<=bytes.Length&&live.TryGetValue(BitConverter.ToUInt16(bytes,at),out var target))
                {
                    var state=ObjectStates.BuildStateUpdate(target.View.Goid,0);
                    if(state!=null&&state.Length>=4&&at+2+state.Length<=bytes.Length&&bytes.AsSpan(at+2,state.Length-4).SequenceEqual(state.AsSpan(0,state.Length-4)))
                    {at+=2+state.Length;continue;}
                }
                int next=creations.Where(c=>c.Offset>at).Select(c=>c.Offset).DefaultIfEmpty(bytes.Length).Min();
                var gap=bytes[at..next];
                foreach(var v in live.Values)if(BytePattern.IndexOf(gap,BitConverter.GetBytes(v.View.ViewId),0)>=0)v.Known=false;
                at=next;
            }
        }
        if(!ownSeen)throw new InvalidDataException("No current own2 birth");
        var candidates=live.Values.Where(v=>v.Binding==o.Spot.mxoStaticId).ToArray();
        if(candidates.Length!=1)throw new InvalidDataException("Expected exactly one live current source binding");
        var selected=candidates[0];var view=selected.View;
        // The raw source stays immutable; native bound326 creation uses its audited bounds centre.
        var pose=MissionObjectPoses.For(new MissionObjectState{goid=o.Goid,spot=o.Spot});
        byte[] expectedPosition=new[]{pose.X,pose.Y,pose.Z}.SelectMany(BitConverter.GetBytes).ToArray();
        byte[] expectedKey={0xff,(byte)scope.Instance,(byte)(scope.Instance>>8),2};
        if(!selected.Known||view.Goid!=o.Goid||!view.Attributes.TryGetValue("Position",out var p)||!p.SequenceEqual(expectedPosition)||
            !view.Attributes.TryGetValue("Orientation",out var q)||!q.SequenceEqual(pose.Orientation)||
            !view.Attributes.TryGetValue("MissionKey",out var key)||!key.SequenceEqual(expectedKey)||!stream.Current)
            throw new InvalidDataException("Current live bound body pose/key/generation mismatch");
        return new(view,stream.Life,selected.Birth,selected.Binding!.Value,selected.Raw);
    }
}
