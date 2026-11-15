using System.Runtime.CompilerServices;
using HeadlessClient;
using NativeQa.Modes;
using hds;
using hds.shared;
namespace hds.Tests;
public class MobiusRifleBootstrapTests
{
    static byte[] SuccessFromActualCodec()
    {
        var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.messageQueue=new MessageQueue();
        new ServerPackets().sendInventoryItemMove(1,105,client);
        var framed=Assert.Single(client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()).content;
        Assert.Equal(6,framed[0]);Assert.Equal(7,framed.Length);return framed[1..];
    }
    [Fact] public void RequestHasTwoByteSizedSlotsAndActualReplyUsesU16Slots()
    {
        var request=MobiusRifleBootstrap.Request;
        Assert.Equal(Convert.FromHexString("630169"),request);Assert.Equal(2,request.Length-1);Assert.Equal(InventoryHandler.WeaponSlot,request[2]);
        Assert.Equal(Convert.FromHexString("650100690000"),SuccessFromActualCodec());
        request[1]=2;Assert.Equal((byte)1,MobiusRifleBootstrap.Request[1]);
    }
    [Fact] public void ActualEquipCodecPlusActualOwnViewUpdateConfirmRifle()
    {
        Assert.True(MobiusRifleBootstrap.HasEquipEvidence(new[]{SuccessFromActualCodec()},new[]{ServerPackets.BuildEquippedItemUpdate(692)}));
    }
    [Fact] public void MalformedOldRequestRefusalCannotPassEvenWithRifleInHand()
    {
        var refused=ServerPackets.BuildEquipmentRefusal(Convert.FromHexString("01006900"));
        Assert.Equal(Convert.FromHexString("650100010000"),refused);
        Assert.False(MobiusRifleBootstrap.HasEquipEvidence(new[]{refused},new[]{ServerPackets.BuildEquippedItemUpdate(692)}));
    }
    [Fact] public void AckAloneWrongWeaponWrongViewAndPriorCursorEvidenceCannotPass()
    {
        var success=SuccessFromActualCodec();
        Assert.False(MobiusRifleBootstrap.HasEquipEvidence(new[]{success},Array.Empty<byte[]>()));
        Assert.False(MobiusRifleBootstrap.HasEquipEvidence(new[]{success},new[]{ServerPackets.BuildEquippedItemUpdate(0),ServerPackets.BuildEquippedItemUpdate(2565)}));
        var peer=ServerPackets.BuildEquippedItemUpdate(692);peer[0]=4;
        Assert.False(MobiusRifleBootstrap.HasEquipEvidence(new[]{success},new[]{peer}));
        Assert.False(MobiusRifleBootstrap.HasEquipEvidence(new[]{success[..^1]},new[]{ServerPackets.BuildEquippedItemUpdate(692)}));
        Assert.False(MobiusRifleBootstrap.HasEquipEvidence(new[]{success},new[]{ServerPackets.BuildEquippedItemUpdate(692)}.Skip(1)));
        Assert.False(MobiusRifleBootstrap.HasEquipEvidence(new[]{success}.Skip(1),new[]{ServerPackets.BuildEquippedItemUpdate(692)}));
    }
}
