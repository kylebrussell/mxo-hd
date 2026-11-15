using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

public class RegionRequestTests
{
    [Fact]
    public void TruncatedRegionRequestsAreRejectedBeforeReadingClientOrSource()
    {
        var previous = Store.currentClient;
        Store.currentClient = null;
        try
        {
            byte[] absent = null!;
            new RegionHandler().ProcessRegionLoaded(ref absent);
            for (int n = 0; n < 16; n++)
            {
                byte[] truncated = new byte[n];
                new RegionHandler().ProcessRegionLoaded(ref truncated);
            }
        }
        finally { Store.currentClient = previous; }
    }

    [Theory]
    [InlineData(0x01b00004u)]
    [InlineData(0x00c00005u)]
    public void MissingNativeDataMineKeysDoNotInventViewsOrReplies(uint id)
    {
        var singleton = typeof(DataLoader).GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previousLoader = singleton.GetValue(null);
        var previousClient = Store.currentClient;
        var loader = (DataLoader)RuntimeHelpers.GetUninitializedObject(typeof(DataLoader));
        loader.WorldObjectsById = new StaticObjectIndex();
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(7);
        client.viewMan = new ViewManager();
        client.messageQueue = new MessageQueue();
        singleton.SetValue(null, loader);
        Store.currentClient = client;
        try
        {
            // A client-provided position must not manufacture a source object for an omitted key.
            byte[] request = BitConverter.GetBytes(id).Concat(BitConverter.GetBytes(123f))
                .Concat(BitConverter.GetBytes(456f)).Concat(BitConverter.GetBytes(789f)).ToArray();
            new RegionHandler().ProcessRegionLoaded(ref request);
            Assert.Equal(0, client.viewMan.Count);
            Assert.Empty(client.messageQueue.ObjectMessagesQueue);
            Assert.Empty(client.messageQueue.RPCMessagesQueue);
            Assert.Null(loader.WorldObjectsById.Find(id, 7));
        }
        finally { singleton.SetValue(null, previousLoader); Store.currentClient = previousClient; }
    }
}
