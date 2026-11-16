using System;
using System.IO;
using System.Linq;
using HeadlessClient;
using hds;
using NativeQa.Modes;
using Xunit;
public class OpenWorldFirstLevelTests
{
    [Fact] public void CapturedWanderingTargetUsesBoundedInsetWithoutStraightLineClaim()
    {
        var root=FindRoot();SectorProperties.Load(Path.Combine(root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data/navmesh"));
        double[] target={20017.978515625,-705,61984.71484375};
        Assert.False(NavMesh.Project(1,target[0],target[1],target[2]-100,out _,maxDistance:1,reachableFrom:OpenWorldFirstLevelFixture.Start,requireDirect:true));
        var approach=OpenWorldFirstLevelFixture.Approach(target);
        Assert.Equal(20020,approach.x);Assert.Equal(-705,approach.y);Assert.Equal(61884.71484375,approach.z);
        Assert.Equal((ushort)524,approach.location.sector);Assert.False(approach.location.indoors);
        foreach(var spawn in OpenWorldFirstLevelFixture.Spawns)Assert.Equal(spawn[2]-100,OpenWorldFirstLevelFixture.Approach(spawn).z);
        Assert.Throws<InvalidDataException>(()=>OpenWorldFirstLevelFixture.Approach(new[]{19900d,-705d,61984d}));
        Assert.Throws<InvalidDataException>(()=>OpenWorldFirstLevelFixture.Approach(new[]{target[0],-305d,target[2]}));
        Assert.Throws<InvalidDataException>(()=>OpenWorldFirstLevelFixture.Approach(new[]{double.NaN,-705d,target[2]}));
        Assert.Throws<InvalidDataException>(()=>OpenWorldFirstLevelFixture.Approach(new[]{0d,-705d,0d}));
    }
    [Fact] public void ExistingCapturedPairSurvivesProductionDedupAndIsGrounded()
    {
        var root=FindRoot();var seen=new MobSpawnFilter.Seen();var kept=File.ReadLines(Path.Combine(root,"data/npc_spawns/OpenWorld.csv")).Skip(1).Select(l=>l.Split(';')).Where(row=>!seen.IsRepeat(ushort.Parse(row[0]),row[2],double.Parse(row[7]),double.Parse(row[9]),double.Parse(row[8]))).ToArray();
        SectorProperties.Load(Path.Combine(root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data/navmesh"));
        foreach(var p in OpenWorldFirstLevelFixture.Spawns)
        {
            var row=Assert.Single(kept.Where(row=>row[0]=="1"&&row[2]=="Chopper Runner"&&double.Parse(row[7])==p[0]&&double.Parse(row[8])==p[1]&&double.Parse(row[9])==p[2]));
            Assert.Equal("1",row[3]);Assert.Equal("75",row[4]);Assert.Equal("75",row[5]);
            Assert.True(NavMesh.Locate(1,p[0],p[1],p[2],out var nav));Assert.False(nav.indoors);Assert.Equal(-800,nav.floorY);
            Assert.True(NavMesh.Project(1,p[0],p[1],p[2],out var at,maxDistance:1,reachableFrom:OpenWorldFirstLevelFixture.Start,requireDirect:true));Assert.Equal(p,at.Position);
        }
    }
    static string FindRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir!=null&&!File.Exists(Path.Combine(dir.FullName,"data/npc_spawns/OpenWorld.csv")))dir=dir.Parent;return dir?.FullName??throw new DirectoryNotFoundException();}
    [Fact] public void ActualEncoderMatchesIndependentEarnedGainAndLevelWitness()
    {
        var gain=ServerPackets.BuildEXPGain(391);Assert.True(OpenWorldFirstLevelFixture.FightingGain(gain,391));Assert.False(OpenWorldFirstLevelFixture.FightingGain(ServerPackets.BuildEXPGain(391,2),391));Assert.False(OpenWorldFirstLevelFixture.FightingGain(gain[..^1],391));
        var level=ServerPackets.BuildLevelUpUpdate(2,200,103,80,120,1);Assert.True(OpenWorldFirstLevelFixture.OwnLevelTwo(level));
        Assert.False(OpenWorldFirstLevelFixture.OwnLevelTwo(ServerPackets.BuildLevelUpUpdate(3,250,106,80,120,1)));Assert.False(OpenWorldFirstLevelFixture.OwnLevelTwo(level[..^1]));
        var wrong=(byte[])level.Clone();wrong[17]^=1;Assert.False(OpenWorldFirstLevelFixture.OwnLevelTwo(wrong));
    }
    [Fact] public void StrictPinAndMalformedLootRefuse()
    {
        string pin=new('A',64);OpenWorldFirstLevelFixture.RequireSource(pin,pin.ToLowerInvariant());Assert.Throws<InvalidDataException>(()=>OpenWorldFirstLevelFixture.RequireSource(null,pin));Assert.Throws<InvalidDataException>(()=>OpenWorldFirstLevelFixture.RequireSource(new string('B',64),pin));
        Assert.Throws<InvalidDataException>(()=>OpenWorldFirstLevelFixture.LootInfo(new byte[11]));Assert.Throws<InvalidDataException>(()=>OpenWorldFirstLevelFixture.LootInfo(new byte[12]));
    }
    [Fact] public void NativeAndWireStartAtZeroWithoutDifficultyOverrides()
    {
        var plan=new OpenWorldFirstLevelMode().Plan;Assert.Equal(new ushort[]{2},plan.Abilities);Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.DoesNotContain("MXO_AGGRO",plan.EnvironmentOverrides.Keys);Assert.DoesNotContain("MXO_MOB_WANDER",plan.EnvironmentOverrides.Keys);
        var seed=OpenWorldFirstLevelFixture.Seed;Assert.Equal(0,seed.Experience);Assert.Equal(1,seed.Level);Assert.Equal(150,seed.MaxHealth);Assert.Equal(100,seed.MaxInnerStrength);
        Assert.Equal("0",plan.EnvironmentOverrides["MXO_DEV_COMMANDS"]);
        var wire=OpenWorldFirstLevelFixture.HeadlessEnvironment;
        Assert.Equal("1",wire["MXO_DEV_COMMANDS"]);
        Assert.Equal(OpenWorldFirstLevelFixture.Environment.Keys.OrderBy(k=>k),wire.Keys.OrderBy(k=>k));
        foreach(var pair in OpenWorldFirstLevelFixture.Environment.Where(p=>p.Key!="MXO_DEV_COMMANDS"))Assert.Equal(pair.Value,wire[pair.Key]);
    }
}
