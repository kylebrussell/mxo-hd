using System.Xml.Linq;
using hds;
namespace HeadlessClient;
public static class MachinesAfter201206Fixture
{
 public static readonly int[] Missions={201207,201208,201209,201210,201211};
 public static readonly int[] Prerequisites=MachinesCriticalNextFiveFixture.Prerequisites.Concat(MachinesCriticalNextFiveFixture.Missions).ToArray();
 public const int NextMission=201212,Seed=201207,PhaseTransitionSeconds=60;
 public const string SourcePin="MXO_MACHINES_AFTER201206_SOURCE_SHA256";
 public static void RequirePin(string? expected,string actual)
 {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Exact Machines after201206 source pin required");}
 public static MissionProgress.SavedState? ReadCheckpointHex(string hex)=>MachinesCriticalNextFiveFixture.ReadCheckpointHex(hex);
 public static string[] LootIdentities(byte[] packet,bool accepted)=>MachinesCriticalNextFiveFixture.LootIdentities(packet,accepted);
 public static bool CurrentHostile(MissionProgress.SavedMob actor,IReadOnlySet<ushort> authored)=>MachinesCriticalNextFiveFixture.CurrentHostile(actor,authored);
 // Current XML has no reference-level annotation; these independent L50 current-policy literals are NOT retail L50 claims.
 public static (uint Xp,uint Info) ExpectedReward(int id)=>id switch
 {201207=>(58000,59500),201208=>(116560,59500),201209=>(88000,59500),201210=>(88000,59500),201211=>(44000,59500),_=>throw new ArgumentOutOfRangeException(nameof(id))};
 public static int[] ProtectedIds(XElement mission,int phase)=>mission.Elements("phase").ElementAt(phase).Elements("objective").Where(g=>(string?)g.Attribute("type")=="Protect").SelectMany(g=>((string)g.Attribute("npc")!).Split(',').Select(int.Parse)).Distinct().ToArray();
 public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
 public static IReadOnlyDictionary<string,string> Environment=>new Dictionary<string,string>
 { ["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]=Seed.ToString(),["MXO_MOB_WANDER"]="0",["MXO_MOBS"]="test",["MXO_AGGRO"]="0",["MXO_PACKET_LOG"]="1"};
}
