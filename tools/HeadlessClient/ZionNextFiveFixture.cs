namespace HeadlessClient;
public static class ZionNextFiveFixture
{
    public static readonly int[] Missions={101208,101209,101210,101211,101212};
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202,101203,101204,101205,101206,101207};
    public const string NextTitle="It's Hard To Find Good Help";
    public static uint ExpectedKill(int npcLevel)=>(uint)(67d*5d*49d*Math.Pow(3d,(1d+npcLevel-50d)/7d));
    public static ViewCreation? CurrentNpc(IReadOnlyList<ViewCreation> views,int cursor,hds.MissionProgress.SavedMob mob)
    {
        if(cursor<0||cursor>views.Count)return null;
        return views.Skip(cursor).LastOrDefault(v=>v.Goid==599&&v.Name==mob.Name&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
            Math.Abs(BitConverter.ToDouble(pos)-mob.X)<1&&Math.Abs(BitConverter.ToDouble(pos,8)-mob.Y)<1&&Math.Abs(BitConverter.ToDouble(pos,16)-mob.Z)<1&&
            v.Attributes.TryGetValue("IsEnemy",out var side)&&side.Length>0&&(side[0]!=0)==!mob.Friendly);
    }
    public static uint DoorAt(IEnumerable<string> rows,double[] pose)
    {
        var ids=rows.Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])).Select(l=>l.Split(','))
            .Where(f=>f.Length>=11&&f[0]=="1"&&new ushort[]{417,419}.Contains(BitConverter.ToUInt16(Convert.FromHexString(f[4]),0))&&
                Enumerable.Range(0,3).All(i=>double.Parse(f[6+i],System.Globalization.CultureInfo.InvariantCulture)==pose[i]))
            .Select(f=>BitConverter.ToUInt32(Convert.FromHexString(f[2]),0)).Distinct().Take(2).ToArray();
        if(ids.Length!=1)throw new InvalidDataException("Exact current-site door missing or ambiguous");return ids[0];
    }
    public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
}
