using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using hds;
namespace HeadlessClient;
// Explicit disposable capture fixture, not earned progress and never a runtime restore migration.
public static class NativeBoundFurnitureFixture
{
 public const string Name="native-bound-furniture",SourcePin="MXO_BOUND_FURNITURE_SOURCE_SHA256";
 public const string CapturedSha256="DDB8A909C2F1313A637788C385F714C2D8E820D2A1098350BE2137EDDF86402C";
 public const string ManifestSha256="ECAE0AEE396F5754B30818EC73E9A652A42BE42B6584CB5F46BA5C63524AA13E";
 public const uint SourceKey=1032848568;
 public static readonly ushort[] Abilities={2,17,1006,77};
 public static readonly int[] EarlierReceipts=new[]{1201,1204,1203,1205,1206,1207,1208}.Concat(Enumerable.Range(2101,10)).Concat(Enumerable.Range(301101,3)).Concat(Enumerable.Range(301201,9)).ToArray();
 static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
 const BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 public sealed record Prepared(byte[] Payload,MissionProgress.SavedState State,BoundFurniturePublicProof.Plan Public);
 static void Need(bool ok,string why){if(!ok)throw new InvalidDataException("Bound furniture fixture: "+why);}
 public static byte[] Capture
 {
  get{var a=typeof(NativeBoundFurnitureFixture).Assembly;using var s=a.GetManifestResourceStream(a.GetManifestResourceNames().Single(n=>n.EndsWith("native-bound-furniture-1450.capture.json",StringComparison.Ordinal)))!;using var m=new MemoryStream();s.CopyTo(m);return m.ToArray();}
 }
 public static MissionProgress.SavedState VerifyCapture(byte[] capture)
 {
  Need(Convert.ToHexString(SHA256.HashData(capture))==CapturedSha256,"Captured bytes differ");var s=JsonSerializer.Deserialize<MissionProgress.SavedState>(capture,Fields)!;
  var o=s.Objects.Single();Need(s.Version==2&&s.Mission==301210&&s.Phase==1&&s.Identity=="bc7cfe770db746209313034b307ca1cd"&&s.Started==1794824876&&s.Instance==49&&
   s.Done.SequenceEqual(new[]{1})&&!s.Done.Contains(3)&&s.Items.Count==1&&s.Items.GetValueOrDefault((ushort)6854)==1&&o.Id==1&&o.Goid==1450&&o.OpenFloor==null&&o.Spot.mxoStaticId==SourceKey&&
   new[]{o.Spot.pos_x,o.Spot.pos_y,o.Spot.pos_z}.SequenceEqual(new[]{42300d,1145,-27045})&&o.Spot.quat=="0000000000000000000000000000803f","Exact captured owner/source/goals/items");
  Need(EarlierReceipts.Length==29&&EarlierReceipts.Distinct().Count()==29&&!EarlierReceipts.Contains(301210),"Earlier receipt boundary");return s;
 }
 static IEnumerable<StaticWorldObject> Rows(string repo)
 {
  foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
  {
   using var r=File.OpenText(Path.Combine(repo,"data",file));var header=r.ReadLine()!.Split(',');var columns=header.Select((n,i)=>(n,i)).ToDictionary(x=>x.n,x=>x.i);string? line;
   while((line=r.ReadLine())!=null)
   {
    var v=line.Split(',');if(v.Length!=header.Length||v[columns["sector_id"]]!="985"||v[columns["metr_id"]]!="1")continue;
    double Number(string n)=>double.Parse(v[columns[n]],CultureInfo.InvariantCulture);
    uint id=BitConverter.ToUInt32(Convert.FromHexString(v[columns["staticId"]]));
    yield return new StaticWorldObject{metrId=1,sectorID=985,staticId=id,mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(v[columns["mxoId"]])),exterior=bool.Parse(v[columns["exterior"]]),
     pos_x=Number("pos_x"),pos_y=Number("pos_y"),pos_z=Number("pos_z"),rot=Number("rotation"),quat=v[columns["quat"]],type=Convert.FromHexString(v[columns["type"]][..4])};
   }
  }
 }
 public static Prepared Prepare(string repo)
 {
  var captured=VerifyCapture(Capture);var assembly=typeof(MissionProgress).Assembly;
  var selectionType=assembly.GetType("hds.MissionStreetSelection",true)!;var routesType=assembly.GetType("hds.MissionStreetFurnitureRoutes",true)!;
  var json=JsonNode.Parse(Capture)!.AsObject();var old=json["StreetAccess"]!["Witnesses"]!.AsArray().Single(w=>(int)w!["Phase"]! ==1)!;
  Need((int)old["Version"]! ==1&&old["FurnitureRoutes"]==null&&old["BoundRoutes"]==null,"Only explicit old capture input, not a runtime upgrade");
  var all=Rows(repo).ToArray();var expected=captured.OpenFloorSites![captured.OpenFloorCurrentSiteIndex!.Value];
  var fingerprint=typeof(MissionOpenFloorSite).GetMethod("CatalogFingerprint",Flags)!;
  var sites=MissionSites.Build(all,1);var site=sites.Single(s=>s.elevatorDoor.mxoStaticId==expected.DoorKey&&(string)fingerprint.Invoke(null,new object[]{s})! ==expected.Fingerprint);
  var xml=new XmlDocument();xml.Load(Path.Combine(repo,"data/missions/merovingian_1_2_10_into_the_hive.xml"));var mission=MissionXml.Parse(xml.DocumentElement!);
  var progress=new MissionProgress(mission){persistenceId=captured.Identity,startedAt=captured.Started,phaseIndex=1,site=site};progress.sites.Add(site);
  foreach(var o in captured.Objects)progress.objects.Add((ushort)o.Id,new MissionObjectState{def=mission.phases[1].objects.Single(d=>d.id==o.Id),spot=o.Spot,goid=o.Goid});
  var selection=JsonSerializer.Deserialize(old.ToJsonString(),selectionType,Fields)!;var choices=captured.Objects.Select(o=>new MissionOpenFloorChoice((ushort)o.Id,o.Goid,JsonSerializer.Serialize(o.Spot,Fields))).ToArray();
  var first=new Dictionary<uint,StaticWorldObject>();foreach(var raw in all)first.TryAdd(raw.mxoStaticId,raw);
  StaticWorldObject Lookup(uint key)=>first.GetValueOrDefault(key)!;
  SectorProperties.Load(Path.Combine(repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(repo,"data/navmesh"));
  object prepared;try{prepared=routesType.GetMethod("BuildOnSite",Flags)!.Invoke(null,new object[]{progress,selection,site,choices,(Func<uint,StaticWorldObject>)Lookup})!;
   routesType.GetMethod("Validate",Flags)!.Invoke(null,new object[]{progress,prepared,(Func<uint,StaticWorldObject>)Lookup,choices});
   routesType.GetMethod("RequireCurrentSources",Flags)!.Invoke(null,new object[]{progress,prepared});}
  catch(TargetInvocationException e){throw new InvalidDataException("Actual fresh furniture builder refused captured fixture",e.InnerException??e);}
  var replacement=JsonSerializer.SerializeToNode(prepared,selectionType,Fields)!;Need((int)replacement["Version"]! ==3&&replacement["BoundRoutes"]==null,"New separately tagged authority");
  var witnesses=json["StreetAccess"]!["Witnesses"]!.AsArray();int at=witnesses.IndexOf(old);witnesses[at]=replacement;
  byte[] payload=Encoding.UTF8.GetBytes(json.ToJsonString());var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(payload,Fields)!;
  Need(state.Identity==captured.Identity&&state.Started==captured.Started&&state.Mission==captured.Mission&&state.Phase==captured.Phase&&
   JsonSerializer.Serialize(state.Objects,Fields)==JsonSerializer.Serialize(captured.Objects,Fields)&&state.Done.SequenceEqual(captured.Done)&&state.Items.OrderBy(x=>x.Key).SequenceEqual(captured.Items.OrderBy(x=>x.Key)),"Fixture may only add freshly built authority");
  return new(payload,state,BoundFurniturePublicProof.Read(repo,state));
 }
 public static string FindRepository()
 {string? p=AppContext.BaseDirectory;while(p!=null&&!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p);return p??throw new InvalidDataException("Repository not found for captured fixture");}
 static readonly Lazy<Prepared> DefaultPrepared=new(()=>Prepare(FindRepository()));
 public static Prepared Default=>DefaultPrepared.Value;
 public static void RequireOwned(string schema)=>Need(schema.StartsWith(ThrowawayDatabase.Prefix,StringComparison.Ordinal)&&schema.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'),"Owned disposable schema required");
 public static void RequireSource(string? expected,string actual)=>Need(expected?.Length==64&&expected.All(Uri.IsHexDigit)&&expected.Equals(actual,StringComparison.OrdinalIgnoreCase),"Exact source pin required");
 public static string SeedSql(uint character,Prepared p)
 {
  Need(character==110||character==1001,"Disposable fixture identity only");string n=character.ToString(CultureInfo.InvariantCulture);var start=p.Public.Outside[0];
  return "CREATE TABLE IF NOT EXISTS char_active_missions(charId INT UNSIGNED NOT NULL PRIMARY KEY,payload LONGTEXT NULL,receipt LONGTEXT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"+
   $"DELETE FROM char_active_missions WHERE charId={n};INSERT INTO char_active_missions(charId,payload,receipt) VALUES({n},CONVERT(0x{Convert.ToHexString(p.Payload)} USING utf8mb4),NULL);"+
   $"DELETE FROM char_missions WHERE charId={n};INSERT INTO char_missions(charId,missionId) VALUES"+string.Join(",",EarlierReceipts.Select(id=>$"({n},{id})"))+";"+
   $"DELETE FROM inventory WHERE charId={n} AND (slot=1 OR goid=6854);INSERT INTO inventory(charId,goid,slot,count,purity) VALUES({n},6854,1,0,0);"+
   $"UPDATE characters SET x={start[0].ToString(CultureInfo.InvariantCulture)},y={start[1].ToString(CultureInfo.InvariantCulture)},z={start[2].ToString(CultureInfo.InvariantCulture)},level=50,exp={PlayerProgression.ExperienceForLevel(50)},alignment=3,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,district='slums',districtId=1,pvpflag=0 WHERE charId={n};";
 }
}
