using hds;
namespace HeadlessClient;
/// <summary>Read-only current-birth computer state; unknown framing can never enable a menu.</summary>
public static class MervAfterComputerState
{
    public sealed record Capability(long Life,int Birth,ushort View,byte Spawn,ushort Goid,string RawCreation);
    public sealed record Observation(Capability Capability,uint State);
    sealed class Live
    {
        public required ViewCreation View;public required string Raw;public int Birth;public uint State;public bool Known=true;
    }
    static bool Equal(ViewCreation a,ViewCreation b)=>a.ViewId==b.ViewId&&a.SpawnId==b.SpawnId&&a.Goid==b.Goid&&
        a.Attributes.Count==b.Attributes.Count&&a.Attributes.All(x=>b.Attributes.TryGetValue(x.Key,out var bytes)&&x.Value.SequenceEqual(bytes));
    static Dictionary<ushort,Live> Reduce(OwnViewLifeSnapshot stream)
    {
        if(!stream.Current||!stream.OwnViewReady||stream.OwnBirthSection<0||stream.Events.Any(e=>e.Life!=stream.Life))throw new InvalidDataException("No current own life for computer state");
        var live=new Dictionary<ushort,Live>();var retired=new HashSet<(ushort,byte)>();bool ownSeen=false;
        foreach(var e in stream.Events)
        {
            if(e.Section<stream.OwnBirthSection)continue;var bytes=e.Bytes;var births=WireFormat.FindCreationEvents(bytes);int at=0;
            if(e.Section==stream.OwnBirthSection){var own=births.LastOrDefault(c=>c.Creation.ViewId==2&&c.Creation.Goid==12)??throw new InvalidDataException("No actual own2 birth");at=own.Offset+own.Length;ownSeen=true;}
            if(!ownSeen)throw new InvalidDataException("Computer must follow own2");
            while(at<bytes.Length)
            {
                var birth=births.FirstOrDefault(c=>c.Offset==at);
                if(birth!=null)
                {
                    var view=birth.Creation;string raw=Convert.ToHexString(bytes.AsSpan(at,birth.Length));
                    if(!live.TryGetValue(view.ViewId,out var prior)||prior.Raw!=raw)
                    {
                        if(prior!=null)retired.Add((prior.View.ViewId,prior.View.SpawnId));
                        live[view.ViewId]=new(){View=view,Raw=raw,Birth=e.Section,State=MervFollowupApproachFixture.InitialState(view),Known=!retired.Contains((view.ViewId,view.SpawnId))};
                    }
                    at+=birth.Length;continue;
                }
                if(at+7<=bytes.Length&&bytes.AsSpan(at,5).SequenceEqual(new byte[]{1,0,1,1,0}))
                {if(live.Remove(BitConverter.ToUInt16(bytes,at+5),out var old))retired.Add((old.View.ViewId,old.View.SpawnId));at+=7;continue;}
                if(at+28<=bytes.Length&&bytes.AsSpan(at,4).SequenceEqual(new byte[]{2,0,1,0x1c})){at+=28;continue;}
                if(at+18<=bytes.Length&&bytes[at+2]==2&&bytes[at+3]==0x0e)
                {if(live.TryGetValue(BitConverter.ToUInt16(bytes,at),out var moving)&&MissionStaging.IsComputer(moving.View.Goid))moving.Known=false;at+=18;continue;}
                if(at+2<=bytes.Length&&live.TryGetValue(BitConverter.ToUInt16(bytes,at),out var target))
                {
                    var shape=ObjectStates.BuildStateUpdate(target.View.Goid,0);
                    if(shape!=null&&shape.Length>=4&&at+2+shape.Length<=bytes.Length&&bytes.AsSpan(at+2,shape.Length-4).SequenceEqual(shape.AsSpan(0,shape.Length-4)))
                    {target.State=BitConverter.ToUInt32(bytes,at+2+shape.Length-4);at+=2+shape.Length;continue;}
                }
                int next=births.Where(c=>c.Offset>at).Select(c=>c.Offset).DefaultIfEmpty(bytes.Length).Min();var gap=bytes[at..next];
                foreach(var v in live.Values)if(BytePattern.IndexOf(gap,BitConverter.GetBytes(v.View.ViewId),0)>=0)v.Known=false;
                at=next;
            }
        }
        if(!ownSeen||!stream.Current)throw new InvalidDataException("Computer own life changed");return live;
    }
    public static Observation Capture(OwnViewLifeSnapshot stream,ViewCreation expected)
    {
        if(!MissionStaging.IsComputer(expected.Goid))throw new InvalidDataException("Computer state only");
        var live=Reduce(stream);
        if(!live.TryGetValue(expected.ViewId,out var current)||!current.Known||!Equal(current.View,expected))throw new InvalidDataException("Computer current birth does not match selected view");
        return new(new(stream.Life,current.Birth,expected.ViewId,expected.SpawnId,expected.Goid,current.Raw),current.State);
    }
    public static uint Read(OwnViewLifeSnapshot stream,Capability scope)
    {
        if(stream.Life!=scope.Life)throw new InvalidDataException("Computer belongs to an old own life");
        var live=Reduce(stream);
        if(!live.TryGetValue(scope.View,out var current)||!current.Known||current.Birth!=scope.Birth||current.Raw!=scope.RawCreation||
            current.View.SpawnId!=scope.Spawn||current.View.Goid!=scope.Goid||!stream.Current)throw new InvalidDataException("Computer deleted/replaced or unsupported state framing");
        return current.State;
    }
}
