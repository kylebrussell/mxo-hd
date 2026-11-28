using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

public class DataLoaderAndProtocolParserTests
{
    [Fact]
    public void ParseStaticWorldObjectLineMatchesLegacyValuesAndDeduplicatesTypeAndQuat()
    {
        var typeCache = new Dictionary<ushort, byte[]>();
        var quatInterner = new DataLoader.StringSpanInterner(16);

        string line1 = "1,0,01000000,02000000,4c010000,True,99640.5,245.25,-8350.75,-89.5,0000000000000000000000000000803f";
        string line2 = "2,3,78563412,05000000,4c010000,False,-179670.0,-655.0,-95350.0,0.0,0000000000000000000000000000803f";

        StaticWorldObject obj1 = DataLoader.ParseStaticWorldObjectLine(line1.AsSpan(), typeCache, quatInterner, out ushort typeId1);
        StaticWorldObject obj2 = DataLoader.ParseStaticWorldObjectLine(line2.AsSpan(), typeCache, quatInterner, out ushort typeId2);

        Assert.Equal((ushort)1, obj1.metrId);
        Assert.Equal((ushort)0, obj1.sectorID);
        Assert.Equal(1u, obj1.mxoStaticId);
        Assert.Equal(2u, obj1.staticId);
        Assert.Equal((ushort)0x014c, typeId1);
        Assert.Equal(new byte[] { 0x4c, 0x01 }, obj1.type);
        Assert.True(obj1.exterior);
        Assert.Equal(99640.5, obj1.pos_x);
        Assert.Equal(245.25, obj1.pos_y);
        Assert.Equal(-8350.75, obj1.pos_z);
        Assert.Equal(-89.5, obj1.rot);
        Assert.Equal("0000000000000000000000000000803f", obj1.quat);

        Assert.Equal((ushort)2, obj2.metrId);
        Assert.Equal((ushort)3, obj2.sectorID);
        Assert.Equal(0x12345678u, obj2.mxoStaticId);
        Assert.Equal(5u, obj2.staticId);
        Assert.Equal((ushort)0x014c, typeId2);
        Assert.False(obj2.exterior);
        Assert.Equal(-179670.0, obj2.pos_x);

        // Identical type and quat must share canonical instances across rows/districts.
        Assert.Same(obj1.type, obj2.type);
        Assert.Same(obj1.quat, obj2.quat);
        Assert.Single(typeCache);
        Assert.Equal(1, quatInterner.Count);
    }

    [Fact]
    public void StringSpanInternerResizesAndPreservesCanonicalReferences()
    {
        var interner = new DataLoader.StringSpanInterner(16);
        var originals = new string[1500];
        for (int i = 0; i < originals.Length; i++)
        {
            string fresh = $"quat_{i:D8}_0000803f";
            originals[i] = interner.Intern(fresh.AsSpan());
            Assert.Equal(fresh, originals[i]);
        }

        Assert.Equal(originals.Length, interner.Count);
        for (int i = 0; i < originals.Length; i++)
        {
            char[] copy = $"quat_{i:D8}_0000803f".ToCharArray();
            string lookedUp = interner.Intern(copy.AsSpan());
            Assert.Same(originals[i], lookedUp);
        }
    }

    [Fact]
    public void StaticObjectIndexSeparatesDistrictsAndDefaultsToFirstDistrict()
    {
        var index = new StaticObjectIndex();
        index.EnsureCapacity(8);

        var d1 = new StaticWorldObject { metrId = 1, mxoStaticId = 0x00010005u };
        var d2 = new StaticWorldObject { metrId = 2, mxoStaticId = 0x00010005u };
        var d1Dup = new StaticWorldObject { metrId = 1, mxoStaticId = 0x00010005u };

        index.Add(d1);
        index.Add(d2);
        index.Add(d1Dup); // TryAdd keeps first entry

        Assert.Same(d1, index.Find(0x00010005u, 1));
        Assert.Same(d2, index.Find(0x00010005u, 2));
        Assert.Same(d1, index.Find(0x00010005u));
        Assert.Null(index.Find(0x00010005u, 3));
        Assert.Null(index.Find(0x99999999u));
    }

    [Fact]
    public void Protocol04ManagerRejectsTruncatedAndMalformedPacketsWithoutThrowing()
    {
        var manager = new Protocol04Manager();
        byte[][] malformed =
        {
            Array.Empty<byte>(),
            new byte[] { 0x01 },                         // 1 block, missing seq+count
            new byte[] { 0x01, 0x00, 0x01 },             // missing rpcInside
            new byte[] { 0x01, 0x00, 0x01, 0x01 },       // missing rpc length
            new byte[] { 0x01, 0x00, 0x01, 0x01, 0x80 }, // truncated 2-byte length
            new byte[] { 0x01, 0x00, 0x01, 0x01, 0x01, 0x80 }, // 2-byte header truncated
            new byte[] { 0x01, 0x00, 0x01, 0x01, 0x01, 0x80, 0x01 }, // length underflows (< 0 after 2-byte header)
            new byte[] { 0x01, 0x00, 0x01, 0x01, 0x10, 0x05, 0xaa }, // payload shorter than declared length
        };

        foreach (byte[] raw in malformed)
        {
            byte[] packet = raw;
            int end = manager.parse(0, ref packet);
            Assert.Equal(packet.Length, end);
        }
    }

    [Fact]
    public void MultiProtocolManagerHandlesTruncatedSimTimeAndEndOfPacketSectionsSafely()
    {
        WorldClient previous = Store.currentClient;
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        typeof(WorldClient).GetProperty("IsBot")!.SetValue(client, true);
        client.playerData = new ClientData();
        client.messageQueue = new MessageQueue();
        Store.currentClient = client;
        try
        {
            var mpm = new MultiProtocolManager();

            // Empty encrypted payload.
            mpm.Parse(true, Array.Empty<byte>());

            // Truncated simTime (flag 0x82 requires 4 sim-time bytes after flag).
            mpm.Parse(true, new byte[] { 0x82, 0x00, 0x00 });

            // SimTime-only ACK packet (exact 5 bytes; previously threw IndexOutOfRangeException on 0x05/0x03 check).
            mpm.Parse(true, new byte[] { 0x82, 0x00, 0x00, 0x80, 0x3f });
            Assert.Equal(1.0f, client.playerData.getClientSimTime());

            // 0x05 section ending exactly at packet end (previously threw IndexOutOfRangeException on 0x03 check).
            mpm.Parse(true, new byte[] { 0x02, 0x05, 0x00, 0x00, 0x00 });
        }
        finally
        {
            Store.currentClient = previous;
        }
    }
}
