using System.Xml.Linq;
using hds;
namespace HeadlessClient;
public static class MachinesAfter201211Fixture
{
 public static readonly int[] Missions={201212,201213,201214,201215,201216};
 public static readonly int[] EarlierStoryReceipts=MachinesCriticalNextFiveFixture.Prerequisites.Concat(MachinesCriticalNextFiveFixture.Missions).Concat(new[]{201207,201208,201209,201210,201211}).ToArray();
 public const int StandardReceipt=986,NextMission=201217,Seed=201212,PhaseTransitionSeconds=60;
 public static readonly int[] Prerequisites=EarlierStoryReceipts.Append(StandardReceipt).ToArray();
 public const string SourcePin="MXO_MACHINES_AFTER201211_SOURCE_SHA256";
 public static void RequirePin(string? expected,string actual)
 {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Exact Machines after201211 source pin required");}
 public static MissionProgress.SavedState? ReadCheckpointHex(string hex)=>MachinesCriticalNextFiveFixture.ReadCheckpointHex(hex);
 public static string[] LootIdentities(byte[] packet,bool accepted)=>MachinesCriticalNextFiveFixture.LootIdentities(packet,accepted);
 public static bool CurrentHostile(MissionProgress.SavedMob actor,IReadOnlySet<ushort> authored)=>MachinesCriticalNextFiveFixture.CurrentHostile(actor,authored);
 // Independent current-policy L50 literals; headers have no reference-level annotation.201212 is reconstructed.
 public static (uint Xp,uint Info) ExpectedReward(int id)=>id switch
 {201212=>(88000,59500),201213=>(88000,59500),201214=>(93500,63000),201215=>(61625,63000),201216=>(93500,63000),_=>throw new ArgumentOutOfRangeException(nameof(id))};
 public static IEnumerable<XElement> RequiredActions(XElement phase)=>phase.Elements("objective").Where(g=>(string?)g.Attribute("type")!="Protect"&&(int?)g.Attribute("optional")!=1);
 public static int[] TrackedFollowerIds(XElement mission,int phase)=>mission.Elements("phase").ElementAt(phase).Elements("objective").Where(g=>(string?)g.Attribute("type") is "Protect" or "Escort").SelectMany(g=>((string)g.Attribute("npc")!).Split(',').Select(int.Parse)).Distinct().ToArray();
 public static int[] ProtectedIds(XElement mission,int phase)=>mission.Elements("phase").ElementAt(phase).Elements("objective").Where(g=>(string?)g.Attribute("type")=="Protect").SelectMany(g=>((string)g.Attribute("npc")!).Split(',').Select(int.Parse)).Distinct().ToArray();
 public static string[] NpcReferences(XElement goal)
 {var ids=((string?)goal.Attribute("npc")??throw new InvalidDataException("NPC goal references required")).Split(',');if(ids.Length==0||ids.Any(x=>!ushort.TryParse(x,out var id)||id==0)||ids.Distinct().Count()!=ids.Length)throw new InvalidDataException("Exact distinct authored NPC references required");return (string?)goal.Attribute("type")=="Loot"?ids.Take(1).ToArray():ids;}
 public static bool CarryCheckpoint(int mission,int phase,int goal)=>mission==201212&&phase==1&&goal==5;
 public const uint CarryItem=6815;
 public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
 public static IReadOnlyDictionary<string,string> Environment=>new Dictionary<string,string>
 { ["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]=Seed.ToString(),["MXO_MOB_WANDER"]="0",["MXO_MOBS"]="test",["MXO_AGGRO"]="0",["MXO_PACKET_LOG"]="1"};
}
