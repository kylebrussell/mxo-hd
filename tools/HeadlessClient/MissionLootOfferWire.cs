using System.Text;

namespace HeadlessClient;

/// <summary>Independent ordinary2201 list oracle from the native8095/96 consumers; not a UI-state emulator.</summary>
public static class MissionLootOfferWire
{
    public sealed record Offer(byte[] Header,byte[] Entry,byte[] End,ushort Index);
    public static Offer Require(IEnumerable<byte[]> records,ushort request)
    {
        var list=records.Where(b=>b.Length>=2&&b[0]==0x80&&b[1] is 0x95 or 0x96 or 0x97).ToArray();
        void Need(bool valid,string reason) { if(!valid)throw new InvalidDataException("2201 contact list: "+reason); }
        Need(list.Length==3,"one header, entry and end required");
        byte[] h=list[0],e=list[1],end=list[2];
        Need(h.Length==24&&h[1]==0x95,"header layout");
        Need(BitConverter.ToUInt32(h,2)==0&&BitConverter.ToUInt16(h,6)==request,"status/nonce");
        Need(BitConverter.ToUInt16(h,8)==1&&h[10]==0&&h[11]==2&&BitConverter.ToUInt32(h,12)==2001,"count and Agent Gray contact");
        Need(e.Length>=18&&e[1]==0x96&&BitConverter.ToUInt16(e,2)==request,"entry layout/nonce");
        ushort offset=BitConverter.ToUInt16(e,6);
        Need(offset==15&&e[8]==2&&BitConverter.ToUInt32(e,9)==2001&&e[13]==0&&e[14]==1,"title offset/contact/plot");
        ushort length=BitConverter.ToUInt16(e,offset);
        Need(length>0&&offset+2+length==e.Length&&e[^1]==0,"sized terminated title");
        Need(Encoding.ASCII.GetString(e,offset+2,length-1)=="The End of the Beginning","actual offered title");
        Need(end.Length==4&&end[1]==0x97&&BitConverter.ToUInt16(end,2)==request,"end/nonce");
        return new(h,e,end,BitConverter.ToUInt16(e,4));
    }
}
