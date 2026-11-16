using hds;
namespace hds.Tests;

// Pure assignment review: no enrollment, profile load, NAV startup, or global/static mutation.
public sealed class NativeBoundFurnitureSourcePoolReviewTests
{
 static StaticWorldObject Row(ushort goid,uint id)=>new(){metrId=1,sectorID=663,mxoStaticId=(663u<<20)|id,staticId=id,type=BitConverter.GetBytes(goid),pos_x=28000+id,pos_y=947,pos_z=-20155,rot=270,quat="000000000000803f000000002ebd3bb3"};
 static MissionPhase Phase(params string[] kinds)
 {var phase=new MissionPhase{site="Normal"};ushort id=0;foreach(string kind in kinds)phase.objects.Add(new(){id=++id,kind=kind});return phase;}
 static MissionSite Site(params StaticWorldObject[] rows)
 {var site=new MissionSite{metrId=1};site.containers.AddRange(rows);return site;}
 static MissionStaging.ObjectChoice[] Choose(MissionPhase phase,MissionSite site,Func<StaticWorldObject,bool> compatible,int seed)
 {
  Assert.True(MissionBoundFurnitureAdmission.BuildPools(phase,site,compatible,out var pools,out string why),why);Assert.NotNull(pools);
  var random=new Random(seed);var result=MissionStaging.SelectObjectsFromPools(phase,site,random,pools).ToArray();
  var original=new Random(seed);for(int i=0;i<site.containers.Count+site.objectSpots.Count;i++)original.Next();Assert.Equal(original.Next(),random.Next());
  Assert.Equal(phase.objects.Count,result.Length);Assert.Equal(result.Length,result.Select(c=>c.Spot).Distinct().Count());return result;
 }
 [Theory][InlineData(0)][InlineData(1)][InlineData(7)][InlineData(37)][InlineData(201209)]
 public void Incompatible1451NeverConsumesAssignmentDespiteItsOriginalRandomDraw(int seed)
 {
  var bad=Row(1451,945);var good=Row(1450,946);var cabinet=Row(326,947);var phase=Phase("Furniture","Furniture");var site=Site(bad,good,cabinet);
  var before=site.containers.ToArray();var result=Choose(phase,site,r=>!ReferenceEquals(r,bad),seed);
  Assert.DoesNotContain(result,c=>ReferenceEquals(c.Spot,bad));Assert.Contains(result,c=>ReferenceEquals(c.Spot,good)&&c.Goid==1450);Assert.Contains(result,c=>ReferenceEquals(c.Spot,cabinet)&&c.Goid==326);
  Assert.Equal(before,site.containers);Assert.All(result,c=>Assert.Same(phase.objects.Single(d=>d.id==c.Definition.id),c.Definition));
 }
 [Theory][InlineData(0)][InlineData(7)][InlineData(37)]
 public void GenuinePhonePrioritySurvivesEvenWhenCompatibleCallbackRejectsEveryNativeRow(int seed)
 {
  var phone=Row(1401,1);var blocked=Row(1451,2);var phase=Phase("Phone");var site=Site(blocked,phone);int phoneChecks=0;
  var result=Choose(phase,site,r=>{if(ReferenceEquals(r,phone))phoneChecks++;return false;},seed);
  Assert.Same(phone,Assert.Single(result).Spot);Assert.Equal((ushort)1401,result[0].Goid);Assert.Equal(0,phoneChecks);
 }
 [Theory][InlineData(0)][InlineData(1)][InlineData(7)][InlineData(37)]
 public void PhoneFallbackUsesOnlyCompatibleDistinctNativeRows(int seed)
 {
  var blocked=Row(1451,1);var dresser=Row(1450,2);var cabinet=Row(326,3);var phase=Phase("Phone","Furniture");
  var result=Choose(phase,Site(blocked,dresser,cabinet),r=>!ReferenceEquals(r,blocked),seed);
  Assert.DoesNotContain(result,c=>ReferenceEquals(c.Spot,blocked));Assert.All(result,c=>Assert.Equal(MissionSites.Goid(c.Spot),c.Goid));
  Assert.Equal(new ushort[]{326,1450},result.Select(c=>c.Goid).OrderBy(v=>v));Assert.Equal("Phone",result[0].Definition.kind);Assert.Equal("Furniture",result[1].Definition.kind);
 }
 [Fact]public void PhoneFallbackCannotTurnAnIncompatibleOnlyRowIntoCapacity()
 {
  var bad=Row(1451,1);Assert.False(MissionBoundFurnitureAdmission.BuildPools(Phase("Phone"),Site(bad),_=>false,out var pools,out string why));
  Assert.Null(pools);Assert.Equal("InsufficientCompatibleNativeFurnitureSources",why);
 }
 [Theory][InlineData(0)][InlineData(7)][InlineData(37)]
 public void FurnitureThenPhoneKeepsNativePreferenceAndUniquePhoneReservation(int seed)
 {
  var phone=Row(1401,1);var bad=Row(1451,2);var good=Row(1450,3);var phase=Phase("Furniture","Phone");
  var result=Choose(phase,Site(phone,bad,good),r=>!ReferenceEquals(r,bad),seed);
  Assert.Same(good,result[0].Spot);Assert.Equal((ushort)1450,result[0].Goid);Assert.Same(phone,result[1].Spot);Assert.Equal((ushort)1401,result[1].Goid);
 }
 [Fact]public void InsufficientCompatibleCapacityRefusesAndAddingOneSourceRepairsOnlyThePureDomain()
 {
  var bad=Row(1451,1);var good=Row(1450,2);var phase=Phase("Furniture","Furniture");var site=Site(bad,good);
  Assert.False(MissionBoundFurnitureAdmission.BuildPools(phase,site,r=>!ReferenceEquals(r,bad),out var absent,out string why));Assert.Null(absent);Assert.Equal("InsufficientCompatibleNativeFurnitureSources",why);
  site.containers.Add(Row(326,3));var result=Choose(phase,site,r=>!ReferenceEquals(r,bad),7);Assert.DoesNotContain(result,c=>ReferenceEquals(c.Spot,bad));
 }
 [Fact]public void DuplicateSourceIdentityCannotFabricateDistinctCompatibleCapacity()
 {
  var one=Row(1450,1);var duplicate=Row(1450,1);Assert.False(MissionBoundFurnitureAdmission.BuildPools(Phase("Furniture","Furniture"),Site(one,duplicate),_=>true,out var pools,out string why));
  Assert.Null(pools);Assert.Equal("AmbiguousFurnitureSources",why);
 }
 [Fact]public void MixedComputerDefinitionDoesNotEnterNewFurnitureAdmission()
 {
  var computer=Row(3,1);var good=Row(1450,2);var site=Site(good);site.realObjects.Add(computer);var phase=Phase("Computer","Furniture");
  Assert.False(MissionBoundFurnitureAdmission.Applies(phase,site));var choices=MissionStaging.SelectObjects(phase,site,new Random(7)).ToArray();
  Assert.Same(computer,choices[0].Spot);Assert.Equal((ushort)3,choices[0].Goid);Assert.Same(good,choices[1].Spot);Assert.Equal((ushort)1450,choices[1].Goid);
 }
}
