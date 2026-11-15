using System.Text.Json;
using hds;
using D=hds.MissionNpcDomainCertificates;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    private static D.Document DomainDocument(MissionSite site,MissionNpcInteriorWitness witness,int capacity=1)
    {
        string key=new string('B',64);
        return new D.Document{Format=1,Policy=D.Policy,RolePlan=D.RolePlan,World=1,ProfilesManifest=new string('A',64),Catalogue=D.Catalogue(new[]{site}),Inputs=D.Inputs(Array.Empty<StaticWorldObject>()),
            Sites=new[]{new D.Row{Site=MissionOpenFloorSite.CatalogFingerprint(site),Domain=key}},Domains=new[]{new D.Domain{Key=key,Capacity=capacity,FloorDigest=witness.Floor.EvidenceDigest,LobbyDigest=witness.Lobby.EvidenceDigest,Floor=witness.Floor,Lobby=witness.Lobby}}};
    }
    private static D DomainRead(D.Document doc,params MissionSite[] sites)=>D.Read(JsonSerializer.SerializeToUtf8Bytes(doc),sites,D.Inputs(Array.Empty<StaticWorldObject>()),new string('A',64));
    [Theory] [InlineData("policy")] [InlineData("roleplan")] [InlineData("world")] [InlineData("profiles")] [InlineData("catalogue")] [InlineData("inputs")] [InlineData("missing")]
    public void DomainMetadataCannotAuthorizeAStaleCatalogueOrPolicy(string change)
    {
        var p=Prepared(true);var d=DomainDocument(p.Site,p.Pending.Witness!);
        switch(change){case "policy":d.Policy++;break;case "roleplan":d.RolePlan++;break;case "world":d.World=2;break;case "profiles":d.ProfilesManifest=new string('C',64);break;case "catalogue":p.Site.style="changed";break;case "inputs":d.Inputs=new string('C',64);break;case "missing":d.Sites=Array.Empty<D.Row>();break;}
        Assert.Throws<InvalidDataException>(()=>DomainRead(d,p.Site));
    }
    [Fact] public void DomainPayloadCorruptionRefusesBeforeParsingOrSelection()
    {
        var p=Prepared(true);byte[] invalid={31,139,8,0};
        Assert.Equal("NpcDomainPayloadHash",Assert.Throws<InvalidDataException>(()=>D.Load(invalid,new string('A',64),new[]{p.Site},D.Inputs(Array.Empty<StaticWorldObject>()),new string('A',64))).Message);
    }
    [Theory] [InlineData("standing")] [InlineData("node")] [InlineData("position")] [InlineData("yaw")] [InlineData("catalogue")]
    public void SelectedAdmissionRequiresExactlyTheCertifiedContextAndPreservesOwnerOnMismatch(string change)
    {
        var p=Prepared(true);p.Progress.itemSlots[6783]=6;var c=Client(p.Progress);var d=DomainDocument(p.Site,p.Pending.Witness!);var original=d.Domains[0].Floor;
        var standing=original.Standing;var position=original.Position;var node=original.Node;string q=original.Quaternion;
        if(change=="standing")standing[1]++;if(change=="position")position[0]++;if(change=="node")node=new MissionNpcInteriorNode((ushort)(node.Region+1),node.Polygon);if(change=="yaw")q=(q[0]=='0'?"1":"0")+q.Substring(1);
        d.Domains[0].Floor=new MissionNpcInteriorEntry(original.RawDoorJson,original.EvidenceDigest,q,node,position,standing);
        var domains=DomainRead(d,p.Site);Assert.True(domains.TryGet(p.Site,1,out var binding,out _));if(change=="catalogue")p.Site.npcSpots.Add(new[]{1d,2,3,4});
        string before=JsonSerializer.Serialize(p.Progress.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{p.Site}),MissionProgress.SaveJson);
        Assert.False(MissionNpcInteriorPlanning.Prepare(c,p.Progress,2,p.Site,p.Provider,out _,out _,out var why,binding));Assert.Equal("ChangedCertifiedNpcContext",why);
        Assert.Same(p.Progress,c.playerData.missionProgress);Assert.Equal((ushort)6,p.Progress.itemSlots[6783]);
        Assert.Equal(before,JsonSerializer.Serialize(p.Progress.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{p.Site}),MissionProgress.SaveJson));
    }
    [Fact] public void SelectedAdmissionAcceptsTheExactCertifiedEntriesWithoutChangingOwner()
    {
        var p=Prepared(true);var domains=DomainRead(DomainDocument(p.Site,p.Pending.Witness!),p.Site);Assert.True(domains.TryGet(p.Site,1,out var binding,out _));
        var client=Client(p.Progress);Assert.True(MissionNpcInteriorPlanning.Prepare(client,p.Progress,2,p.Site,p.Provider,out _,out var prepared,out var why,binding),why);
        Assert.Equal(JsonSerializer.Serialize(p.Pending.Witness),JsonSerializer.Serialize(prepared.Witness));Assert.Null(p.Progress.npcInteriorPending);Assert.Null(p.Progress.site);
    }
    private sealed class DomainRandom:Random{internal int Calls;public override int Next(int max){Calls++;return base.Next(max);}internal DomainRandom(int seed):base(seed){}}
    [Fact] public void DomainFilterConsumesNoRandomAndExistingChooserKeepsItsOneDraw()
    {
        var p=Prepared(true);var first=p.Site;var second=Site();second.minX+=10;second.maxX+=10;
        var d=DomainDocument(first,p.Pending.Witness!,1);var domains=DomainRead(d,first);var actual=new DomainRandom(47);var expected=new DomainRandom(47);
        bool Allowed(MissionSite s)=>domains.TryGet(s,1,out _,out _);
        for(int i=0;i<10;i++){Assert.True(Allowed(first));Assert.False(Allowed(second));}Assert.Equal(0,actual.Calls);
        Assert.Same(MissionSites.Choose(new[]{first},"Office",0,0,1,0,expected),MissionSites.Choose(new[]{first,second},"Office",0,0,1,0,actual,admissible:Allowed));
        Assert.Equal(1,actual.Calls);Assert.Equal(expected.Next(1000),actual.Next(1000));
        Assert.False(domains.TryGet(first,2,out _,out var why));Assert.Equal("InsufficientCertifiedNpcCapacity",why);
    }
    [Fact] public void WorldTwoAndAuthoredRelativeArrivalsStayOutsideFreshCoveragePolicy()
    {
        var mission=Definition();Assert.True(MissionNpcCoverage.Required(mission,2,out int count));Assert.Equal(1,count);
        mission.district=2;Assert.False(MissionNpcCoverage.Required(mission,2,out _));mission.district=1;mission.npcs[0].arrival.at="player";Assert.False(MissionNpcCoverage.Required(mission,2,out _));
    }    [Fact] public void ActiveFormat2AndOldSavedDigestRemainSeparateAuthorities()
    {
        string active=Path.Combine(Repo,"data","mission_interior_evidence_v2","1"),legacy=Path.Combine(Repo,"data","mission_interior_evidence","1");
        var current=MissionInteriorEvidence.LoadActive(active,1,550);Assert.NotEqual(Primary.Value.Digest,current.Digest);
        Assert.Equal(Primary.Value.Digest,MissionInteriorEvidence.LoadSaved(active,legacy,1,550,Primary.Value.Digest).Digest);
        Assert.Equal(current.Digest,MissionInteriorEvidence.LoadSaved(active,legacy,1,550,current.Digest).Digest);
    }
    [Fact] public void CommittedCompactCatalogueCoversActualSourceInputsAndHasExplicitNegativeSites()
    {
        string data=Path.Combine(Repo,"data"),active=Path.Combine(data,"mission_interior_evidence_v2","1");
        var world=D.ReadOfflineWorld(data);var sites=MissionSites.Build(world,1);
        var domains=D.Load(File.ReadAllBytes(Path.Combine(active,"npc-domains.json.gz")),MissionNpcCoverage.DomainSha256,sites,D.Inputs(world),MissionInteriorEvidence.ActiveManifestSha256);
        int positive=sites.Count(s=>domains.TryGet(s,1,out _,out _));Assert.Equal(sites.Count,domains.Count);Assert.InRange(positive,3000,sites.Count-1);
        Assert.All(sites.Where(s=>!domains.TryGet(s,1,out _,out _)).Take(20),s=>{Assert.False(domains.TryGet(s,1,out _,out var refusal));Assert.False(string.IsNullOrWhiteSpace(refusal));});
    }
    [Fact] public void GeneralCoverageModeIsDiscoveredAndRetainsNormalOriginReceiptsAndNoSelectedProgress()
    {
        var mode=NativeQa.NativeQaModes.RequireKnown("mission-npc-scene-coverage");var plan=mode.Plan;
        Assert.Equal((17000,2300),(plan.X,plan.Z));Assert.Equal("101213",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
        Assert.Equal(370,plan.ExtraQaMetadata["AuditedScenes"].GetInt32());Assert.Equal(2,plan.ExtraQaMetadata["SceneFormat"].GetInt32());
        Assert.False(plan.ExtraQaMetadata["SelectedProgressSeeded"].GetBoolean());Assert.DoesNotContain("(110,101213)",Assert.Single(plan.BeforeLoadout));
        Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.Contains("ordinary",Assert.Single(plan.ReadyMessages));
    }

}
