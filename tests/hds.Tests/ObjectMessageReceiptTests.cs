using hds;
namespace hds.Tests;
public class ObjectMessageReceiptTests
{
    private static WorldPacket Encode(MessageQueue q,ClientData data)
    {
        var packet=new WorldPacket(data);
        foreach(SequencedMessage message in q.ObjectMessagesQueue)Assert.True(packet.addObjectContent(message));
        packet.getFinalData(data);return packet;
    }
    [Fact] public void EncodingAndCumulativeAckAreNotDispatchOrExactReceipt()
    {
        var q=new MessageQueue();var receipt=new ObjectMessageReceipt();q.AddTrackedObject(new byte[]{2,0,1,0},receipt);
        var packet=Encode(q,new ClientData());ushort seq=packet.neededAckSSeq;
        q.ObserveObjectReceipts(seq,0);Assert.False(receipt.Received); // encode/send failure
        packet.RecordDispatchedObjectReceipts();
        q.ObserveObjectReceipts((ushort)((seq+2)&4095),0);Assert.False(receipt.Received);
        q.ObserveObjectReceipts((ushort)((seq+2)&4095),2);Assert.True(receipt.Received);
    }
    [Fact] public void EverySuccessfulRetransmitMembershipSurvivesTheOriginalObjectRetirement()
    {
        var q=new MessageQueue();var receipt=new ObjectMessageReceipt();q.AddTrackedObject(new byte[]{2,0,1,0},receipt);
        var data=new ClientData();var first=Encode(q,data);first.RecordDispatchedObjectReceipts();
        var second=Encode(q,data);second.RecordDispatchedObjectReceipts();
        q.ObjectMessagesQueue.Clear(); // retirement alone is not acknowledgement
        q.ObserveObjectReceipts(first.neededAckSSeq,0);Assert.True(receipt.Received);
        Assert.NotEqual(first.neededAckSSeq,second.neededAckSSeq);
    }
    [Fact] public void SelectiveBitmapWrapsAndResetCancelsEvenAnAcknowledgedCapability()
    {
        var q=new MessageQueue();var receipt=new ObjectMessageReceipt();q.AddTrackedObject(new byte[]{2,0,1,0},receipt);
        var packet=new WorldPacket(new ClientData()){neededAckSSeq=4094};
        packet.addObjectContent((SequencedMessage)q.ObjectMessagesQueue[0]!);packet.RecordDispatchedObjectReceipts();
        q.ObserveObjectReceipts(0,0);Assert.False(receipt.Received);
        q.ObserveObjectReceipts(0,2);Assert.True(receipt.Received);
        q.ForgetWorldObjects(); // already received receipts are lifecycle fenced by their owner, not queue membership
        var other=new ObjectMessageReceipt();q.AddTrackedObject(new byte[]{2,0,1,0},other);
        var sent=Encode(q,new ClientData());sent.RecordDispatchedObjectReceipts();q.ForgetWorldObjects();
        q.ObserveObjectReceipts(sent.neededAckSSeq,0);Assert.False(other.Received);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void MissionRollbackAndRejectedTransportPublicationNeverArmReceipt(bool changedEpoch)
    {
        var q=new MessageQueue();var receipt=new ObjectMessageReceipt();var fence=new WorldSendFence();
        using(var batch=MissionOutput.Begin(q))
        {
            q.AddTrackedObject(new byte[]{2,0,1,0},receipt);Assert.Empty(q.ObjectMessagesQueue);
            if(changedEpoch){Assert.True(MissionOutput.BindCurrent(q,fence,fence.Capture(),0));fence.PublishIncoming(true,()=>{});batch.Commit();}
        }
        Assert.Empty(q.ObjectMessagesQueue);receipt.Dispatched(1);q.ObserveObjectReceipts(1,0);Assert.False(receipt.Received);
    }
}
