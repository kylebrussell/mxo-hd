namespace HeadlessClient;

/// <summary>Scoped complete-list oracle for subway-only CurrentState updates, not a general 03 decoder.</summary>
public static class SubwayStateWire
{
    public sealed record State(ushort View, uint Value);

    // Includes the enclosing list's final zero view. A terminator is not per-entry padding.
    public static IReadOnlyList<State> DecodeComplete(ReadOnlySpan<byte> list)
    {
        var result=new List<State>();int at=0;
        while(at+2<=list.Length)
        {
            ushort view=BitConverter.ToUInt16(list[at..]);at+=2;
            if(view==0)
            {
                if(at!=list.Length)throw new FormatException("Bytes follow the subway view-list terminator");
                return result;
            }
            if(at+6>list.Length||list[at]!=1||list[at+1]!=2)
                throw new FormatException("Not a complete subway CurrentState entry");
            result.Add(new(view,BitConverter.ToUInt32(list[(at+2)..])));at+=6;
        }
        throw new FormatException("Missing subway view-list terminator");
    }
}
