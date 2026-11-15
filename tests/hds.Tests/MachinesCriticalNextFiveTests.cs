using System.Xml;
using hds;
using HeadlessClient;
using NativeQa;
namespace hds.Tests;
public class MachinesCriticalNextFiveTests
{

    private static (MissionProgress.SavedState State,MissionNpcInteriorProof.Plan Plan) CapturedFinalRoleShape()
    {
        var state=new MissionProgress.SavedState{Version=2,Mission=201206,Phase=1,Identity=new string('a',32),Started=100,Instance=51};
        state.Mobs.Add(new(){Npc=4,Identity=new string('b',32)});
        for(int i=0;i<4;i++)state.Mobs.Add(new(){Npc=(ushort)(1100+i),Identity=(i+1).ToString("x32"),Hostile=true,Wave=0,Health=100});
        var actors=new[]{new{Kind="Npc",Id=4,Index=0},new{Kind="Wave",Id=0,Index=0},new{Kind="Wave",Id=0,Index=1},new{Kind="Wave",Id=0,Index=2},new{Kind="Wave",Id=0,Index=3}};
        var witness=System.Text.Json.JsonSerializer.SerializeToElement(new{Selection=new{Mission=201206,Actors=actors},Actors=actors.Select(a=>new{Role=$"{a.Kind}:{a.Id}:{a.Index}"}).ToArray()});
        var door=new MissionNpcInteriorProof.Door(1,417,new[]{0d,0,0},new string('0',32),null,"");
        return(state,new(state.Identity,state.Started,state.Phase,"literal-role-only-shape","",witness,door,door));
    }
    [Fact] public void CurrentFinalProgramsMapToTheirOwnCertifiedWaveRolesIncludingRetainedDeadSiblings()
    {
        var f=CapturedFinalRoleShape();Assert.Equal(0,MachinesCriticalNextFiveFixture.CertifiedActorIndex(f.State,f.State.Mobs[0],f.Plan));
        f.State.Mobs[1].Dead=true;
        for(int i=1;i<5;i++)Assert.Equal(i,MachinesCriticalNextFiveFixture.CertifiedActorIndex(f.State,f.State.Mobs[i],f.Plan));
    }
    [Theory][InlineData("owner")][InlineData("phase")][InlineData("missingSibling")][InlineData("oldStage")][InlineData("duplicateIdentity")][InlineData("wrongWave")]
    public void ReacquireNeverBorrowsForeignOrIncompleteSavedActorRole(string change)
    {
        var f=CapturedFinalRoleShape();var actor=f.State.Mobs[^1];
        if(change=="owner")f.State.Identity=new string('c',32);else if(change=="phase")f.State.Phase++;
        else if(change=="missingSibling")f.State.Mobs.RemoveAt(1);else if(change=="oldStage")actor.Npc=1053;
        else if(change=="duplicateIdentity")f.State.Mobs[1].Identity=actor.Identity;else actor.Wave=9;
        Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.CertifiedActorIndex(f.State,actor,f.Plan));
    }

    [Fact] public void CapturedCabinetApproachUsesAuditedPoseWithoutChangingRawSource()
    {
        string repo=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(repo,"data","sector_properties.csv")))repo=Path.GetDirectoryName(repo)!;
        SectorProperties.Load(Path.Combine(repo,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(repo,"data","navmesh"));
        var raw=new StaticWorldObject{metrId=1,sectorID=209,mxoStaticId=0x0d1001a2,staticId=418,type=BitConverter.GetBytes((ushort)326),pos_x=30765,pos_y=1347,pos_z=14160,rot=90,quat="0000000000000000000000000000803f"};
        var obj=new MissionProgress.SavedObject{Id=1,Goid=326,Spot=raw};double[] root={28874.278135270823,1295,14514.304661822947};
        string before=System.Text.Json.JsonSerializer.Serialize(raw,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        Assert.False(NavMesh.Project(1,raw.pos_x,raw.pos_y,raw.pos_z,out _,maxDistance:800,reachableFrom:root));
        var actual=MachinesCriticalNextFiveFixture.BoundApproach(obj,root);
        Assert.Equal((28040d,1282d,11435d),(actual.Pose.X,actual.Pose.Y,actual.Pose.Z));Assert.Equal(new[]{28080d,1295,11330},actual.Standing.Position);
        Assert.Equal((209,47,350),((int)actual.Standing.location.sector,(int)actual.Standing.location.region,(int)actual.Standing.location.polygon));
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(raw,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
        obj.Goid=552;Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.BoundApproach(obj,root));obj.Goid=326;
        Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.BoundApproach(obj,new[]{double.NaN,1295,14514d}));
    }

    [Theory][InlineData(null)][InlineData("bad")][InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void WholeCaseRequiresExactFrozenSource(string? expected)=>Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.RequirePin(expected,new string('B',64)));

    [Fact] public void ActualEscortReadinessHasNoSyntheticContentEntryRequirement()
    {
        var mission=Load().Single(m=>m.id==201204);var state=new MissionProgress.SavedState{Version=2,Mission=201204,Phase=0,Identity=new string('a',32),Started=100,Instance=47,
            Flags=new(){["entered"]=false,["failed"]=false},Done=new[]{1,2},Following=new ushort[]{1},Mobs=new(){new(){Npc=1,Identity=new string('b',32),Friendly=true,Health=100}}};
        Assert.True(ZionAfter1212EscortProof.CanApproach(state,mission,3));Assert.False(state.Flags["entered"]);
        state.Flags["failed"]=true;Assert.False(ZionAfter1212EscortProof.CanApproach(state,mission,3));
    }
    [Fact] public void MatchingFrozenSourcePinIsAccepted()=>MachinesCriticalNextFiveFixture.RequirePin(new string('B',64),new string('B',64));
    [Theory][InlineData(1)][InlineData(2)]
    public void MysqlHexRetainsEscapedNamesAndBothSavedVersions(int version)
    {
        string json=System.Text.Json.JsonSerializer.Serialize(new{Version=version,Mission=201203,Mobs=new[]{new{Name="A \"quoted\" \\ path"}}});
        var state=MachinesCriticalNextFiveFixture.ReadCheckpointHex(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(json)))!;
        Assert.Equal(version,state.Version);Assert.Equal("A \"quoted\" \\ path",state.Mobs.Single().Name);Assert.Null(MachinesCriticalNextFiveFixture.ReadCheckpointHex("NULL"));
    }
    [Theory][InlineData("00")][InlineData("FF")][InlineData("bad")][InlineData("7B2256657273696F6E223A337D")]
    public void MalformedOrUnknownCheckpointNeverBecomesAValidScope(string hex)=>Assert.ThrowsAny<Exception>(()=>MachinesCriticalNextFiveFixture.ReadCheckpointHex(hex));
    [Fact] public void CurrentHostilesIncludeNamedXmlActorsWithoutTreatingOldStageOrFriendlyActorsAsTargets()
    {
        var authored=new HashSet<ushort>{1};
        var phage=new MissionProgress.SavedMob{Npc=1,Friendly=false,Hostile=false,Dead=false,Phase=0};
        Assert.True(MachinesCriticalNextFiveFixture.CurrentHostile(phage,authored));
        Assert.False(MachinesCriticalNextFiveFixture.CurrentHostile(phage,new HashSet<ushort>{2}));
        phage.Friendly=true;Assert.False(MachinesCriticalNextFiveFixture.CurrentHostile(phage,authored));
        phage.Friendly=false;phage.Dead=true;Assert.False(MachinesCriticalNextFiveFixture.CurrentHostile(phage,authored));
        Assert.True(MachinesCriticalNextFiveFixture.CurrentHostile(new MissionProgress.SavedMob{Npc=1050,Hostile=true},authored));
    }
    [Fact] public void LatestGenerationCannotReselectHistoricalSameViewId()
    {
        var attrs=new Dictionary<string,byte[]>();
        var old=new ViewCreation(7,599,1,attrs);var current=new ViewCreation(7,599,2,attrs);
        Assert.Same(current,Assert.Single(MachinesCriticalNextFiveFixture.LatestViews(new[]{old,current},0)));
        Assert.Same(current,Assert.Single(MachinesCriticalNextFiveFixture.LatestViews(new[]{old,current},1)));
    }
    [Fact] public void ActualNpcLevelIsOneByteAndMalformedSchemaIsRejected()
    {
        Assert.Equal((byte)50,MachinesCriticalNextFiveFixture.NpcLevel(new Dictionary<string,byte[]>{{"Level",new byte[]{0x32}}}));
        Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.NpcLevel(new Dictionary<string,byte[]>()));
        Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.NpcLevel(new Dictionary<string,byte[]>{{"Level",new byte[]{0x32,0}}}));
    }
    [Fact] public void LootProofUsesClaimedIdentityNotAnInventorySlotOrOnlyCount()
    {
        var window=new byte[26];window[0]=0x81;window[1]=0x19;window[16]=1;
        Convert.FromHexString("000000088C1A0000").CopyTo(window,18);
        var claim=new byte[22];claim[0]=0x81;claim[1]=0x20;claim[12]=1;
        window.AsSpan(18,8).CopyTo(claim.AsSpan(14));
        Assert.Equal(MachinesCriticalNextFiveFixture.LootIdentities(window,false),MachinesCriticalNextFiveFixture.LootIdentities(claim,true));
        claim[14]=1;Assert.NotEqual(MachinesCriticalNextFiveFixture.LootIdentities(window,false)[0],MachinesCriticalNextFiveFixture.LootIdentities(claim,true)[0]);
        Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.LootIdentities(claim[..^1],true));
    }
    private static string Root {get {var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static List<Mission> Load() => Directory.GetFiles(Path.Combine(Root,"data","missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
    [Fact] public void ActualAgentGrayOrderDeterminesTheFiveAndFollowingCritical()
    {
        var all=Load(); MissionContacts.Load(Path.Combine(Root,"data","mission_contacts.csv"));
        var contact=MissionContacts.ForOrg(2,0); Assert.Equal(2001u,contact.id);
        var catalogue=all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList();
        var completed=MachinesCriticalNextFiveFixture.Prerequisites.ToHashSet();
        Assert.Equal(21,completed.Count); Assert.Empty(completed.Intersect(MachinesCriticalNextFiveFixture.Missions.Append(MachinesCriticalNextFiveFixture.NextMission)));
        var intro=new[]{1201,1204,1203,1205,1206,1207,1208};
        Assert.Equal(completed.Order(),catalogue.Where(m=>m.plot&&m.order<1202).Select(m=>m.id).Concat(intro).Order());
        foreach(int id in MachinesCriticalNextFiveFixture.Missions.Append(MachinesCriticalNextFiveFixture.NextMission))
        {
            var offers=DataLoader.ContactOfferWithStandards(catalogue,completed);
            var mission=Assert.Single(offers.Where(m=>m.plot)); Assert.Equal(id,mission.id);
            Assert.DoesNotContain(offers,m=>m.id==20001); completed.Add(id);
        }
    }
    [Fact] public void SourceMissionGoalStructureAndIndependentLevel50RewardAmountsStayExplicit()
    {
        var all=Load();var selected=MachinesCriticalNextFiveFixture.Missions.Select(id=>all.Single(m=>m.id==id)).ToArray();
        Assert.Equal(new[]{"Poster Hunt","Hard Time","No Allies Here","Under Warranty","Gathering Evidence"},selected.Select(m=>m.title));
        Assert.Equal(new[]{3,4,2,2,2},selected.Select(m=>m.phases.Count));
        foreach(var m in selected)
        {
            var expected=MachinesCriticalNextFiveFixture.ExpectedReward(m.id);
            if(m.id==201202){Assert.Equal(17,m.rewardLevel);Assert.Equal(132000,m.exp);Assert.Equal(59500,m.info);
                Assert.Equal(404250u,expected.Xp);Assert.Equal(175000u,expected.Info);}
            else{Assert.Equal(0,m.rewardLevel);Assert.Equal(expected.Xp,(uint)m.exp);Assert.Equal(expected.Info,(uint)m.info);}
        }
        Assert.Equal(8,selected[0].phases.Sum(p=>p.objects.Count));
        var warranty=selected[3];Assert.Single(warranty.phases[1].waves);Assert.Equal(3,warranty.phases[1].waves[0].count);
    }
    [Fact] public void NativeModeHasOnlyEarlierReceiptsAndLayoutControl()
    {
        var mode=NativeQaModes.Find("machines-critical-next-five"); Assert.Equal("machines-critical-next-five",mode.Name);
        var p=mode.Plan; Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls); Assert.True(p.DevCommands);
        Assert.Equal(new ushort[]{2,17,1006,77},p.Abilities);
        Assert.Equal("201202",Assert.Single(p.EnvironmentOverrides).Value);
        Assert.Equal(MachinesCriticalNextFiveFixture.SeedPrerequisites(110),p.AfterHelpers[0]);
        Assert.DoesNotContain("char_active_missions",string.Join("\n",p.AfterHelpers));
        Assert.DoesNotContain("inventory",string.Join("\n",p.AfterHelpers));
        Assert.Single(Scenarios.All,s=>s.Name=="machines-critical-next-five");
    }
}
