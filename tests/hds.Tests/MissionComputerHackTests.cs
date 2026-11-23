using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using hds;
using hds.shared;

namespace hds.Tests;

public class MissionComputerHackTests
{
    [Theory]
    [InlineData(1, 335)]
    [InlineData(2, 335)]
    [InlineData(19, 6030)]
    [InlineData(50, 16415)]
    public void HackExperienceUsesTheShippedWeaklingBasisWithoutAKillLevelFactor(int level, int experience) =>
        Assert.Equal((uint)experience, MissionPersistence.ComputerHackExperience(level));

    [Fact]
    public void TheOptionalCamilloHackNeedsTheLiveHackerAbilityAndTheCurrentHackAction()
    {
        var xml = new XmlDocument();
        xml.Load(DataLoader.DataPath("missions", "machines_1_1_3_scheduled_for_deletion.xml"));
        var (client, progress, view) = Start(xml, 3);

        Assert.True(progress.Phase.missionObjectives[0].optional);
        Assert.True(progress.Phase.missionObjectives[0].computerHack);
        Assert.Equal(MissionStaging.ComputerHack, progress.objects[1].shownState);

        Use(client, view, 4, 7);
        Assert.False(progress.IsDone(1));
        Assert.Contains(Rpcs(client), bytes => Encoding.ASCII.GetString(bytes).Contains("Load Hacker"));

        client.playerData.passives.Loaded[52] = 2;
        Use(client, view, 5, 7); // Take does not execute the authored Hack.
        Use(client, view, 4, 6); // A retired view generation cannot execute it.
        Assert.False(progress.IsDone(1));
        Assert.Equal(MissionStaging.ComputerHack, progress.objects[1].shownState);
        Assert.DoesNotContain(Rpcs(client), CompletedHack);

        Use(client, view, 4, 7);
        Assert.True(progress.IsDone(1));
        Assert.False(progress.IsDone(2));
        Assert.False(progress.IsDone(3));
        Assert.Equal(MissionStaging.ComputerIdle, progress.objects[1].shownState);
        Assert.Single(Rpcs(client), CompletedHack);
    }

    [Fact]
    public void AnUnmarkedComputerSearchStillCompletesWithoutHacker()
    {
        var xml = new XmlDocument();
        xml.LoadXml("""
            <mission id="999" title="Search">
              <phase>
                <object id="1" kind="Computer" />
                <objective goal="1" type="Search" object="1" text="Find information" />
                <objective goal="2" type="Talk" npc="1" text="Continue" />
              </phase>
            </mission>
            """);
        var (client, progress, view) = Start(xml, 3);

        Use(client, view, 4, 7);

        Assert.True(progress.IsDone(1));
        Assert.False(AbilityPassives.IsLoaded(client, 52));
        Assert.Single(Rpcs(client), CompletedHack);
    }

    private static (WorldClient, MissionProgress, ClientView) Start(XmlDocument xml, ushort goid)
    {
        var progress = new MissionProgress(MissionXml.Parse(xml.DocumentElement!));
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.missionProgress = progress;
        client.viewMan = new ViewManager();
        client.messageQueue = new MessageQueue();
        var view = client.viewMan.addView(1000, goid);
        view.spawnId = 7;
        var state = new MissionObjectState { def = progress.Phase.objects[0], goid = goid, entity = view.entityId };
        state.shownState = MissionStaging.ComputerStateFor(progress, state);
        progress.objects.Add(state.def.id, state);
        return (client, progress, view);
    }

    private static void Use(WorldClient client, ClientView view, byte action, byte spawn)
    {
        using var pending = MissionOutput.Begin(client.messageQueue);
        var previous = Store.currentClient;
        try
        {
            Store.currentClient = client;
            byte[] body = { (byte)view.ViewID, (byte)(view.ViewID >> 8), spawn, 0, action, 0 };
            new ObjectInteractionHandler().processObjectDynamic(ref body);
            pending.Commit();
        }
        finally
        {
            Store.currentClient = previous;
        }
    }

    private static IEnumerable<byte[]> Rpcs(WorldClient client) =>
        client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Select(message => message.content[(message.content[0] >= 128 ? 2 : 1)..]);

    private static bool CompletedHack(byte[] rpc) =>
        rpc.Length > 5 && rpc[0] == 0x80 && rpc[1] == 0xa0 && rpc[2] == 1 && rpc[5] == 1;
}
