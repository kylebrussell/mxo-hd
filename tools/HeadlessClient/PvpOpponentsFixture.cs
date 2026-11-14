using System.Collections.ObjectModel;
using hds;
namespace HeadlessClient;
/// <summary>Tools-only live-entry witness; no flag, relationship or duel mutation.</summary>
public static class PvpOpponentsFixture
{
    public const int X=17000,Y=495,Z=2300,EnemyX=17120,FriendX=16880;
    public static readonly ushort[] Abilities={2,17};
    public const string HostileSetting="3a 05 00 11 00 0a 00 50 76 50 53 65 72 76 65 72 00 04 00 06 00 00 00";
    public const string SafeSetting="3a 05 00 17 00 10 00 50 76 50 4d 61 78 53 61 66 65 4c 65 76 65 6c 00 04 00 0f 00 00 00";
    public const string SelfFlag="02 80 80 80 80 80 80 01 00 00 00 01";
    public static IReadOnlyDictionary<string,string> Environment=>new ReadOnlyDictionary<string,string>(new Dictionary<string,string>{
        ["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_DEV_COMMANDS"]="0",["MXO_MOB_WANDER"]="0",["MXO_MOBS"]="test",["MXO_AGGRO"]="0"});
    public static string NativeSql=>$"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},pvpflag=0,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,x=CASE charId WHEN 110 THEN {X} WHEN 6111 THEN {FriendX} WHEN 6112 THEN 17600 WHEN 6113 THEN 16600 ELSE {EnemyX} END,y={Y},z={Z},rotation=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN(110,6111,6112,6113,6114)";
    public static async Task<object> EnterAsync(HeadlessPlayer player)
    {
        await player.EnterWorldAsync();
        await player.WaitUntilAsync(()=>player.Received(HostileSetting).Count!=0&&player.Received(SafeSetting).Count!=0,"Actual hostile6/safe15 world settings",TimeSpan.FromSeconds(10));
        await player.WaitForViewUpdateAsync(2,SelfFlag);
        var self=player.World.CreationSnapshot().Last(v=>v.ViewId==2);
        return new{player.Handle,player.CharId,Settings=player.World.RpcSnapshot().Where(r=>r.Bytes[0]==0x3a).Select(r=>r.Hex).ToArray(),
            Self=self.Attributes.ToDictionary(p=>p.Key,p=>Convert.ToHexString(p.Value)),RawViews=player.World.ViewSnapshot().Select(Convert.ToHexString).ToArray()};
    }
    public static object Observe(ViewCreation view,byte organization,uint character)
    {
        Check.That(view.Attributes.TryGetValue("CharacterID",out var id)&&id.Length==4&&BitConverter.ToUInt32(id)==character,"Actual observer character identity");
        Check.That(view.Attributes.TryGetValue("OrganizationID",out var org)&&org.Length==1&&org[0]==organization,"Actual observer organization field");
        Check.That(view.Attributes.TryGetValue("Level",out var level)&&level.Length==1&&level[0]==50,"Actual observer level50 field");
        Check.That(view.Attributes.TryGetValue("ConditionStateFlags",out var flag)&&flag.Length==4&&(BitConverter.ToUInt32(flag)&PvpFlag.PvpBit)!=0,"Actual observer PvP flag in creation");
        foreach(string name in new[]{"DuelID"})
            if(view.Attributes.TryGetValue(name,out var value))Check.That(value.Length==4&&BitConverter.ToUInt32(value)==0,"Unassigned observed "+name);
        if(view.Attributes.TryGetValue("MissionTeamID",out var team))Check.Equal(4,team.Length,"Actual team field width; no team override");
        return new{view.ViewId,view.SpawnId,view.Goid,Attributes=view.Attributes.ToDictionary(p=>p.Key,p=>Convert.ToHexString(p.Value)),
            DuelFieldPresent=view.Attributes.ContainsKey("DuelID"),TeamFieldPresent=view.Attributes.ContainsKey("MissionTeamID"),OmittedFieldsAreNotFabricatedValues=true};
    }
    //Literal health-only update fields used by the normal opening damage; scoped view+cursor prevents regen/history confusion.
    public static ushort[] HealthHistory(IEnumerable<byte[]> lists,int cursor,ushort view,bool self)
    {
        byte[] prefix=BitConverter.GetBytes(view).Concat(WireFormat.FromHex(self?"02 80 80 80 40":"02 80 80 10")).ToArray();
        var values=new List<ushort>();
        foreach(var list in lists.Skip(cursor))for(int at=BytePattern.IndexOf(list,prefix,0);at>=0;at=BytePattern.IndexOf(list,prefix,at+1))
            if(at+prefix.Length+2<=list.Length)values.Add(BitConverter.ToUInt16(list,at+prefix.Length));
        return values.ToArray();
    }
}
