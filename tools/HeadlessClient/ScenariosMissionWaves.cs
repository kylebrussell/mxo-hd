using System.Text;
using System.Xml.Linq;

namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// Empty Vessel (11.3.3), phase 1: the Machine Captains arrive when the mainframe is used, the kill goal completes
    /// once they are dead without waiting for the extra security that comes after the Effectuator's talk (a &lt;wave&gt;
    /// after a later goal), and that wave arrives then. Office Party (10.3.1), phase 2: Maxwell Boren turns hostile
    /// when Andreas Myers is talked to (hostileAfter): his friendly view is deleted and he is created again, hostile.
    /// </summary>
    private static async Task MissionWavesAndSideSwitch(ScenarioContext context)
    {
        XElement[] definitions = MerovingianDefinitions(context);

        // Empty Vessel
        XElement vessel = definitions.Single(definition => (int)definition.Attribute("id")! == 311303);
        HeadlessPlayer player = await StartMerovingianStoryMission(context, vessel, definitions);
        await EnterMissionArea(player);
        await UseMissionObject(context, player, "Mainframe", "04");
        await player.WaitForTextAsync("Could be a toughie");
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Count(view => view.Name == "Machine Captain") >= 2,
            "Two Machine Captains", TimeSpan.FromSeconds(10));
        foreach (ViewCreation captain in player.World.CreationSnapshot().Where(view => view.Name == "Machine Captain").ToList())
        {
            await DefeatMissionNpc(player, captain);
        }

        // The kill goal is done with the Captains: the wave after the Effectuator's talk doesn't hold it up
        // (its done line can come before the second kill's loot window, so this looks at everything received)
        byte[] doneLine = Encoding.Latin1.GetBytes("Well, look who decided to show.");
        await player.WaitUntilAsync(() => player.World.RpcSnapshot().Any(record => BytePattern.IndexOf(record.Bytes, doneLine, 0) >= 0),
            "The Captains' kill goal done", TimeSpan.FromSeconds(10));
        Check.That(!context.Server.DebugLog().Contains("(wave 1)"), "The extra security comes only after the talk");
        Check.That(!player.World.CreationSnapshot().Any(view => view.Name == "Machine Program"), "No extra security yet");

        await TalkToMissionNpc(player, "The Effectuator");
        await player.WaitForTextAsync("Mainframe access enabled");
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("[MISSION] 2 hostiles arrive (wave 1)"),
            "The extra security arrives", TimeSpan.FromSeconds(10));
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Count(view => view.Name == "Machine Program") >= 2,
            "Two Machine Programs", TimeSpan.FromSeconds(10));

        // The data, with the extra security still standing: killing it isn't a goal
        await UseMissionObject(context, player, "Mainframe", "05");
        await player.WaitForRpcAsync(GoalsCompleteLeaveLine, TimeSpan.FromSeconds(10));
        player.SendRpc("80 a6");
        await player.WaitForRpcAsync("80 a2 08");

        // Office Party
        XElement party = definitions.Single(definition => (int)definition.Attribute("id")! == 310301);
        player = await StartMerovingianStoryMission(context, party, definitions);
        await EnterMissionArea(player);
        await UseMissionObject(context, player, "Computer", "04");
        await player.WaitForTextAsync("A secret party, eh?");
        await player.WaitForRpcAsync(GoalsCompleteLeaveLine);
        await LeaveMissionArea(player);
        await player.WaitForTextAsync("cheap booze-ups");
        await player.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(player);

        ViewCreation boren = await player.WaitForViewNamedAsync("Maxwell Boren");
        Check.That(boren.Attributes.ContainsKey("DontAttack") && !boren.Attributes.ContainsKey("IsEnemy"), "Boren is friendly at the party");
        int seen = player.World.CreationSnapshot().Count;
        await TalkToMissionNpc(player, "Andreas Myers");
        await player.WaitForTextAsync("You bashtard! C'mere!");
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("[MISSION] Maxwell Boren turns hostile"),
            "Boren turns hostile", TimeSpan.FromSeconds(10));

        // His friendly view goes (01 00 01 01 00 <view>) and he comes back where he stood, hostile
        byte[] delete = { 0x01, 0x00, 0x01, 0x01, 0x00, (byte)boren.ViewId, (byte)(boren.ViewId >> 8) };
        await player.WaitUntilAsync(() => player.World.ViewSnapshot().Any(section => BytePattern.IndexOf(section, delete, 0) >= 0),
            "Boren's friendly view deleted", TimeSpan.FromSeconds(10));
        ViewCreation? hostile = null;
        await player.WaitUntilAsync(() =>
        {
            hostile = player.World.CreationSnapshot().Skip(seen).LastOrDefault(view => view.Name == "Maxwell Boren");
            return hostile != null;
        }, "Boren created again", TimeSpan.FromSeconds(10));
        Check.That(hostile!.Attributes.ContainsKey("IsEnemy") && !hostile.Attributes.ContainsKey("DontAttack") &&
                   !hostile.Attributes.ContainsKey("TalkDefaultable"), "Boren comes back hostile");
        Check.That(hostile.Attributes["Position"].SequenceEqual(boren.Attributes["Position"]), "Boren comes back where he stood");

        // Leading Andreas out still ends the phase
        await LeaveMissionArea(player);
        await player.WaitForRpcAsync(GoalsCompleteLine, TimeSpan.FromSeconds(15));
        player.SendRpc("80 a6");
        await player.WaitForRpcAsync("80 a2 08");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions " +
            $"WHERE charId={player.CharId} AND missionId=310301"), "Aborted Office Party stays incomplete");
    }
}
