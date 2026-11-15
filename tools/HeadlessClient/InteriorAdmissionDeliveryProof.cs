using System.Globalization;
using System.Text.RegularExpressions;
namespace HeadlessClient;

/// <summary>Independent retained-wire check. A datagram receipt is not a room-ready/rendering claim.</summary>
public static class InteriorAdmissionDeliveryProof
{
    public sealed record Packet(int Line,string Time,bool Server,ushort Pss,ushort Cseq,ushort Sequence,byte[] Bytes);
    public sealed record Receipt(int TeleportLine,ushort TeleportSequence,int AckLine,ushort AckSequence,ushort AckBits,int CreationLine,ushort CreationSequence,ushort View,byte Spawn,string Scope="Exact transport receipt only; native rendering unproved");
    public static string Read(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }
    public static List<Packet> Parse(string text,uint character)
    {
        string[] lines=text.Split((char)10);var result=new List<Packet>();
        for(int i=0;i<lines.Length;i++)
        {
            var h=Regex.Match(lines[i],@"^\[(Server\s*->\s*Client|Client->Server) (.*?) PSS\s*:(\d+) CSEQ: (\d+) SSEQ: (\d+)\s*\]");
            if(!h.Success||i+1>=lines.Length)continue;
            var owner=Regex.Match(lines[i+1],@"^\[Receiver \|CharId: (\d+)\|");
            if(!owner.Success||uint.Parse(owner.Groups[1].Value,CultureInfo.InvariantCulture)!=character)continue;
            int j=i+2;var hex=new System.Text.StringBuilder();
            while(j<lines.Length&&Regex.IsMatch(lines[j].Trim(),@"^(?:[0-9a-fA-F]{2}\s*)+$")){hex.Append(lines[j].Trim());j++;}
            if(hex.Length==0)continue;
            result.Add(new Packet(i+1,h.Groups[2].Value,h.Groups[1].Value.StartsWith("Server",StringComparison.Ordinal),ushort.Parse(h.Groups[3].Value),ushort.Parse(h.Groups[4].Value),ushort.Parse(h.Groups[5].Value),WireFormat.FromHex(hex.ToString())));
        }
        return result;
    }
    public static bool ExactAck(ushort sent,ushort ack,ushort bits)
    {
        int distance=(ack-sent)&0xfff;
        return distance==0||(distance>=1&&distance<=8&&(bits&(1<<(distance-1)))!=0);
    }
    private static byte[]? Views(Packet p)
    {
        if(!p.Server||p.Bytes.Length==0)return null;int at=(p.Bytes[0]&0x80)!=0?5:1;
        return p.Bytes.Length>at+2&&p.Bytes[at]==3&&p.Bytes[^1]==0&&p.Bytes[^2]==0?p.Bytes[(at+1)..^2]:null;
    }
    private static bool Teleport(Packet p,double[] root)
    {
        var views=Views(p);if(views==null)return false;
        for(int i=0;i+28<=views.Length;i++)
            if(views.AsSpan(i,4).SequenceEqual(new byte[]{2,0,1,0x1c})&&Enumerable.Range(0,3).All(k=>BitConverter.ToSingle(views,i+12+k*4)==(float)root[k]))return true;
        return false;
    }
    public static Receipt Require(string text,uint character,int afterLine,double[] root,ushort goid,double[] centre)
    {
        var packets=Parse(text,character).Where(p=>p.Line>afterLine).ToArray();
        var teleport=packets.FirstOrDefault(p=>Teleport(p,root))??throw new ScenarioFailure("Missing exact own03 arrival teleport in retained wire");
        var bodies=packets.SelectMany(p=>Views(p)is byte[] views?WireFormat.FindCreations(views).Select(v=>(Packet:p,View:v)):Enumerable.Empty<(Packet Packet,ViewCreation View)>())
            .Where(p=>p.View.Goid==goid&&p.View.Attributes.TryGetValue("Position",out var position)&&position.Length==24&&Enumerable.Range(0,3).All(k=>BitConverter.ToDouble(position,k*8)==centre[k])).ToArray();
        Check.That(bodies.Length>0,"Expected actual free-prop creation in retained wire");var first=bodies[0];
        Check.That(first.Packet.Line>teleport.Line,"No free prop before or alongside arrival teleport");
        var ack=packets.FirstOrDefault(p=>!p.Server&&p.Line>teleport.Line&&p.Line<first.Packet.Line&&ExactAck(teleport.Sequence,p.Sequence,p.Pss));
        Check.That(ack!=null,"First prop creation follows a real exact client ACK/PSS receipt of the arrival datagram");
        return new Receipt(teleport.Line,teleport.Sequence,ack!.Line,ack.Sequence,ack.Pss,first.Packet.Line,first.Packet.Sequence,first.View.ViewId,first.View.SpawnId);
    }
}
