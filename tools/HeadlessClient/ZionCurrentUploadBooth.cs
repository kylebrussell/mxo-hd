using hds;
namespace HeadlessClient;
// Read-only current-life reducer over qualified producer-backed frames. Never scans through unknown bytes.
public static class ZionCurrentUploadBooth
{
    public const uint StaticKey=0x393001ea;
    public static ViewCreation? Find(OwnViewLifeSnapshot stream)
    {
        if(!stream.Current||!stream.OwnViewReady||stream.OwnBirthSection<0||stream.Events.Any(e=>e.Life!=stream.Life))throw new InvalidDataException("No current own life for measured door");
        var live=new Dictionary<ushort,(ViewCreation View,string Raw,int Birth,bool Known)>();var retired=new HashSet<(ushort,byte)>();bool own=false;
        foreach(var e in stream.Events)
        {
            if(e.Section<stream.OwnBirthSection)continue;var bytes=e.Bytes;var births=WireFormat.FindCreationEvents(bytes);int at=0;
            if(e.Section==stream.OwnBirthSection){var self=births.LastOrDefault(c=>c.Creation.ViewId==2&&c.Creation.Goid==12)??throw new InvalidDataException("Actual own2 birth absent");at=self.Offset+self.Length;own=true;}
            if(!own)throw new InvalidDataException("Door cannot precede current own2");
            while(at<bytes.Length)
            {
                var birth=births.FirstOrDefault(c=>c.Offset==at);
                if(birth!=null)
                {
                    var v=birth.Creation;var raw=Convert.ToHexString(bytes.AsSpan(at,birth.Length));
                    if(!live.TryGetValue(v.ViewId,out var prior)||prior.Raw!=raw)
                    {if(prior.View!=null)retired.Add((prior.View.ViewId,prior.View.SpawnId));live[v.ViewId]=(v,raw,e.Section,!retired.Contains((v.ViewId,v.SpawnId)));}
                    at+=birth.Length;continue;
                }
                ushort? kind=at+2<=bytes.Length&&live.TryGetValue(BitConverter.ToUInt16(bytes,at),out var addressed)?addressed.View.Goid:
                    at+2<=bytes.Length&&BitConverter.ToUInt16(bytes,at)==2?(ushort)12:null;
                if(!ZionDoorViewFrames.TryRead(bytes,at,kind,out var frame))throw new InvalidDataException("Unknown/truncated current measured-door life");
                if(frame.Type==ZionDoorViewFrames.Kind.Delete){if(live.Remove(frame.View,out var old))retired.Add((old.View.ViewId,old.View.SpawnId));}
                else if(live.TryGetValue(frame.View,out var changed)&&frame.PositionOffset>=0&&changed.View.Goid!=599)live[frame.View]=(changed.View,changed.Raw,changed.Birth,false);
                at+=frame.Length;
            }
        }
        var matches=live.Values.Where(v=>v.View.RelatedStaticObjectId==StaticKey).ToArray();
        if(matches.Length==0)return null;
        if(!own||matches.Length!=1||!matches[0].Known)throw new InvalidDataException("Ambiguous/deleted/reused current upload booth");
        var chosen=matches[0];
        if(chosen.View.Goid!=592||!chosen.View.Attributes.TryGetValue("Position",out var xyz)||xyz.Length!=24||
           !new[]{17000d,530,2200}.SequenceEqual(Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(xyz,i*8))))throw new InvalidDataException("Wrong exact Mara upload booth pose/class");
        if(!stream.Current)throw new InvalidDataException("Upload booth own life changed");
        return chosen.View;
    }
}
