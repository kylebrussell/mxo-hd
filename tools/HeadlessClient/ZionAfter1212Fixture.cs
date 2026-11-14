using hds;
namespace HeadlessClient;
public static class ZionAfter1212Fixture
{
    public static readonly int[] Missions={101213,101214,101215,101216,101217};
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202,101203,101204,101205,101206,101207,101208,101209,101210,101211,101212};
    public static readonly uint[] MissionExperience={68750,68750,243750,149875,281500};
    public static readonly uint[] MissionInformation={91000,91000,91000,91000,91000};
    public const int NextMission=101301,LayoutSeed=101213;
    public const string NextTitle="Lord of Flies";
    public static uint ExpectedKill(int npcLevel)=>(uint)(67d*5d*49d*Math.Pow(3d,(1d+npcLevel-50d)/7d));
    public static ViewCreation? CurrentNpc(IReadOnlyList<ViewCreation> views,int cursor,MissionProgress.SavedMob mob)
    {
        if(cursor<0||cursor>views.Count)return null;
        return views.Skip(cursor).LastOrDefault(v=>v.Goid==599&&v.Name==mob.Name&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
            Math.Abs(BitConverter.ToDouble(pos)-mob.X)<1&&Math.Abs(BitConverter.ToDouble(pos,8)-mob.Y)<1&&Math.Abs(BitConverter.ToDouble(pos,16)-mob.Z)<1&&
            (v.Attributes.TryGetValue("IsEnemy",out var enemy)&&enemy[0]!=0)==mob.Hostile);
    }
    public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
}
