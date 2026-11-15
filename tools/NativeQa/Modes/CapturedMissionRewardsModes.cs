using HeadlessClient;
namespace NativeQa.Modes;
internal static class CapturedRewardPlans
{
    public static NativeQaModePlan For(int level)
    {
        var expected=CapturedMissionRewardsFixture.Expected(level);
        return new(new ushort[]{2,17,1006,77},17000,2300,Array.Empty<string>(),Array.Empty<string>(),
            new[]{PosterHuntFixture.SeedPrerequisites(110),CapturedMissionRewardsFixture.ActorSql(level)},
            DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
            environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="201202"},
            extraQaMetadata:new(){["CapturedRewardActorLevel"]=level,["CapturedRewardExpectedXp"]=expected.Xp,["CapturedRewardExpectedInfo"]=expected.Info,["ObservedArchiveReference"]=17},
            readyMessages:new[]{"CSR 110: normal Agent Gray Call > Critical > Poster Hunt. Existing authored Box proxies 3/1/4; finish through ordinary Open/exit actions, no forced completion. Read actual completion summary/chat: "+expected.Xp+" XP and "+expected.Info+" information. Level 17 matches the observed post-completion archive HUD point; level 50 is OURS extrapolation, not retail proof. Native Box picking/placement remains pending; stop/report unavailable Open. See docs/MISSION-CAPTURED-REFERENCES.md."});
    }
}
public sealed class CapturedRewards17Mode : NativeQaMode
{
    public override string Name=>"captured-rewards-17";
    public override NativeQaModePlan Plan {get;}=CapturedRewardPlans.For(17);
}
public sealed class CapturedRewards50Mode : NativeQaMode
{
    public override string Name=>"captured-rewards-50";
    public override NativeQaModePlan Plan {get;}=CapturedRewardPlans.For(50);
}
