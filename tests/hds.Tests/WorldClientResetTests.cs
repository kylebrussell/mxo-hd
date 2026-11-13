using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class WorldClientResetTests
{
    private sealed class RecordingCipher : IWorldEncryption
    {
        public readonly List<byte[]> Plain = new();
        public Exception? Fault;
        public byte[] encrypt(byte[] bytes,int length,ushort pss,ushort cseq,ushort sseq)
        { if(Fault != null) throw Fault; lock(Plain) Plain.Add(bytes.ToArray()); return bytes; }
        public ArrayList decrypt(byte[] bytes,int length)=>throw new NotSupportedException();
    }
    private static void Set(WorldClient client,string field,object value)=>typeof(WorldClient)
        .GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(client,value);
    private static bool Flag(WorldClient client,string field)=>(bool)typeof(WorldClient)
        .GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(client)!;

    [Fact]
    public void EncryptionFailureReleasesTheActualFlushClaimForLaterWork()
    {
        var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        var cipher=new RecordingCipher {Fault=new InvalidOperationException("test encryption failure")};
        Set(client,"flushLock",new object());Set(client,"<TransportFence>k__BackingField",new WorldSendFence());
        Set(client,"cypher",cipher);
        client.playerData=new ClientData();client.messageQueue=new MessageQueue();client.viewMan=new ViewManager();
        client.messageQueue.addRpcMessage(new byte[]{0x81,0x49});
        Assert.Throws<InvalidOperationException>(()=>client.FlushQueue(force:true));
        Assert.False(Flag(client,"flushingQueueInProgress"));
        cipher.Fault=null;
        client.messageQueue.addRpcMessage(new byte[]{0x81,0x4b});
        client.TickFlush();
        Assert.Contains(cipher.Plain,p=>p.AsSpan().IndexOf(new byte[]{0x81,0x4b})>=0);
        Assert.False(Flag(client,"flushingQueueInProgress"));
    }

    [Fact]
    public void ArchiveViewProducerCannotAllocateAfterItsEpochWorldOrMapChanges()
    {
        var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        var fence=new WorldSendFence();Set(client,"<TransportFence>k__BackingField",fence);
        client.playerData=new ClientData();client.playerData.setDistrictId(7);client.playerData.setOnWorld(true);
        client.messageQueue=new MessageQueue();client.viewMan=new ViewManager();
        long epoch=fence.Capture();int world=client.playerData.worldGeneration;
        void Allocate(){client.viewMan.addView(123,47267);client.messageQueue.addObjectMessage(new byte[]{1,0,8},false);}
        Assert.True(PvpArchives.CommitViews(client,epoch,7,world,Allocate));
        fence.PublishIncoming(true,()=>{});long reset=fence.Capture();
        Assert.True(fence.ApplyReset(reset,()=>{client.viewMan.Clear();client.messageQueue.ForgetWorldObjects();}));
        Assert.False(PvpArchives.CommitViews(client,reset,7,world,Allocate)); // reset setup is still pending
        fence.Resume(reset);
        Assert.False(PvpArchives.CommitViews(client,epoch,7,world,Allocate));
        client.playerData.setOnWorld(false);client.playerData.setOnWorld(true);
        Assert.False(PvpArchives.CommitViews(client,reset,7,world,Allocate));
        world=client.playerData.worldGeneration;client.playerData.setDistrictId(1);
        Assert.False(PvpArchives.CommitViews(client,reset,7,world,Allocate));
        Assert.Empty(client.messageQueue.ObjectMessagesQueue);Assert.Equal(0,client.viewMan.Count);
        Assert.True(PvpArchives.CommitViews(client,reset,1,world,Allocate));
        Assert.Single(client.messageQueue.ObjectMessagesQueue);Assert.Equal(1,client.viewMan.Count);
    }

    [Fact]
    public async Task RejectedPreResetFlushReleasesClaimAndTickSendsNewSetupAndView()
    {
        using var receiver=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
        receiver.Bind(new IPEndPoint(IPAddress.Loopback,0));
        using var sender=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
        var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        var fence=new WorldSendFence();var cipher=new RecordingCipher();
        Set(client,"flushLock",new object());Set(client,"<TransportFence>k__BackingField",fence);
        Set(client,"cypher",cipher);Set(client,"socket",sender);Set(client,"Remote",receiver.LocalEndPoint!);
        Set(client,"alive",true);Set(client,"lastUsedTime",TimeUtils.getUnixTimeUint32());
        client.playerData=new ClientData();client.messageQueue=new MessageQueue();client.viewMan=new ViewManager();
        Task? oldFlush=null; Thread? flushThread=null;
        Monitor.Enter(client.messageQueue.rawMessages.SyncRoot);
        try
        {
            // Capture old epoch, then pause the actual flush before its queue preparation.
            oldFlush=Task.Run(()=>{flushThread=Thread.CurrentThread;client.FlushQueue(force:true);});
            Assert.True(SpinWait.SpinUntil(()=>Flag(client,"flushingQueueInProgress"),TimeSpan.FromSeconds(5)));
            // The only held lock on this path is rawMessages: waiting here proves epoch capture finished.
            Assert.True(SpinWait.SpinUntil(()=>flushThread != null &&
                (flushThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,TimeSpan.FromSeconds(5)));
            fence.PublishIncoming(true,()=>client.playerData.setPss(0));long reset=fence.Capture();
            Assert.True(fence.ApplyReset(reset,client.messageQueue.ForgetWorldObjects));
            client.messageQueue.addObjectMessage(new byte[]{0x2a,0,1,8,0x5a},false);
            client.messageQueue.addRpcMessage(new byte[]{0x81,0x49});
            fence.Resume(reset);
            client.FlushQueue(force:true); // setup requests a flush while the old claim is still occupied
        }
        finally { Monitor.Exit(client.messageQueue.rawMessages.SyncRoot); }
        await oldFlush!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(Flag(client,"flushingQueueInProgress"));Assert.True(Flag(client,"flushPending"));
        var view=(SequencedMessage)client.messageQueue.ObjectMessagesQueue[0]!;
        Assert.Equal((ushort)0,view.sendCounter);Assert.False(view.IsAckedBy(0));
        Assert.Empty(cipher.Plain);

        byte[] datagram=new byte[4096];var receive=receiver.ReceiveAsync(datagram,SocketFlags.None);
        client.TickFlush();
        Assert.True(await receive.WaitAsync(TimeSpan.FromSeconds(5))>0);
        Assert.Equal((ushort)1,view.sendCounter);
        Assert.True(((SequencedMessage)client.messageQueue.RPCMessagesQueue[0]!).rpcNumber>=0);
        Assert.Contains(cipher.Plain,p=>p.AsSpan().IndexOf(new byte[]{0x2a,0,1,8,0x5a})>=0);
        Assert.Contains(cipher.Plain,p=>p.AsSpan().IndexOf(new byte[]{0x81,0x49})>=0);
        Assert.False(Flag(client,"flushingQueueInProgress"));
    }
    [Fact]
    public async Task UnsentHowitzerRegistrationCannotLeakAcrossResetOrConsumeRpcNumbers()
    {
        using var receiver=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);receiver.Bind(new IPEndPoint(IPAddress.Loopback,0));
        using var sender=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));var fence=new WorldSendFence();var cipher=new RecordingCipher();
        Set(c,"flushLock",new object());Set(c,"<TransportFence>k__BackingField",fence);Set(c,"cypher",cipher);Set(c,"socket",sender);Set(c,"Remote",receiver.LocalEndPoint!);
        c.playerData=new ClientData();c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerData.passives.Loaded[356]=50;c.messageQueue=new();c.viewMan=new();
        c.messageQueue.addRpcMessage(new byte[]{0x81,0x49});
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});
        var old=c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Skip(1).ToArray();Assert.Equal(2,old.Length);Assert.All(old,m=>Assert.Equal(-1,m.rpcNumber));
        c.messageQueue.addRpcMessage(new byte[]{0x81,0x4b});
        fence.PublishIncoming(true,()=>c.playerData.setPss(0));long reset=fence.Capture();
        Assert.True(fence.ApplyReset(reset,()=>{c.playerData.setOnWorld(false);c.messageQueue.ForgetSentRpcs();c.messageQueue.ForgetWorldObjects();c.playerData.setRPCCounter(0);}));
        c.playerData.passives.Loaded.Remove(356);c.playerData.setOnWorld(true);fence.Resume(reset);
        var receive=receiver.ReceiveAsync(new byte[4096],SocketFlags.None);c.FlushQueue(force:true);Assert.True(await receive.WaitAsync(TimeSpan.FromSeconds(5))>0);
        var rpcs=cipher.Plain.SelectMany(p=>HeadlessClient.WireFormat.ParsePayload(p).Rpcs).ToArray();
        Assert.Equal(new[]{"8149","814B"},rpcs.Select(r=>Convert.ToHexString(r.Bytes)));
        Assert.Equal(new[]{0,1},rpcs.Select(r=>(int)r.ListCounter+r.Index));
        Assert.All(old,m=>Assert.Equal(-1,m.rpcNumber));
        Assert.Equal((ushort)2,c.playerData.getRPCCounter());
        //New-world readiness still has no parent; later ordinary parent load can register once in this world.
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(2,c.messageQueue.RPCMessagesQueue.Count);
        c.playerData.passives.Loaded[356]=50;AbilityGrantRegistration.LoadedChanged(c);Assert.Equal(4,c.messageQueue.RPCMessagesQueue.Count);
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(4,c.messageQueue.RPCMessagesQueue.Count);
    }

    [Fact]
    public void ResetAfterPreparationRejectsOldBatchAndKeepsUnrelatedUnsentRpcContiguous()
    {
        var fence=new WorldSendFence();var data=new ClientData();var queue=new MessageQueue();
        queue.AddWorldBoundRpc(ServerPackets.BuildAbilityLoad(1087,0,520),0,0);queue.addRpcMessage(new byte[]{0x81,0x49});
        var old=(SequencedMessage)queue.RPCMessagesQueue[0]!;var unaffected=(SequencedMessage)queue.RPCMessagesQueue[1]!;
        var prepared=new WorldPacket(data);prepared.addRPCContent(old);prepared.addRPCContent(unaffected);
        long oldEpoch=fence.Capture();Assert.Equal(-1,old.rpcNumber);
        fence.PublishIncoming(true,()=>data.setPss(0));long current=fence.Capture();
        Assert.True(fence.ApplyReset(current,queue.ForgetSentRpcs));fence.Resume(current);
        Assert.False(fence.TryRun(oldEpoch,()=>prepared.getFinalData(data)));Assert.Equal(-1,old.rpcNumber);
        Assert.True(fence.TryRun(current,()=>
        {
            queue.PruneUnassignedWorldRpcs(data.worldGeneration,current);
            var next=new WorldPacket(data);foreach(SequencedMessage message in queue.RPCMessagesQueue)next.addRPCContent(message);
            var rpc=Assert.Single(HeadlessClient.WireFormat.ParsePayload(next.getFinalData(data)).Rpcs);
            Assert.Equal("8149",Convert.ToHexString(rpc.Bytes));Assert.Equal((ushort)0,rpc.ListCounter);
        }));
        Assert.Single(queue.RPCMessagesQueue);Assert.Equal(-1,old.rpcNumber);Assert.Equal(0,unaffected.rpcNumber);
    }
    [Fact]
    public void GenerationChangeAfterPreparationKeepsExistingBareAckAndConsumesNoRpcCounter()
    {
        var queue=new MessageQueue();var data=new ClientData {lastSimTimeUpdate=1,lastSimTimeSeconds=TimeUtils.GetSimTimeSeconds()};
        queue.AddWorldBoundRpc(ServerPackets.BuildAbilityLoad(1087,0,520),data.worldGeneration,0);
        var message=(SequencedMessage)queue.RPCMessagesQueue[0]!;var packet=new WorldPacket(data);packet.addRPCContent(message);
        data.setOnWorld(false); //generation changed without transport reset, AFTER preparation.
        packet.PruneUnassignedWorldRpcs(data.worldGeneration,0);queue.PruneUnassignedWorldRpcs(data.worldGeneration,0);
        byte[] ack=packet.getFinalData(data);
        Assert.Equal(new byte[]{2},ack);Assert.Empty(HeadlessClient.WireFormat.ParsePayload(ack).Rpcs);
        Assert.Equal((ushort)0,data.getRPCCounter());Assert.Equal(-1,message.rpcNumber);Assert.Empty(queue.RPCMessagesQueue);
    }
    [Fact]
    public void AlreadyNumberedOwnedRpcKeepsReliableResendAndFollowingCounter()
    {
        var queue=new MessageQueue();var data=new ClientData();data.setRPCCounter(8);
        queue.AddWorldBoundRpc(ServerPackets.BuildAbilityLoad(1087,0,520),0,0);
        var old=(SequencedMessage)queue.RPCMessagesQueue[0]!;old.rpcNumber=7;
        queue.addRpcMessage(new byte[]{0x81,0x49});data.setOnWorld(false);
        queue.PruneUnassignedWorldRpcs(data.worldGeneration,0);Assert.Equal(2,queue.RPCMessagesQueue.Count);
        var packet=new WorldPacket(data);foreach(SequencedMessage message in queue.RPCMessagesQueue)packet.addRPCContent(message);
        packet.PruneUnassignedWorldRpcs(data.worldGeneration,0);
        var rpc=HeadlessClient.WireFormat.ParsePayload(packet.getFinalData(data)).Rpcs;
        Assert.Equal(new[]{7,8},rpc.Select(r=>(int)r.ListCounter+r.Index));Assert.Equal((ushort)9,data.getRPCCounter());
    }

}
