using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task MissionRewardsWelcome(ScenarioContext context)=>MissionRewardsAtTwoLevels(context,false);
    private static Task MissionRewardsHothouse(ScenarioContext context)=>MissionRewardsAtTwoLevels(context,true);
    private static async Task MissionRewardsAtTwoLevels(ScenarioContext context,bool ticket)
    {
        foreach(int level in ticket?new[]{30,31}:new[]{2,3})
        {
            HeadlessPlayer player;
            if(ticket)
            {
                var definitions=MerovingianDefinitions(context);
                var definition=definitions.Single(m=>(int)m.Attribute("id")! ==312101);
                player=await StartMerovingianStoryMission(context,definition,definitions,level);
                player.SendRpc("65 01 00 60 00");await player.WaitForRpcAsync("65 01 00 60 00");
                await EnterMissionArea(context,player);await PlayHothouse(context,player);
            }
            else
            {
                player=await context.AddPlayerAsync(new CharacterSeed("Reward"+level){X=58800,Y=2495,Z=31900,
                    Level=level,Experience=(int)PlayerProgression.ExperienceForLevel(level)});
                await player.EnterWorldAsync();
                player.SendRpc("80 94 05 00 01");await player.WaitForTextAsync("Welcome to the Matrix");await player.WaitForRpcAsync("80 97 05 00");
                player.SendRpc("80 98 06 00 00 00 01 02 00");await player.WaitForRpcAsync("80 99");
                player.SendRpc("80 9b 07 00 01");await player.WaitForRpcAsync("80 9c");await player.WaitForRpcAsync("80 9e 00");
                foreach(string name in new[]{"Abalakin","Rigel","Crow"})await TalkToMissionNpc(player,name);
                await player.WaitForTextAsync("Kill the simulacrum");
                await DefeatMissionNpc(player,await player.WaitForViewNamedAsync("Watchman"));
            }
            await player.WaitUntilAsync(()=>player.Received(GoalsCompleteLeaveLine).Count>0,"Actual phase goals complete before exit",TimeSpan.FromSeconds(10));
            long beforeXp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={player.CharId}");
            long beforeInfo=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}");
            uint xp=ticket?(level==30?239105u:247350u):(level==2?5500u:11000u);
            uint info=ticket?(level==30?105000u:108500u):(level==2?21000u:31500u);
            int missionId=ticket?312101:1201;
            await LeaveMissionArea(player);
            RpcRecord result=await player.WaitForRpcAsync("80 a2 00",TimeSpan.FromSeconds(20));
            Check.Equal(info,BitConverter.ToUInt32(result.Bytes,11),"Completion summary uses scaled info captured before level-up");
            Check.Equal(xp,BitConverter.ToUInt32(result.Bytes,15),"Completion summary uses scaled XP");
            await player.WaitForRpcAsync("80 e6 "+WireFormat.ToHex(BitConverter.GetBytes(xp))+" 02 00 00 00");
            await player.WaitForRpcAsync("80 e4 "+WireFormat.ToHex(BitConverter.GetBytes((uint)(beforeInfo+info)))+" 02 00 00 00");
            async Task AssertPaidOnce()
            {
                Check.Equal(beforeXp+xp,await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={player.CharId}"),"Exactly one scaled XP payout persisted");
                Check.Equal(beforeInfo+info,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={player.CharId}"),"Exactly one scaled info payout persisted");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={player.CharId} AND missionId={missionId}"),"Legacy mission completion counted once");
            }
            await AssertPaidOnce();
            // Repeat the completion-triggering movement and an old NPC action after the mission has ended.
            await LeaveMissionArea(player);player.SendRpc("80 c7 ff 7f 00 00 09 00");await Task.Delay(700);await AssertPaidOnce();
            // Actual quit handshake clears the online flag; disposing UDP/TCP alone is not a character logout.
            player.SendRpc("80 fe 00");await player.WaitForRpcAsync("80 fd");
            Check.Equal(0L,await context.Database.LongAsync($"SELECT is_online FROM characters WHERE charId={player.CharId}"),"Native quit path released the character");
            player.Dispose();
            // Margin's character reservation is removed by the normal world inactivity cleanup (>30s), not80fe alone.
            Console.WriteLine($"  mission={missionId} level={level}: paid once; waiting normal UDP logout cleanup before relog");
            await Task.Delay(TimeSpan.FromSeconds(33));
            using(var relog=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+player.CharId,player.Handle))
            {
                await relog.EnterWorldAsync();await Task.Delay(700);
                Check.Equal(0,relog.Received("80 a2 00").Count,"Relog never replays a completed payout");
                Check.Equal(0,relog.Received("80 e6 ?? ?? ?? ?? 02 00 00 00").Count,"Relog grants no mission XP");
                await AssertPaidOnce();
                Check.Equal(0,relog.World.Problems.Count,"Relog wire parsing");
            }
            Check.Equal(0,player.World.Problems.Count,"Reward summary wire parsing");
            Console.WriteLine($"  mission={missionId} level={level} XP={xp} info={info}: summary/gain/DB/duplicate/relog checked");
        }
    }
}
