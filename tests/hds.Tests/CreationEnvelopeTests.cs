using hds;
using HeadlessClient;
namespace hds.Tests;
public class CreationEnvelopeTests
{
    private static byte[] Frame(byte[] body,Action<byte[]>? change=null)
    {
        var codec=new MarginEncryption();byte[] checkedBytes=new byte[6+body.Length];
        BitConverter.GetBytes((ushort)body.Length).CopyTo(checkedBytes,0);body.CopyTo(checkedBytes,6);
        int pad=16-(checkedBytes.Length+4)%16;
        byte[] plain=new Crc32().checksumB(checkedBytes,1).Concat(checkedBytes).Concat(Enumerable.Repeat((byte)pad,pad)).ToArray();change?.Invoke(plain);
        var tf=new MxoTwofish();tf.SetIV(codec.getIV());tf.SetKey(codec.getTFKey());byte[] encrypted=new byte[plain.Length];tf.Encrypt(plain,encrypted);
        return new byte[]{0}.Concat(codec.getIV()).Concat(encrypted).ToArray();
    }
    [Fact] public void ObservedTwentyByteNameHasTwoPaddingBytesButExactCreationBodyIsTwenty()
    {
        byte[] body=CharacterCreationWire.Reserve("Personality308");Assert.Equal(20,body.Length);byte[] packet=Frame(body);
        byte[] legacy=new MarginEncryption().decryptMargin(packet);Assert.Equal(22,legacy.Length);Assert.Equal(new byte[]{2,2},legacy[^2..]);
        Assert.True(new MarginEncryption().TryDecryptCreationBody(packet,out var exact));Assert.Equal(body,exact);
        Assert.Equal(legacy,new MarginEncryption().decryptMargin(packet));
    }
    [Fact] public void AllCreationPaddingLengthsReturnTheDeclaredBodyAndStrictParserStillRejectsExtraBytes()
    {
        var seen=new HashSet<int>();
        for(int length=0;length<32;length++)
        {
            byte[] body=CharacterCreationWire.Create(308,"First","Last",new string('a',length));byte[] packet=Frame(body);
            int padding=packet.Length-17-10-body.Length;seen.Add(padding);
            Assert.True(new MarginEncryption().TryDecryptCreationBody(packet,out var exact));Assert.Equal(body,exact);
            Assert.True(CharacterCreation.TryParse(exact,out _));Assert.False(CharacterCreation.TryParse(exact.Concat(new byte[]{0}).ToArray(),out _));
        }
        Assert.Equal(Enumerable.Range(1,16),seen.Order());
    }
    [Theory][InlineData("padding")][InlineData("size")][InlineData("crc")]
    public void CorruptCreationEnvelopeNeverReachesItsBodyParser(string part)
    {
        byte[] body=CharacterCreationWire.Reserve("Personality308");
        byte[] packet=Frame(body,plain=>{if(part=="padding")plain[^1]^=1;else if(part=="size")plain[4]++;else plain[0]^=1;});
        Assert.False(new MarginEncryption().TryDecryptCreationBody(packet,out _));
    }
    [Fact] public void OtherOpcodesAndTruncatedCiphertextDoNotOptIn()
    {
        byte[] body={0x0f,1,0,0,0,0,0};byte[] packet=Frame(body);
        Assert.False(new MarginEncryption().TryDecryptCreationBody(packet,out _));Assert.Equal(body,new MarginEncryption().decryptMargin(packet)[..body.Length]);
        Assert.False(new MarginEncryption().TryDecryptCreationBody(packet[..^1],out _));
        Assert.False(new MarginEncryption().TryDecryptCreationBody(new byte[16],out _));
    }
}
