using HeadlessClient;
using hds;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
namespace hds.Tests;
[Collection("DelayedActions")]
public class EngageFoesNativeFixtureTests
{
    private readonly ITestOutputHelper output;
    public EngageFoesNativeFixtureTests(ITestOutputHelper output){this.output=output;}
    private static string Root()
    {string dir=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(dir,"data","mob_just_some_and_winter.csv")))dir=Path.GetDirectoryName(dir)!;return dir;}
    [Fact]public void PrivateCsvChangesOnlyTwoHealthCellsAndOnePrimaryCoordinate()
    {
        byte[] original=File.ReadAllBytes(Path.Combine(Root(),"data","mob_just_some_and_winter.csv"));var modified=EngageFoesFixture.InitialNpcCsv(original);
        var before=Encoding.Latin1.GetString(original).Split('\n');var after=Encoding.Latin1.GetString(modified).Split('\n');Assert.Equal(before.Length,after.Length);var changed=new List<string>();
        for(int row=0;row<before.Length;row++)
        {var a=before[row].Split(';');var b=after[row].Split(';');Assert.Equal(a.Length,b.Length);for(int col=0;col<a.Length;col++)if(a[col]!=b[col])changed.Add(a[2]+":"+col+":"+a[col]+"->"+b[col]);}
        Assert.Equal(new[]{"WinterFrost:4:800->10000","WinterFrost:5:800->10000","Trainman:9:-1498->-1198"},changed);
        Assert.Throws<InvalidDataException>(()=>EngageFoesFixture.InitialNpcCsv(modified));
    }
    [Theory][InlineData("HEADLESS_ENGAGE_FOES_NPCS","2")][InlineData("MXO_DEV_COMMANDS","0")][InlineData("MXO_MOBS","world")][InlineData("database","reality_hd")][InlineData("database","mxotest_bad-name")]
    public void InitialDataWriteRequiresStrictDisposableOptIn(string key,string value)
    {
        var env=EngageFoesFixture.Environment;string db="mxotest_fixture_unit";if(key=="database")db=value;else env[key]=value;
        string nonexistent=Path.Combine(Path.GetTempPath(),"engage-seed-gate-"+Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidOperationException>(()=>EngageFoesFixture.PrepareInitialNpcs(nonexistent,nonexistent,db,env));Assert.False(Directory.Exists(nonexistent));
        Assert.Null(EngageFoesFixture.PrepareInitialNpcs(nonexistent,nonexistent,"reality_hd",null));
    }
    [Fact]public void PrestartCopyPreservesSourceAndNeverOverwritesAFile()
    {
        string dir=Path.Combine(Path.GetTempPath(),"engage-seed-unit-"+Guid.NewGuid().ToString("N"));string source=Path.Combine(dir,"source"),work=Path.Combine(dir,"owned");Directory.CreateDirectory(Path.Combine(source,"data"));
        try
        {
            byte[] original=File.ReadAllBytes(Path.Combine(Root(),"data","mob_just_some_and_winter.csv"));string sourceCsv=Path.Combine(source,"data","mob_just_some_and_winter.csv");File.WriteAllBytes(sourceCsv,original);
            string proof=EngageFoesFixture.PrepareInitialNpcs(source,work,"mxotest_fixture_unit",EngageFoesFixture.Environment)!;
            Assert.Equal(original,File.ReadAllBytes(sourceCsv));Assert.Equal(EngageFoesFixture.InitialNpcCsv(original),File.ReadAllBytes(Path.Combine(work,"data","mob_just_some_and_winter.csv")));
            using var manifest=JsonDocument.Parse(File.ReadAllText(proof));Assert.False(manifest.RootElement.GetProperty("RuntimeMutation").GetBoolean());Assert.Equal(3,manifest.RootElement.GetProperty("Changes").GetArrayLength());
            Assert.Throws<InvalidOperationException>(()=>EngageFoesFixture.PrepareInitialNpcs(source,work,"mxotest_fixture_unit",EngageFoesFixture.Environment));
        }
        finally{Directory.Delete(dir,true);}
    }
    private static ViewCreation Npc(ushort view,double x,double y,double z,bool enemy=true,ushort health=500)=>new(view,599,1,new Dictionary<string,byte[]>{
        ["Position"]=BitConverter.GetBytes(x).Concat(BitConverter.GetBytes(y)).Concat(BitConverter.GetBytes(z)).ToArray(),["Health"]=BitConverter.GetBytes(health),["IsEnemy"]=new[]{(byte)(enemy?1:0)}});
    [Fact]public void RecipientsComeFromLatestActualPosesAndIndependentSixMetreGeometry()
    {
        var primary=Npc(6,24499,495,-1198);var views=new[]{primary,Npc(5,24499,495,-1498),Npc(7,24499,495,-1798),Npc(8,24499,495,-1799),Npc(9,24499,0,-1198),Npc(10,24499,495,-1198,false),Npc(11,24499,495,-1198,true,0),Npc(5,30000,495,-1498)};
        Assert.Equal(new ushort[]{6,7},EngageFoesFixture.ExpectedRecipients(views,primary));
    }
    [Fact]public void ThreeMetreSidewaysPrimaryIsWalkableOnSameOutdoorFloor()
    {
        string dir=Root();SectorProperties.Load(Path.Combine(dir,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(dir,"data","navmesh"));
        Assert.True(NavMesh.Locate(1,24499,495,-1498,out var secondary));Assert.True(NavMesh.Locate(1,24499,495,-1198,out var primary));
        Assert.Equal(secondary.floorY,primary.floorY,3);Assert.Equal(secondary.indoors,primary.indoors);
        output.WriteLine($"NAV actual secondary {secondary.sector}/{secondary.region}/{secondary.polygon} floor={secondary.floorY}; primary {primary.sector}/{primary.region}/{primary.polygon} floor={primary.floorY}; indoor={primary.indoors}");
        Assert.Equal("1",NativeQa.NativeQaFixturePlan.EnvironmentFor("engage-foes-taunt")[EngageFoesFixture.InitialNpcsEnvironment]);
        Assert.NotEqual("1",NativeQa.NativeQaFixturePlan.EnvironmentFor("stealth-shields")[EngageFoesFixture.InitialNpcsEnvironment]);
    }
}
