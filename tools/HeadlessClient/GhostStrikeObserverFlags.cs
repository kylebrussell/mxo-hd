using hds;
namespace HeadlessClient;

/// <summary>Narrow observer evidence reader for the two actual flag-update layouts used by this fixture.</summary>
public static class GhostStrikeObserverFlags
{
    public sealed record Sample(int ViewList,int Offset,ushort View,uint Flags,ushort? Dissembling,string Record);
    public static uint Expected(uint baseline,bool invisible)=>invisible?baseline|Stealth.StealthedFlag:baseline&~Stealth.StealthedFlag;
    public static Sample? Latest(IReadOnlyList<byte[]> sections,ushort view,int firstViewList=0)
    {
        byte[] flags=WireFormat.FromHex($"{view&255:x2}{view>>8:x2}028080808004");
        byte[] combined=WireFormat.FromHex($"{view&255:x2}{view>>8:x2}02808080c0");
        Sample? latest=null;
        for(int i=firstViewList;i<sections.Count;i++)
        {
            byte[] section=sections[i];
            foreach(bool both in new[]{false,true})
            {
                byte[] prefix=both?combined:flags;
                for(int at=BytePattern.IndexOf(section,prefix,0);at>=0;at=BytePattern.IndexOf(section,prefix,at+1))
                {
                    int value=at+prefix.Length+(both?3:0),end=value+4;
                    if(end>section.Length||both&&section[at+prefix.Length+2]!=4)continue;
                    if(latest!=null&&(latest.ViewList>i||latest.ViewList==i&&latest.Offset>at))continue;
                    latest=new Sample(i,at,view,BitConverter.ToUInt32(section,value),both?BitConverter.ToUInt16(section,at+prefix.Length):null,
                        Convert.ToHexString(section.AsSpan(at,end-at)));
                }
            }
        }
        return latest;
    }
}
