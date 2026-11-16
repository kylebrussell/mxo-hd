using hds;
namespace HeadlessClient;
// Read-only current-life reducer over qualified producer-backed frames. Never scans through unknown bytes.
public static class ZionCurrentDoorOverlay
{
    public static ViewCreation Require(OwnViewLifeSnapshot stream,ZionAfter1212RidePlan plan)
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
        var matches=live.Values.Where(v=>v.View.RelatedStaticObjectId==plan.Key).ToArray();
        if(!own||matches.Length!=1||!matches[0].Known)throw new InvalidDataException("Missing/ambiguous/deleted/reused current measured door key");
        var chosen=matches[0];plan.RequireOverlay(chosen.View);if(!stream.Current)throw new InvalidDataException("Door own life changed");
        return chosen.View;
    }
}
