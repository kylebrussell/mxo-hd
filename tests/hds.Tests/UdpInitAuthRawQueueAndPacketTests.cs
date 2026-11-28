using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading;
using hds;
using hds.shared;
using Xunit;

namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class UdpInitAuthRawQueueAndPacketTests
{
    private static WorldClient MakeMinimalClient()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        return client;
    }

    [Fact]
    public void MultiProtocolManager_RefusedUnverifiedUdpInit_DoesNotEstablishSessionOrQueueWorldSetup()
    {
        MarginServer previousMargin = Store.margin;
        WorldClient previousClient = Store.currentClient;
        try
        {
            Store.margin = new MarginServer();
            WorldClient client = MakeMinimalClient();
            Store.currentClient = client;

            byte[] initPacket = new byte[16];
            BitConverter.GetBytes(30302u).CopyTo(initPacket, 11);

            var mpm = new MultiProtocolManager();
            mpm.Parse(false, initPacket);

            Assert.False(client.playerData.getUDPSEssionEstablished());
            Assert.Equal(0u, client.playerData.getCharID());
            Assert.Empty(client.messageQueue.RPCMessagesQueue);
            Assert.Empty(client.messageQueue.ObjectMessagesQueue);
            Assert.Empty(client.messageQueue.rawMessages);
        }
        finally
        {
            Store.currentClient = previousClient;
            Store.margin = previousMargin;
        }
    }

    [Fact]
    public void MultiProtocolManager_ShortUnencryptedPacketBeforeInit_IsDroppedNotReflected()
    {
        WorldClient previousClient = Store.currentClient;
        try
        {
            WorldClient client = MakeMinimalClient();
            Store.currentClient = client;

            byte[] shortPlain = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

            var mpm = new MultiProtocolManager();
            mpm.Parse(false, shortPlain);

            Assert.False(client.playerData.getUDPSEssionEstablished());
            Assert.Empty(client.messageQueue.rawMessages);
            Assert.Empty(client.messageQueue.RPCMessagesQueue);
            Assert.Empty(client.messageQueue.ObjectMessagesQueue);
        }
        finally
        {
            Store.currentClient = previousClient;
        }
    }

    [Fact]
    public void MultiProtocolManager_PlainPacketAfterEstablishedSession_RoutesToBoundedRawQueue()
    {
        WorldClient previousClient = Store.currentClient;
        try
        {
            WorldClient client = MakeMinimalClient();
            client.playerData.setUDPSessionEstablished(true);
            Store.currentClient = client;

            var mpm = new MultiProtocolManager();
            byte[] plain = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05 };
            for (int i = 0; i < MessageQueue.MaxRawMessages + 20; i++)
            {
                mpm.Parse(false, plain);
            }

            Assert.Equal(MessageQueue.MaxRawMessages, client.messageQueue.rawMessages.Count);
        }
        finally
        {
            Store.currentClient = previousClient;
        }
    }

    [Fact]
    public void MessageQueue_AddRawMessage_IsThreadSafeAndIgnoresNullOrEmpty()
    {
        var queue = new MessageQueue();
        queue.addRawMessage(null);
        queue.addRawMessage(Array.Empty<byte>());
        Assert.Empty(queue.rawMessages);

        Exception? failure = null;
        using var stop = new CancellationTokenSource();

        var flusher = new Thread(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    lock (queue.rawMessages.SyncRoot)
                    {
                        foreach (object _ in queue.rawMessages)
                        {
                        }
                        if (queue.rawMessages.Count >= MessageQueue.MaxRawMessages / 2)
                        {
                            queue.rawMessages.Clear();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        flusher.Start();

        byte[] payload = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05 };
        for (int i = 0; i < 5000; i++)
        {
            queue.addRawMessage(payload);
        }

        stop.Cancel();
        flusher.Join();

        Assert.Null(failure);
        Assert.True(queue.rawMessages.Count <= MessageQueue.MaxRawMessages);
    }

    [Fact]
    public void MessageQueue_ForgetSentRpcsAndPruneUnassignedWorldRpcs_CompactInOrder()
    {
        var queue = new MessageQueue();
        queue.addRpcMessage(new byte[] { 0x11 }); // sent
        queue.addRpcMessage(new byte[] { 0x22 }); // unsent
        queue.addRpcMessage(new byte[] { 0x33 }); // sent
        queue.addRpcMessage(new byte[] { 0x44 }); // unsent

        ((SequencedMessage)queue.RPCMessagesQueue[0]!).rpcNumber = 1;
        ((SequencedMessage)queue.RPCMessagesQueue[2]!).rpcNumber = 2;

        queue.ForgetSentRpcs(enteringLoadingArea: false);

        Assert.Equal(2, queue.RPCMessagesQueue.Count);
        Assert.Equal(new byte[] { 1, 0x22 }, ((SequencedMessage)queue.RPCMessagesQueue[0]!).content);
        Assert.Equal(new byte[] { 1, 0x44 }, ((SequencedMessage)queue.RPCMessagesQueue[1]!).content);

        queue.ForgetSentRpcs(enteringLoadingArea: true);
        Assert.Empty(queue.RPCMessagesQueue);

        queue.AddWorldBoundRpc(new byte[] { 0x55 }, world: 1, transport: 10);
        queue.AddWorldBoundRpc(new byte[] { 0x66 }, world: 2, transport: 10);
        queue.AddWorldBoundRpc(new byte[] { 0x77 }, world: 2, transport: 11);

        queue.PruneUnassignedWorldRpcs(world: 2, transport: 11);

        Assert.Single(queue.RPCMessagesQueue);
        Assert.Equal(new byte[] { 1, 0x77 }, ((SequencedMessage)queue.RPCMessagesQueue[0]!).content);
    }

    [Fact]
    public void PacketContent_AddHexBytes_MatchesLegacyAndRollsBackOnInvalidHex()
    {
        var content = new PacketContent();
        content.AddHexBytes("");
        content.AddHexBytes(null);
        Assert.Empty(content.ReturnFinalPacket());

        const string sample = "000fa5FF1234567890abcdefABCDEF";
        content.AddHexBytes(sample);
        Assert.Equal(StringUtils.hexStringToBytes(sample), content.ReturnFinalPacket());

        byte[] beforeError = content.ReturnFinalPacket();

        Assert.Throws<FormatException>(() => content.AddHexBytes("abc"));
        Assert.Equal(beforeError, content.ReturnFinalPacket());

        Assert.Throws<FormatException>(() => content.AddHexBytes("deadbeeg"));
        Assert.Equal(beforeError, content.ReturnFinalPacket());

        content.AddHexBytes("42");
        Assert.Equal(beforeError.Length + 1, content.ReturnFinalPacket().Length);
        Assert.Equal(0x42, content.ReturnFinalPacket()[^1]);
    }

    [Fact]
    public void StringUtils_StringCreateHelpers_HandleEdgeCases()
    {
        byte[] data = { 0x00, 0x09, 0xab, 0xff };
        Assert.Equal("00 09 ab ff ", StringUtils.bytesToString(data));
        Assert.Equal("00 09 ", StringUtils.bytesToString(data, 2));
        Assert.Equal("00 09 ab ff ", StringUtils.bytesToString(data, 99));
        Assert.Equal("", StringUtils.bytesToString(data, 0));
        Assert.Equal("", StringUtils.bytesToString(data, -3));
        Assert.Equal("", StringUtils.bytesToString(null));
        Assert.Equal("0009abff", StringUtils.bytesToString_NS(data));
        Assert.Equal("", StringUtils.bytesToString_NS(null));

        byte[] chars = { (byte)'M', (byte)'x', (byte)'O', 0, (byte)'!' };
        Assert.Equal("MxO\0!", StringUtils.charBytesToString(chars));
        Assert.Equal("MxO", StringUtils.charBytesToString_NZ(chars));
        Assert.Equal("", StringUtils.charBytesToString(null));
        Assert.Equal("", StringUtils.charBytesToString_NZ(null));
        Assert.Equal("", StringUtils.charBytesToString_NZ(new byte[] { 0 }));
    }
}
