using hds;
namespace hds.Tests;
public class MissionComputer1407QaTests
{
    [Fact] public void OnlyExactDisposableActorMissionPhaseAndSourceCanLog()
    {
        bool Allowed(string dev="1",string db="mxotest_native_ability_abc",string flag="1",ulong actor=110,int mission=3082,int phase=0,ushort source=1407,uint key=0x07f005a3) =>
            MissionComputer1407Qa.Enabled(dev,db,flag,actor,mission,phase,source,key);
        Assert.True(Allowed());
        foreach(var dev in new[]{"", "true", "01", "0"})Assert.False(Allowed(dev:dev));
        foreach(var db in new[]{"mxotest_", "mxotest", "mxotest_a;hds", "hds", "MXOTEST_native_a", "mxotest_a-b", "mxotest_é"})Assert.False(Allowed(db:db));
        Assert.False(Allowed(flag:""));Assert.False(Allowed(flag:"true"));
        Assert.False(Allowed(actor:2));Assert.False(Allowed(actor:111));
        Assert.False(Allowed(mission:3072));Assert.False(Allowed(phase:1));
        Assert.False(Allowed(source:1415));Assert.False(Allowed(key:0x07f005a2));
    }
    [Fact] public void OnlyDiggerModeOptsIntoReadOnlyPickingEvidence()
    {
        var plan=NativeQa.NativeQaModes.Find("mission-computer-1407").Plan;
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor("mission-computer-1407");
        Assert.Equal("1",env["MXO_MISSION_COMPUTER_1407_QA"]);Assert.Equal("2",env["MXO_MISSION_SITE_QA_SEED"]);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("mission-computer-anchors")["MXO_MISSION_COMPUTER_1407_QA"]);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("unknown")["MXO_MISSION_COMPUTER_1407_QA"]);
    }
}
