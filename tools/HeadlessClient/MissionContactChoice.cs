namespace HeadlessClient;
/// <summary>8096 carries the server's list index and plot flag, not the definition ID.</summary>
public static class MissionContactChoice
{
    public static int CriticalIndex(IEnumerable<byte[]> entries,string title)
    {
        var entry=entries.Single(b=>b.Length>17 && b[0]==0x80 && b[1]==0x96 && b[14]==1 && System.Text.Encoding.ASCII.GetString(b,17,b.Length-18)==title);
        return BitConverter.ToUInt16(entry,4);
    }
}
