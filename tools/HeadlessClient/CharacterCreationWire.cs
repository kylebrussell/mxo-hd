using System.Buffers.Binary;
using System.Text;
namespace HeadlessClient;

/// <summary>Native 6253c200/6253c750 constructors, 6253c360/6253c8a0 sized strings and 6253e820 producer.</summary>
public static class CharacterCreationWire
{
    public static byte[] Reserve(string handle)
    {
        byte[] name=Text(handle); byte[] body=new byte[5+name.Length];body[0]=0x0a;
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(1),3);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(3),checked((ushort)name.Length));name.CopyTo(body,5);return body;
    }

    // Source StartingCharacterData.xml male clothing choices, and newrsiIDs.csv male body/head/hair.
    // Sixteen actual u32 appearance slots; no character/RSI row is seeded or copied.
    public static readonly uint[] MaleAppearance = {0,106,0,105,0,0,103,0,1030,987,5907,6727,922,6386,0,1258};
    public static byte[] Create(uint trait,string first,string last,string background,string fourth = "")
    {
        var strings=new[]{Text(first),Text(last),Text(background),Text(fourth)};
        byte[] body=new byte[77+strings.Sum(s=>2+s.Length)];body[0]=0x0c;
        for(int i=0;i<16;i++)BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(1+4*i),MaleAppearance[i]);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(65),trait);
        int offset=77;
        for(int i=0;i<4;i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(69+2*i),checked((ushort)offset));
            BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(offset),checked((ushort)strings[i].Length));
            strings[i].CopyTo(body,offset+2);offset+=2+strings[i].Length;
        }
        return body;
    }
    private static byte[] Text(string text)
    {
        if(text.Any(c=>c<32||c>126)||text.Length>1023)throw new ArgumentException("Bounded ASCII creation fixture text");
        return Encoding.ASCII.GetBytes(text+"\0");
    }
}
