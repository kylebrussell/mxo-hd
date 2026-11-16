namespace HeadlessClient;
/// <summary>Observation only, using current-cursor qualified frames and the existing raw receipt parser.</summary>
public static class MissionPublicStreetWire
{
    public sealed record Acknowledgment(int TeleportLine,ushort TeleportSequence,int AckLine,ushort AckSequence,ushort AckBits);
    public static List<string> Teleports(OwnViewLifeSnapshot stream,OwnViewLifeSnapshot expected,int afterSection)
    {
        if(!ReferenceEquals(stream.Owner,expected.Owner)||stream.Life!=expected.Life||!stream.Current||!expected.Current||afterSection<0)throw new InvalidDataException("Current own-life teleport scope");
        var goids=new Dictionary<ushort,ushort>();var result=new List<string>();bool own=false;
        foreach(var e in stream.Events)
        {
            if(e.Life!=stream.Life)throw new InvalidDataException("Foreign own-life event");if(e.Section<stream.OwnBirthSection)continue;
            var bytes=e.Bytes;var births=WireFormat.FindCreationEvents(bytes);int at=0;
            if(e.Section==stream.OwnBirthSection){var b=births.LastOrDefault(v=>v.Creation.ViewId==2&&v.Creation.Goid==12)??throw new InvalidDataException("Actual own birth missing");goids[2]=12;at=b.Offset+b.Length;own=true;}
            if(!own)throw new InvalidDataException("No own birth before frames");
            while(at<bytes.Length)
            {
                var birth=births.FirstOrDefault(v=>v.Offset==at);if(birth!=null){goids[birth.Creation.ViewId]=birth.Creation.Goid;at+=birth.Length;continue;}
                ushort? goid=at+2<=bytes.Length&&goids.TryGetValue(BitConverter.ToUInt16(bytes,at),out var id)?id:null;
                if(!MervFollowupViewFrames.TryRead(bytes,at,goid,out var frame))throw new InvalidDataException("Unknown current frame in public access proof");
                if(frame.Type==MervFollowupViewFrames.Kind.Delete)goids.Remove(frame.View);
                if(frame.Type==MervFollowupViewFrames.Kind.OwnTeleport&&e.Section>=afterSection)result.Add(Convert.ToHexString(bytes.AsSpan(at,frame.Length)));at+=frame.Length;
            }
        }
        if(!own||!stream.Current)throw new InvalidDataException("Own life changed");return result;
    }
    public static Acknowledgment Ack(string text,uint character,int afterLine,NpcInteriorComputerState.Teleport arrival)
    {
        byte[] raw=Convert.FromHexString(arrival.Raw);if(raw.Length!=28||!raw.AsSpan(0,4).SequenceEqual(Convert.FromHexString("0200011C")))throw new InvalidDataException("Exact typed arrival required");
        var packets=InteriorAdmissionDeliveryProof.Parse(text,character).Where(p=>p.Line>afterLine).ToArray();
        bool Carries(InteriorAdmissionDeliveryProof.Packet p)
        {
            if(!p.Server||p.Bytes.Length==0)return false;int at=(p.Bytes[0]&0x80)!=0?5:1;if(p.Bytes.Length<=at+2||p.Bytes[at]!=3||p.Bytes[^1]!=0||p.Bytes[^2]!=0)return false;
            var body=p.Bytes[(at+1)..^2];for(int i=0;i+raw.Length<=body.Length;i++)if(body.AsSpan(i,raw.Length).SequenceEqual(raw))return true;return false;
        }
        var sent=packets.FirstOrDefault(Carries)??throw new InvalidDataException("Typed arrival missing from retained current-character wire");
        var ack=packets.FirstOrDefault(p=>!p.Server&&p.Line>sent.Line&&InteriorAdmissionDeliveryProof.ExactAck(sent.Sequence,p.Sequence,p.Pss))??throw new InvalidDataException("No actual client receipt of arrival datagram");return new(sent.Line,sent.Sequence,ack.Line,ack.Sequence,ack.Pss);
    }
}
