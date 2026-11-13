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
        public byte[] encrypt(byte[] bytes,int length,ushort pss,ushort cseq,ushort sseq)
        { lock(Plain) Plain.Add(bytes.ToArray()); return bytes; }
        public ArrayList decrypt(byte[] bytes,int length)=>throw new NotSupportedException();
    }
    private static void Set(WorldClient client,string field,object value)=>typeof(WorldClient)
        .GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(client,value);
    private static bool Flag(WorldClient client,string field)=>(bool)typeof(WorldClient)
        .GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(client)!;

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
}
