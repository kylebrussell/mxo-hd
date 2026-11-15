using HeadlessClient;
using hds;
using NativeQa;
namespace hds.Tests;
public class PvpOpponentsFixtureTests
{
    [Fact] public void ExplicitModeHasConsistentEntryInputsWithoutChangingDefaultOrOtherModes()
    {
        var mode=new NativeQa.Modes.PvpOpponentsMode();var plan=mode.Plan;
        Assert.Equal("pvp-opponents",mode.Name);Assert.Equal(new ushort[]{2,17},plan.Abilities);
        Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal("1",plan.EnvironmentOverrides["MXO_PVP_SERVER"]);Assert.Equal("15",plan.EnvironmentOverrides["MXO_PVP_MAX_SAFE_LEVEL"]);
        Assert.Contains("level=50",PvpOpponentsFixture.NativeSql);Assert.Contains("exp="+PlayerProgression.ExperienceForLevel(50),PvpOpponentsFixture.NativeSql);
        Assert.Contains("pvpflag=0",PvpOpponentsFixture.NativeSql);Assert.Contains("WHEN charId=6114 THEN 2 ELSE 1",PvpOpponentsFixture.NativeSql);
        Assert.DoesNotContain("duel",PvpOpponentsFixture.NativeSql,StringComparison.OrdinalIgnoreCase);
        Assert.Equal("0",PvpOpponentsFixture.Environment["MXO_DEV_COMMANDS"]);
        Assert.Equal("",NativeQaFixturePlan.EnvironmentFor("")["MXO_PVP_SERVER"]);
        Assert.DoesNotContain("MXO_PVP_SERVER",new NativeQa.Modes.ConditionalPowerlessMode().Plan.EnvironmentOverrides.Keys);
        Assert.False(plan.ExtraQaMetadata["NativeCsrEntryProven"].GetBoolean());
    }
    static ViewCreation View(byte org=2,uint flags=0x01000000,uint id=6114)=>new(3,32768,4,new Dictionary<string,byte[]>{
        ["CharacterID"]=BitConverter.GetBytes(id),["OrganizationID"]=new[]{org},["Level"]=new byte[]{50},["ConditionStateFlags"]=BitConverter.GetBytes(flags)});
    [Fact] public void ObserverWitnessRequiresActualIdentityOrgLevelAndFlagRatherThanMetadata()
    {
        PvpOpponentsFixture.Observe(View(),2,6114);
        Assert.ThrowsAny<Exception>(()=>PvpOpponentsFixture.Observe(View(flags:0),2,6114));
        Assert.ThrowsAny<Exception>(()=>PvpOpponentsFixture.Observe(View(org:1),2,6114));
        Assert.ThrowsAny<Exception>(()=>PvpOpponentsFixture.Observe(View(id:6111),2,6114));
        var attrs=View().Attributes.ToDictionary(p=>p.Key,p=>p.Value);attrs["Level"]=new byte[]{15};
        Assert.ThrowsAny<Exception>(()=>PvpOpponentsFixture.Observe(new ViewCreation(3,32768,4,attrs),2,6114));
        attrs=View().Attributes.ToDictionary(p=>p.Key,p=>p.Value);attrs["DuelID"]=BitConverter.GetBytes(2u);
        Assert.ThrowsAny<Exception>(()=>PvpOpponentsFixture.Observe(new ViewCreation(3,32768,4,attrs),2,6114));
    }
    [Fact] public void HealthWitnessKeepsDamageBeforeRegenerationButRejectsOldAndForeignViews()
    {
        var lists=new[]{WireFormat.FromHex("02000280808040a00f"),WireFormat.FromHex("030002808080400100"),
            WireFormat.FromHex("020002808080407313"),WireFormat.FromHex("020002808080408813")};
        Assert.Equal(new ushort[]{4979,5000},PvpOpponentsFixture.HealthHistory(lists,1,2,true));
        Assert.Empty(PvpOpponentsFixture.HealthHistory(lists,4,2,true));
        Assert.Empty(PvpOpponentsFixture.HealthHistory(lists,1,7,false));
    }
    [Fact] public void FixturePointsAreGroundedOnTheSameMaraFloor()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        foreach(int x in new[]{PvpOpponentsFixture.X,PvpOpponentsFixture.EnemyX,PvpOpponentsFixture.FriendX})
        {Assert.True(NavMesh.Locate(1,x,PvpOpponentsFixture.Y,PvpOpponentsFixture.Z,out var p));Assert.Equal(400,p.floorY);}
    }
}
