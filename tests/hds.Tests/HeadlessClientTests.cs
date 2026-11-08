using System.Security.Cryptography;
using System.Text;
using hds;
using hds.resources.gameobjects;
using HeadlessClient;

namespace hds.Tests;

/// <summary>The headless client's own packet code, without a server. The scenarios that need one run from tools/HeadlessClient.</summary>
public class HeadlessClientTests
{
    private static byte[] Hex(string hex) => WireFormat.FromHex(hex);

    [Fact]
    public void ReadsAnRpcListWithItsCounterAndBothLengthForms()
    {
        byte[] longRecord = new byte[200];
        longRecord[0] = 0x81;
        longRecord[1] = 0x87;
        byte[] packet = Hex("02 04 01 00 2a 02 02 4e 00 80 c8").Concat(longRecord).ToArray();

        WorldPayload payload = WireFormat.ParsePayload(packet);

        Assert.Equal(new[] { ((ushort)0x2a, 2) }, payload.RpcLists);
        Assert.Equal(2, payload.Rpcs.Count);
        Assert.Equal(0x4e, payload.Rpcs[0].Opcode);
        Assert.Equal(0x187, payload.Rpcs[1].Opcode);
        Assert.Equal(200, payload.Rpcs[1].Bytes.Length);
        Assert.False(payload.IsAckOnly);
    }

    [Fact]
    public void ReadsTheSimTimeAndAViewList()
    {
        byte[] time = BitConverter.GetBytes(12.5f);
        byte[] packet = new byte[] { 0x82 }.Concat(time).Concat(Hex("03 05 00 02 80 80 00 00")).ToArray();

        WorldPayload payload = WireFormat.ParsePayload(packet);

        Assert.Equal(12.5f, payload.SimTime);
        Assert.Equal(Hex("05 00 02 80 80"), payload.ViewSection);
    }

    [Fact]
    public void ABareAckIsAckOnly()
    {
        Assert.True(WireFormat.ParsePayload(Hex("02")).IsAckOnly);
    }

    [Theory]
    [InlineData("02 04 01 00 00 01 05 4e")]        // record runs past the packet
    [InlineData("02 04 01 00 00 01 01 4e 00")]     // bytes after the list
    [InlineData("02 03 05 00 02 80 80 01")]        // view list without its 00 00
    [InlineData("02 77 01")]                       // no such section
    public void AMalformedPacketIsAnError(string hex)
    {
        Assert.Throws<FormatException>(() => WireFormat.ParsePayload(Hex(hex)));
    }

    [Fact]
    public void BuildsWhatTheServersRpcListLayoutSays()
    {
        Assert.Equal(Hex("02 04 01 01 02 02 01 05 02 81 82"),
            WireFormat.BuildRpcPacket(0x102, new[] { Hex("05"), Hex("81 82") }));
    }

    /// <summary>The server's own creation record, read back: the attribute values and the view id at the end.</summary>
    [Fact]
    public void FindsAPlayerInACreationRecordAmongOtherUpdates()
    {
        PlayerCharacter player = new PlayerCharacter();
        player.DisableAllAttributes();
        player.CharacterName.setValue("Neo");
        player.Level.setValue((byte)7);
        player.CharacterID.setValue(4242u);

        DynamicArray creation = new ObjectManager().GenerateCreationPacket(player, 0, 0x51);
        byte[] section = Hex("0e 00 02 80 80 10 01 00").Concat(creation.getBytes()).Concat(Hex("0f 00 00 11 00 02 08")).ToArray();

        ViewCreation found = Assert.Single(WireFormat.FindCreations(section));

        Assert.Equal((ushort)0x0f, found.ViewId);
        Assert.Equal((ushort)12, found.Goid);
        Assert.Equal((byte)0x51, found.SpawnId);
        Assert.Equal("Neo", found.Name);
        Assert.Equal(7, found.Attributes["Level"][0]);
        Assert.Equal(4242u, BitConverter.ToUInt32(found.Attributes["CharacterID"]));
    }

    [Theory]
    [InlineData("81 87 19", "81 87 19 00", true)]
    [InlineData("81 ?? 19", "81 87 19 00", true)]
    [InlineData("81 87 20", "81 87 19 00", false)]
    [InlineData("81 87 19 00 00", "81 87 19 00", false)]
    public void PatternsMatchTheStartOfARecord(string pattern, string data, bool expected)
    {
        Assert.Equal(expected, BytePattern.Parse(pattern).IsMatch(Hex(data)));
    }

    [Fact]
    public void PatternsCanBeFoundInsideAViewList()
    {
        BytePattern pattern = BytePattern.Parse("05 00 02 ?? 08");
        Assert.True(pattern.Occurs(Hex("0e 00 02 80 05 00 02 99 08 00")));
        Assert.False(pattern.Occurs(Hex("0e 00 02 80 05 00 03 99 08 00")));
    }

    [Fact]
    public void DecryptsWhatTheMarginEncryptsToTheAccountKey()
    {
        using RSA rsa = RSA.Create(1024);
        RSAParameters key = rsa.ExportParameters(true);
        byte[] plain = Encoding.ASCII.GetBytes("the session key and IV go here..");

        byte[] cipher = rsa.Encrypt(plain, RSAEncryptionPadding.OaepSHA1);

        Assert.Equal(plain, AuthLogin.DecryptWithAccountKey(cipher, key.Modulus!, key.D!));
    }

    [Theory]
    [InlineData(5, "05")]
    [InlineData(0x7f, "7f")]
    [InlineData(0x80, "80 80")]
    [InlineData(315, "81 3b")]
    public void FramesWithTheTcpLengthPrefix(int length, string prefix)
    {
        byte[] framed = FramedReader.Frame(new byte[length]);
        Assert.Equal(Hex(prefix), framed.Take(framed.Length - length).ToArray());
    }
}
