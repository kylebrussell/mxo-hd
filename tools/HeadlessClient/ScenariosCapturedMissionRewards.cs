using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task CapturedRewards17(ScenarioContext c)=>CapturedMissionRewards(c,17);
    private static Task CapturedRewards50(ScenarioContext c)=>CapturedMissionRewards(c,50);
    private static async Task CapturedMissionRewards(ScenarioContext context,int level)
    {
        string label="captured-rewards-"+level,handle="CapturedRewards"+level;var expected=CapturedMissionRewardsFixture.Expected(level);
        // Normal list/accept/Open/exit/receipt flow; no direct completion call or reward injection.
        await RunPrimaChain(context,handle,2,new[]{201202},PosterHuntFixture.Prerequisites,"machines_1_2_*.xml",evidenceLabel:label,actorLevel:level);
        string path=Path.Combine(Path.GetTempPath(),"mxo-"+label+"-"+context.Database.Name+".json");
        using var proof=JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var summaries=proof.RootElement.GetProperty("Rpc").EnumerateArray().Select(e=>Convert.FromHexString(e.GetString()!)).Where(b=>b.Length>=19&&b[0]==0x80&&b[1]==0xa2&&b[2]==0).ToArray();
        Check.Equal(1,summaries.Length,"One ordinary mission summary");Check.Equal(expected.Info,BitConverter.ToUInt32(summaries[0],11),"Literal expected information quote");Check.Equal(expected.Xp,BitConverter.ToUInt32(summaries[0],15),"Literal expected XP quote");
        long id=await context.Database.LongAsync($"SELECT charId FROM characters WHERE handle='{handle}'");
        long xp=await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={id}");long info=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={id}");
        Check.Equal((long)PlayerProgression.ExperienceForLevel(level)+expected.Xp,xp,"Persisted XP delta equals quote");Check.Equal(1000L+expected.Info,info,"Persisted info delta equals quote");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={id} AND missionId=201202"),"Exactly one earned receipt");
        await File.WriteAllTextAsync(path+".payout.json",JsonSerializer.Serialize(new{RequestedLevel=level,ExpectedXp=expected.Xp,ExpectedInfo=expected.Info,Summary=Convert.ToHexString(summaries[0]),FinalXp=xp,FinalInfo=info,RawEvidence=path,Scope="Normal Poster Hunt payout; archive point applied through existing OURS curve, not native retail proof"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"  CAPTURED REWARD level={level} normal summary/DB/receipt PASS: XP={expected.Xp}, info={expected.Info}");
    }
}
