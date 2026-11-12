using hds;
namespace NativeQa.Modes;
public abstract class MissionRewardsFixtureMode : NativeQaMode
{
    protected MissionRewardsFixtureMode(int level)
    {
        uint xp=level==2?5500u:11000u,info=level==2?21000u:31500u;
        Plan=new(new ushort[]{2,17},58800,31900,Array.Empty<string>(),Array.Empty<string>(),
            new[]{"UPDATE characters SET level="+level+",exp="+PlayerProgression.ExperienceForLevel(level)+
                ",y=2495,cash=1000,healthC=500,healthM=500,innerStrC=500,innerStrM=500 WHERE charId=110"},
            DevCommands:true,extraQaMetadata:new(){["MissionRewardsQa"]=true,["MissionRewardsPolicy"]="OURS explicit reference; archive default50",
                ["MissionRewardsDefaultArchiveReference"]=MissionRewards.DefaultArchiveReference,["MissionRewardsTemplate"]=1201,["MissionRewardsStartLevel"]=level,["MissionRewardsExpectedXp"]=xp,["MissionRewardsExpectedInfo"]=info},
            readyMessages:new[]{"Mission rewards: Tyndall > Welcome to the Matrix; complete radial Talk/Close Combat, then leave the phase. Expected XP="+xp+", info="+info+". ?weaken 1 is only optional combat preparation; rewards have no random outcome."});
    }
    public override NativeQaModePlan Plan{get;}
}
public sealed class MissionRewardsMode : MissionRewardsFixtureMode
{
    public MissionRewardsMode():base(2){}
    public override string Name=>"mission-rewards";
}
