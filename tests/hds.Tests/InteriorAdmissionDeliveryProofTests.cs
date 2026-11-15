using HeadlessClient;
namespace hds.Tests;
public class InteriorAdmissionDeliveryProofTests
{
    static readonly double[] Root={23739.223227027636,895,13996.116135138183},Centre={23814.611328125,840,14241.5712890625};
    const string Creation="01000c240204cdab04c60100000000f304353f00000000f204353f00000020a741d7400000000000408a4000000020c9d0cb4004ff2f0002060000";
    static byte[] Teleport()=>Convert.FromHexString("0200011c01d1005300030000").Concat(Root.SelectMany(x=>BitConverter.GetBytes((float)x))).Concat(new byte[4]).ToArray();
    static byte[] View(byte[] content)=>new byte[]{2,3}.Concat(content).Concat(new byte[2]).ToArray();
    static string Packet(bool server,int seq,int bits,byte[] bytes,uint character=110)=>
        $"[{(server?"Server -> Client":"Client->Server")} 1:00:00 PM Saturday, November 14, 2026 PSS :{bits} CSEQ: 20 SSEQ: {seq} ]\n[Receiver |CharId: {character}| Handle: CSR_Tester ]\n"+WireFormat.ToHex(bytes)+"\n\n";
    static string Good()=>Packet(true,4094,0,View(Teleport()))+Packet(false,0,2,new byte[]{2})+Packet(true,2,0,View(Convert.FromHexString(Creation)));
    [Fact] public void LiteralNativeCreationFollowsActualWrappedSelectiveAck()
    {
        var v=InteriorAdmissionDeliveryProof.Require(Good(),110,0,Root,548,Centre);
        Assert.Equal((ushort)4094,v.TeleportSequence);Assert.Equal((ushort)0,v.AckSequence);Assert.Equal((ushort)2,v.AckBits);Assert.Equal((ushort)6,v.View);Assert.Equal((byte)4,v.Spawn);
        Assert.True(v.TeleportLine<v.AckLine&&v.AckLine<v.CreationLine);
    }
    [Theory] [InlineData(4094,0,2,true)] [InlineData(138,140,2,true)] [InlineData(138,140,0,false)] [InlineData(138,148,255,false)]
    public void ReceiptUsesExactAckWindowNotLaterSequenceOnly(ushort sent,ushort ack,ushort bits,bool expected)=>Assert.Equal(expected,InteriorAdmissionDeliveryProof.ExactAck(sent,ack,bits));
    [Fact] public void FutureAckWithoutReceiptBitCannotPublishSuccess()
    {
        string raw=Packet(true,4094,0,View(Teleport()))+Packet(false,0,0,new byte[]{2})+Packet(true,2,0,View(Convert.FromHexString(Creation)));
        Assert.Throws<ScenarioFailure>(()=>InteriorAdmissionDeliveryProof.Require(raw,110,0,Root,548,Centre));
    }
    [Fact] public void BodyBeforeOrAlongsideTeleportFailsEvenWhenEventuallyAcked()
    {
        string early=Packet(true,4092,0,View(Convert.FromHexString(Creation)))+Good();
        Assert.Throws<ScenarioFailure>(()=>InteriorAdmissionDeliveryProof.Require(early,110,0,Root,548,Centre));
        string same=Packet(true,4094,0,View(Teleport().Concat(Convert.FromHexString(Creation)).ToArray()))+Packet(false,0,2,new byte[]{2});
        Assert.Throws<ScenarioFailure>(()=>InteriorAdmissionDeliveryProof.Require(same,110,0,Root,548,Centre));
    }
    [Fact] public void AckAfterCreationOrFromOtherCharacterCannotPass()
    {
        string late=Packet(true,4094,0,View(Teleport()))+Packet(true,2,0,View(Convert.FromHexString(Creation)))+Packet(false,0,2,new byte[]{2});
        Assert.Throws<ScenarioFailure>(()=>InteriorAdmissionDeliveryProof.Require(late,110,0,Root,548,Centre));
        string foreign=Packet(true,4094,0,View(Teleport()))+Packet(false,0,2,new byte[]{2},111)+Packet(true,2,0,View(Convert.FromHexString(Creation)));
        Assert.Throws<ScenarioFailure>(()=>InteriorAdmissionDeliveryProof.Require(foreign,110,0,Root,548,Centre));
    }
    [Fact] public void WrongRootOrCentreAndOldOperationCursorCannotPass()
    {
        Assert.Throws<ScenarioFailure>(()=>InteriorAdmissionDeliveryProof.Require(Good(),110,0,new[]{1d,2d,3d},548,Centre));
        Assert.Throws<ScenarioFailure>(()=>InteriorAdmissionDeliveryProof.Require(Good(),110,0,Root,548,new[]{1d,2d,3d}));
        Assert.Throws<ScenarioFailure>(()=>InteriorAdmissionDeliveryProof.Require(Good(),110,100,Root,548,Centre));
    }
}
