using NativeQa.Modes;
using HeadlessClient;
using hds;
using hds.shared;
using System.Runtime.CompilerServices;
namespace hds.Tests;
[Collection("DelayedActions")]
public class DebuffRadiusModeTests
{
    [Fact] public void ModeUsesOrdinaryPvpServerAutoflagsAndIsolatedExactBoundaries()
    {
        var plan=new DebuffRadiusMode().Plan;
        Assert.Contains((ushort)345,plan.Abilities);Assert.Contains((ushort)333,plan.Abilities);Assert.DoesNotContain((ushort)75,plan.Abilities);
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.Empty(plan.Bootstrap);
        Assert.Equal("1",plan.EnvironmentOverrides["MXO_PVP_SERVER"]);Assert.Equal("15",plan.EnvironmentOverrides["MXO_PVP_MAX_SAFE_LEVEL"]);
        Assert.Equal("1",plan.EnvironmentOverrides[AreaTargetDiagnostics.Variable]);
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor("debuff-radius");
        Assert.Equal("1",env["MXO_FREECAST_VIRAL_QA"]);Assert.Equal("32766",env["MXO_FREECAST_VIRAL_QA_ATTACK15"]);Assert.Equal("15636",env["MXO_FREECAST_VIRAL_QA_DEFENSE15"]);
        Assert.Equal("",env["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL100"]);
        Assert.True(plan.ExtraQaMetadata["FreeCastViralContestQa"].GetBoolean());
        foreach(string scenario in new[]{"direct-debuff-radius","debuff-radius-native-fixture"})
        {var selected=Scenarios.All.Single(s=>s.Name==scenario);Assert.Equal("1",selected.Environment!["MXO_FREECAST_VIRAL_QA"]);Assert.Equal("15636",selected.Environment["MXO_FREECAST_VIRAL_QA_DEFENSE15"]);}

        Assert.Equal(DebuffRadiusFixture.NativeTargetsSql(6111,6112,6113,6114),plan.AfterHelpers[1]);
        Assert.Contains("WHEN 6112 THEN 17940",plan.AfterHelpers[1]);Assert.Contains("WHEN 6113 THEN 17500",plan.AfterHelpers[1]);Assert.Contains("WHEN 6113 THEN 2799",plan.AfterHelpers[1]);
        Assert.Equal(17000,plan.X);Assert.Equal(2300,plan.Z);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("unknown-mode")[AreaTargetDiagnostics.Variable]);
    }
    [Theory]
    [InlineData("1","mxotest_native_a",true)] [InlineData("1","reality_hd",false)]
    [InlineData("true","mxotest_native_a",false)] [InlineData("1","mxotest_",false)]
    [InlineData("1","mxotest_bad-name",false)] [InlineData("1","MXOTEST_native_a",false)]
    [InlineData("0","mxotest_native_a",false)]
    public void LoggingHasNoGeneralDevOrProductionAuthority(string opt,string db,bool expected)=>Assert.Equal(expected,AreaTargetDiagnostics.Allowed(opt,db));
    [Fact] public void AllNativeCoordinatesStandOnSameShippedFloorAwayFromAmbientPile()
    {
        string root=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(root,"data","sector_properties.csv")))root=Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        foreach(var p in new[]{(X:17000,Z:2300),(X:17500,Z:2300),(X:17880,Z:2300),(X:17940,Z:2300),(X:17500,Z:2799)})
        {Assert.True(NavMesh.Locate(1,p.X,495,p.Z,out var n),$"NAV at {p.X},495,{p.Z}");Assert.Equal(915,n.sector);Assert.Equal(400,n.floorY);}
        Assert.False(AbilityTargets.WithinArea(17500,495,2300,17500,495,2799,4.84));
        foreach(string row in File.ReadLines(Path.Combine(root,"data","mob_just_some_and_winter.csv")).Skip(1).Where(s=>s.StartsWith("1;")))
        {var c=row.Split(';');double x=double.Parse(c[7],System.Globalization.CultureInfo.InvariantCulture),z=double.Parse(c[9],System.Globalization.CultureInfo.InvariantCulture);Assert.True((x-17500)*(x-17500)+(z-2300)*(z-2300)>5000*5000);}
    }
    static WorldClient Client(uint id,uint org,bool flag,double x=17880,double y=495)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerData.setCharID(id);c.playerData.setEntityId(id);c.playerData.setDistrictId(1);c.playerData.pvpFlag=flag;
        c.playerInstance=new PlayerCharacter();c.playerInstance.Level.setValue((byte)50);c.playerInstance.OrganizationID.setValue((byte)org);c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x,y,2300));c.viewMan=new ViewManager();return c;
    }
    [Fact] public void ExactRuntimePredicateStillRejectsIllegalOrOutOfRangePlayers()
    {
        var old=Store.worldConfig;
        try
        {
            Store.worldConfig=(WorldConfig)RuntimeHelpers.GetUninitializedObject(typeof(WorldConfig));Store.worldConfig.IsPvpServer=false;Store.worldConfig.PvpMaxSafeLevel=15;
            var a=Client(1001,2,true,17000);var b=Client(1002,1,true);a.viewMan.addView(1002,12);
            string Reason(double radius=4)=>AbilityHostile.PlayerAreaReason(a,b,17500,495,2300,radius,null,out _);
            PvpFlag.OnEnterWorld(b);Assert.False(b.playerData.pvpFlag);Assert.Equal("pvp-NotOpponent",Reason());
            Store.worldConfig.IsPvpServer=true;PvpFlag.OnEnterWorld(b);Assert.True(b.playerData.pvpFlag);Assert.Equal("included",Reason());
            string line=AreaTargetDiagnostics.Describe(a,b,"included",PvpRules.Verdict.Allowed,17500,495,2300,4);
            Assert.Contains("org/alignment=2 flag=True",line);Assert.Contains("org/alignment=1 flag=True",line);Assert.Contains("verdict=Allowed reason=included",line);
            b.playerInstance.OrganizationID.setValue((byte)2);Assert.Equal("pvp-NotOpponent",Reason());b.playerInstance.OrganizationID.setValue((byte)1);
            a.playerInstance.MissionTeamID.setValue((uint)42);b.playerInstance.MissionTeamID.setValue((uint)42);Assert.Equal("pvp-NotOpponent",Reason());a.playerInstance.MissionTeamID.setValue((uint)0);b.playerInstance.MissionTeamID.setValue((uint)0);
            b.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17940,495,2300));Assert.Equal("radius",Reason());Assert.Equal("included",Reason(4.84));
            b.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17880,645,2300));Assert.Equal("floor",Reason());b.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17880,495,2300));
            a.viewMan.Clear();Assert.Equal("invisible",Reason());a.viewMan.addView(1002,12);b.playerData.isDead=true;Assert.Equal("dead",Reason());b.playerData.isDead=false;b.playerData.duelDeath=true;Assert.Equal("duel-death",Reason());b.playerData.duelDeath=false;b.playerData.setDistrictId(2);Assert.Equal("district",Reason());b.playerData.setDistrictId(1);
            Assert.Equal("primary",AbilityHostile.PlayerAreaReason(a,b,17500,495,2300,4,b,out _));Assert.Equal("caster",AbilityHostile.PlayerAreaReason(a,a,17500,495,2300,4,null,out _));
        }
        finally{Store.worldConfig=old;}
    }
}
