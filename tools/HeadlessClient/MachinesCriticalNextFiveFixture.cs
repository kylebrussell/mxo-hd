using hds;
namespace HeadlessClient;
public static class MachinesCriticalNextFiveFixture
{
    public static readonly int[] Missions = {201202,201203,201204,201205,201206};
    public static readonly int[] Prerequisites = {
        1201,1204,1203,1205,1206,1207,1208,
        2201,2202,2203,2204,2205,2206,2207,2208,2209,2210,201101,201102,201103,201201};
    // Existing Machines phase-transition QA synchronization budget; no server timer changes.
    public const int PhaseTransitionSeconds=60;
    public const string SourcePin="MXO_MACHINES_NEXT_FIVE_SOURCE_SHA256";
    public static void RequirePin(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Exact Machines source pin required");}
    public static MissionProgress.SavedState? ReadCheckpointHex(string hex)
    {
        if(hex=="NULL")return null;
        var state=System.Text.Json.JsonSerializer.Deserialize<MissionProgress.SavedState>(MissionBoundCabinetFixture.DecodeCheckpointHex(hex),new System.Text.Json.JsonSerializerOptions{IncludeFields=true})??throw new InvalidDataException("Missing Machines checkpoint");
        if(state.Version!=1&&state.Version!=2)throw new InvalidDataException("Unsupported Machines checkpoint version");return state;
    }
    public static (MissionComputerAnchors.Pose Pose,NavPoint Standing) BoundApproach(MissionProgress.SavedObject obj,double[] from)
    {
        if(obj.OpenFloor!=null||obj.Spot?.type==null||obj.Spot.type.Length<2||BitConverter.ToUInt16(obj.Spot.type)!=obj.Goid||obj.Spot.metrId!=1||from.Length!=3||from.Any(x=>!double.IsFinite(x)))
            throw new InvalidDataException("Exact bound source and current root required");
        var pose=MissionObjectPoses.For(new MissionObjectState{goid=obj.Goid,spot=obj.Spot});
        if(!NavMesh.Locate(1,from[0],from[1],from[2],out _))throw new InvalidDataException("Actual mission Ride root is not NAV");
        var rootSector=SectorProperties.At(1,from[0],from[2]);var objectSector=SectorProperties.At(1,pose.X,pose.Z);
        double flat=(from[0]-pose.X)*(from[0]-pose.X)+(from[2]-pose.Z)*(from[2]-pose.Z);
        if(rootSector==null||objectSector==null||(rootSector.sector!=objectSector.sector&&flat>1600d*1600d))throw new InvalidDataException("Bound approach cannot drop current component origin");
        if(!NavMesh.Project(1,pose.X,pose.Y,pose.Z,out var standing,maxDistance:800,reachableFrom:from))throw new InvalidDataException("Effective bound native furniture has no current-component NAV approach");
        return(pose,standing);
    }
    public static int CertifiedActorIndex(MissionProgress.SavedState state,MissionProgress.SavedMob actor,MissionNpcInteriorProof.Plan plan)
    {
        if(plan.Identity!=state.Identity||plan.Started!=state.Started||plan.Phase!=state.Phase||state.Mobs.Count(m=>m.Npc==actor.Npc&&m.Identity==actor.Identity)!=1)throw new InvalidDataException("Current actor/owned route scope changed");
        var selection=plan.Witness.GetProperty("Selection");
        if(selection.GetProperty("Mission").GetInt32()!=state.Mission)throw new InvalidDataException("Foreign actor route mission");
        string key;
        if(!actor.Hostile)key=$"Npc:{actor.Npc}:0";
        else
        {
            if(!actor.Wave.HasValue)throw new InvalidDataException("Current generated actor lacks wave identity");
            int first=1000+(state.Phase+1)*50;
            var wave=state.Mobs.Where(m=>m.Hostile&&m.Wave==actor.Wave).OrderBy(m=>m.Npc).ToArray();
            var roles=selection.GetProperty("Actors").EnumerateArray().Where(a=>a.GetProperty("Kind").GetString()=="Wave"&&a.GetProperty("Id").GetInt32()==actor.Wave.Value).ToArray();
            if(wave.Length>=50||wave.Length!=roles.Length||wave.Select(m=>m.Identity).Distinct().Count()!=wave.Length||wave.Any(m=>m.Npc<first||m.Npc>=first+50)||wave.Select(m=>m.Npc).Distinct().Count()!=wave.Length||!roles.Select(a=>a.GetProperty("Index").GetInt32()).SequenceEqual(Enumerable.Range(0,roles.Length)))throw new InvalidDataException("Incomplete/wrong-stage generated role group");
            int ordinal=Array.FindIndex(wave,m=>m.Npc==actor.Npc&&m.Identity==actor.Identity);if(ordinal<0)throw new InvalidDataException("Current generated identity absent");key=$"Wave:{actor.Wave.Value}:{ordinal}";
        }
        var proofs=plan.Witness.GetProperty("Actors").EnumerateArray().ToArray();var matches=Enumerable.Range(0,proofs.Length).Where(i=>proofs[i].GetProperty("Role").GetString()==key).ToArray();
        if(matches.Length!=1)throw new InvalidDataException("Missing/ambiguous exact actor role");return matches[0];
    }
    public static byte[] ClientPosition(IReadOnlyList<double> root)
    {
        if(root.Count!=3||root.Any(v=>!double.IsFinite(v)||!float.IsFinite((float)v)))throw new ArgumentException("Invalid current root");
        return new byte[]{2,0,1,8}.Concat(root.SelectMany(v=>BitConverter.GetBytes((float)v))).Concat(new byte[]{0,0}).ToArray();
    }
    public const int NextMission = 201207, Seed = 201202;
    // Saved Hostile denotes generated progress.hostiles membership, not every enemy's side.
    public static bool CurrentHostile(MissionProgress.SavedMob actor,IReadOnlySet<ushort> authoredCurrentStageIds) =>
        !actor.Dead&&!actor.Friendly&&(actor.Hostile||authoredCurrentStageIds.Contains(actor.Npc));
    public static IEnumerable<ViewCreation> LatestViews(IEnumerable<ViewCreation> creations,int cursor) =>
        creations.Skip(cursor).GroupBy(v=>v.ViewId).Select(g=>g.Last());
    public static byte NpcLevel(IReadOnlyDictionary<string,byte[]> attributes)
    {
        if(!attributes.TryGetValue("Level",out var value)||value.Length!=1)
            throw new InvalidDataException("GO599 Level must be exactly one wire byte");
        return value[0];
    }
    public static string[] LootIdentities(byte[] packet,bool accepted)
    {
        int countAt=accepted?12:16,itemsAt=countAt+2;
        if(packet.Length<itemsAt||packet[0]!=0x81||packet[1]!=(accepted?0x20:0x19))
            throw new InvalidDataException("Not the expected loot packet");
        int count=BitConverter.ToUInt16(packet,countAt);
        if(packet.Length!=itemsAt+count*8)throw new InvalidDataException("Malformed loot identity array");
        return Enumerable.Range(0,count).Select(i=>Convert.ToHexString(packet,itemsAt+i*8,8)).ToArray();
    }
    // Poster Hunt is referenced to17 by mergedPR202. The remaining unknown archive levels retain master default50.
    // Independent level50 literals, never generated through production Calculate().
    public static (uint Xp,uint Info) ExpectedReward(int mission) => mission switch
    {
        201202 => (404250,175000), 201203 => (88000,59500), 201204 => (88000,59500),
        201205 => (58000,59500), 201206 => (57920,59500), _ => throw new ArgumentOutOfRangeException(nameof(mission))
    };
    public static string SeedPrerequisites(long character) => "INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+
        string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
    public static IReadOnlyDictionary<string,string> Environment => new Dictionary<string,string>
    {
        ["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]=Seed.ToString(),
        ["MXO_MOB_WANDER"]="0",["MXO_MOBS"]="test",["MXO_AGGRO"]="0",["MXO_PACKET_LOG"]="1"
    };
}
