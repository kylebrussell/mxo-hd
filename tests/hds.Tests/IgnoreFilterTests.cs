using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using hds;
using hds.shared;

namespace hds.Tests;

/// <summary>Server-side ignore: tells, area, group and room lines from an ignored handle are not delivered.</summary>
[Collection("ChatAddress")]
public class IgnoreFilterTests
{
    private const uint Alice = 910001, Bob = 910002, Carol = 910003, Crew = 77;

    private static WorldClient Player(uint charId, string handle)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(c, new WorldSendFence());
        c.playerData = new ClientData();
        c.playerData.setCharID(charId);
        c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.CharacterName.setValue(handle);
        c.playerInstance.CrewID.setValue(NumericalUtils.uint32ToByteArray(Crew, 1));
        c.messageQueue = new MessageQueue();
        c.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, long.MaxValue);
        return c;
    }

    private static List<byte[]> Rpcs(WorldClient c) =>
        c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m => m.content.Skip(1).ToArray()).ToList();

    private static byte[] Sized(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        return BitConverter.GetBytes((ushort)(bytes.Length + 1)).Concat(bytes).Append((byte)0).ToArray();
    }

    /// <summary>Client 29 after its header byte: handle and text offsets, counter at +5 (entilsar capture layout).</summary>
    private static byte[] Tell(string handle, string text, byte counter)
    {
        byte[] name = Sized("SOE+MXO+Test+" + handle), body = Sized(text);
        ushort nameAt = 7, textAt = (ushort)(nameAt + name.Length);
        return BitConverter.GetBytes(nameAt).Concat(BitConverter.GetBytes(textAt)).Concat(new byte[] { 0, counter })
            .Concat(name).Concat(body).ToArray();
    }

    private static void WithPlayers(Action<WorldClient, WorldClient, WorldClient> test)
    {
        var oldClient = Store.currentClient;
        WorldServer.Clients ??= new Dictionary<string, WorldClient>();
        var saved = new Dictionary<string, WorldClient>(WorldServer.Clients);
        var alice = Player(Alice, "Alice");
        var bob = Player(Bob, "Bob");
        var carol = Player(Carol, "Carol");
        try
        {
            WorldServer.Clients.Clear();
            WorldServer.Clients["a"] = alice;
            WorldServer.Clients["b"] = bob;
            WorldServer.Clients["c"] = carol;
            Store.currentClient = alice;
            IgnoreFilter.Load(Bob, new[] { "alice" });
            IgnoreFilter.Load(Carol, null);
            test(alice, bob, carol);
        }
        finally
        {
            IgnoreFilter.Load(Bob, null);
            IgnoreFilter.Load(Carol, null);
            WorldServer.Clients.Clear();
            foreach (var kv in saved) WorldServer.Clients[kv.Key] = kv.Value;
            Store.currentClient = oldClient;
        }
    }

    [Fact]
    public void IgnoringCodeIsTheTwentyThirdChatResultName()
    {
        // client.dll lists LTOBJSRV_SOECHAT_* in code order from 0x3e: DESTAVATARDOESNTEXIST is 4th (0x41), IGNORING 23rd.
        Assert.Equal(0x0e000054u, IgnoreListPackets.Ignoring);
        Assert.Equal(new byte[] { 0x2c, 3, 0x54, 0, 0, 0x0e }, SocialPackets.TellResult(3, IgnoreListPackets.Ignoring));
    }

    [Fact]
    public void TellToAPlayerWhoIgnoresTheSenderIsRefusedWithIgnoring()
    {
        WithPlayers((alice, bob, carol) =>
        {
            byte[] toBob = Tell("Bob", "hi", 4);
            new ChatHandler().ProcessWhisperPlayer(ref toBob);
            Assert.Empty(Rpcs(bob));
            Assert.Equal(new byte[] { 0x2c, 4, 0x54, 0, 0, 0x0e }, Assert.Single(Rpcs(alice)));

            byte[] toCarol = Tell("Carol", "hi", 5);
            new ChatHandler().ProcessWhisperPlayer(ref toCarol);
            Assert.Single(Rpcs(carol));
            Assert.Equal(SocialPackets.TellResult(5, true), Rpcs(alice).Last());
        });
    }

    [Fact]
    public void AreaAndCrewLinesSkipOnlyTheListenerWhoIgnores()
    {
        WithPlayers((alice, bob, carol) =>
        {
            new ServerPackets().SendChatMessage(alice, "area line", Alice, "Alice", 7);
            Assert.Empty(Rpcs(bob));
            Assert.Single(Rpcs(carol));
            Assert.Empty(Rpcs(alice));

            Assert.True(SocialHandler.TrySendGroupChat(alice, SocialPackets.ChatCrew, "crew line"));
            Assert.Empty(Rpcs(bob));
            Assert.Equal(2, Rpcs(carol).Count);

            // Bob's own lines still reach Alice: ignore is one-way.
            Store.currentClient = bob;
            new ServerPackets().SendChatMessage(bob, "from bob", Bob, "Bob", 7);
            Assert.Single(Rpcs(alice));
        });
    }

    [Fact]
    public void AddAndRemoveKeepTheOnlineListCurrent()
    {
        var oldDb = IgnoreListHandler.Db;
        var oldCanonical = IgnoreListHandler.CanonicalHandle;
        WithPlayers((alice, bob, carol) =>
        {
            try
            {
                IgnoreListHandler.Db = new MemoryIgnoreDb();
                IgnoreListHandler.CanonicalHandle = h => string.Equals(h, "alice", StringComparison.OrdinalIgnoreCase) ? "Alice" : null;
                Assert.False(IgnoreFilter.Ignores(carol, alice));
                IgnoreListHandler.ProcessAdd(carol, Name("Alice"));
                Assert.True(IgnoreFilter.Ignores(carol, alice));
                IgnoreListHandler.ProcessRemove(carol, Name("ALICE"));
                Assert.False(IgnoreFilter.Ignores(carol, alice));
                Assert.True(IgnoreFilter.Ignores(bob, alice));
            }
            finally
            {
                IgnoreListHandler.Db = oldDb;
                IgnoreListHandler.CanonicalHandle = oldCanonical;
            }
        });

        static byte[] Name(string handle) => BitConverter.GetBytes((ushort)4).Concat(Sized(handle)).ToArray();
    }

    private sealed class MemoryIgnoreDb : IgnoreListDb
    {
        private readonly HashSet<string> rows = new(StringComparer.OrdinalIgnoreCase);
        public override List<string> List(uint charId) => rows.ToList();
        public override void Add(uint charId, string handle) => rows.Add(charId + ":" + handle);
        public override bool Remove(uint charId, string handle) => rows.Remove(charId + ":" + handle);
    }
}
