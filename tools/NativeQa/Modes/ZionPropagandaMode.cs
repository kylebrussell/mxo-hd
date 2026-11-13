using hds;
namespace NativeQa.Modes;
public sealed class ZionPropagandaMode : NativeQaMode
{
    public override string Name=>"zion-propaganda-count";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,1006,77},17000,2300,
        Array.Empty<string>(),Array.Empty<string>(),new[]{HeadlessClient.ZionPropagandaFixture.SeedPrerequisites(110),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="101101"},
        extraQaMetadata:new(){["ZionPropagandaCountQa"]=true,["RequiredProxyCounts"]="6,4",["AuthoredBoxModels"]=true},
        readyMessages:new[]{"CSR 110: Call Tyndall > Critical > The Power of Propaganda. Normal elevators; existing authored Box proxies only. First building: five must remain incomplete, sixth completes the area. Second: three remain incomplete, fourth completes. Leave normally and verify one reward pair. Box source poses/model/picking remain unverified; stop and report unavailable Open instead of changing reach or placement. No GM or forced combat roll. See docs/MISSION-ZION-PROPAGANDA-COUNT.md."});
}
