using hds.shared;
using System;
namespace hds
{
    /// <summary>A zero-claim retry acknowledgement belongs only to the lifecycle that sent Take All.</summary>
    internal static class MissionContainerRetry
    {
        internal sealed class Owner
        {
            internal readonly WorldClient Client;
            internal readonly ClientData Data;
            internal readonly MessageQueue Queue;
            internal readonly uint Character;
            internal readonly int World, Life;
            internal readonly uint District;
            internal readonly long Transport;
            internal Owner(WorldClient client)
            {
                Client=client; Data=client.playerData; Queue=client.messageQueue; Character=Data.getCharID();
                World=Data.worldGeneration; Life=Data.lifeGeneration; District=Data.getDistrictId();
                Transport=client.TransportFence.Capture();
            }
            internal bool Current(WorldClient client) => ReferenceEquals(client,Client)&&ReferenceEquals(Store.currentClient,client)&&
                ReferenceEquals(client.playerData,Data)&&ReferenceEquals(client.messageQueue,Queue)&&Character!=0&&Data.getCharID()==Character&&
                client.Alive&&Data.getOnWorld()&&!Data.isDead&&!Data.duelDeath&&!Data.worldChangePending&&!Data.waitForRPCShutDown&&
                Data.worldGeneration==World&&Data.lifeGeneration==Life&&Data.getDistrictId()==District&&client.TransportFence.Capture()==Transport;
        }
        internal static bool Acknowledge(WorldClient client, Owner owner)
        {
            byte[] empty=ServerPackets.BuildLootAccepted(Array.Empty<(byte[],uint)>());
            bool queued=false;
            client.TransportFence.TryRun(owner.Transport,()=>
            {
                if(!owner.Current(client))return;
                client.messageQueue.AddWorldBoundRpc(empty,owner.World,owner.Transport);
                queued=true;
            });
            return queued;
        }
    }
}
