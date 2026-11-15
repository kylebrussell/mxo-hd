namespace HeadlessClient;
public static class DisturbingTrendsFixture
{
    public const int Mission=201201,LayoutSeed=201101;
    public const uint ExpectedXp=168042,ExpectedInfo=133350;
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208,2201,2202,2203,2204,2205,2206,2207,2208,2209,2210,201101,201102,201103};
    // Object state may legitimately change from unannounced0 to the default Take state2401.
    // The invariant is the complete immutable source and placement, not its live use state.
    public static string FixedDescriptor(hds.MissionProgress.SavedObject obj)=>System.Text.Json.JsonSerializer.Serialize(
        new{obj.Id,obj.Spot,obj.Goid,obj.OpenFloor},new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
    public static CharacterSeed BeforeMargin(CharacterSeed seed)
    {
        if(Prerequisites.Any(id=>id<=0)||Prerequisites.Distinct().Count()!=Prerequisites.Length||Prerequisites.Contains(Mission)||Prerequisites.Contains(Mission+1))
            throw new InvalidOperationException("Prerequisites must be distinct earlier receipts, excluding selected and next mission");
        return seed with {CompletedMissions=Prerequisites};
    }
    public static byte[] ClientPosition(IReadOnlyList<double> root)
    {
        if(root.Count!=3||root.Any(v=>!double.IsFinite(v)||!float.IsFinite((float)v)))throw new ArgumentException("Invalid published root");
        return new byte[]{2,0,1,8}.Concat(root.SelectMany(v=>BitConverter.GetBytes((float)v))).Concat(new byte[]{0,0}).ToArray();
    }
    public static ViewCreation? CurrentNpc(IReadOnlyList<ViewCreation> views,int cursor,hds.MissionProgress.SavedMob mob,int phase,int instance)
    {
        if(cursor<0||cursor>views.Count||mob.Phase!=phase||mob.Dead||mob.Health==0)return null;
        bool Side(ViewCreation v)
        {
            bool present=v.Attributes.TryGetValue("IsEnemy",out var side);
            if(present&&(side!.Length!=1||side[0]>1))return false;
            if(!mob.Friendly)return present&&side![0]==1;
            return (!present||side![0]==0)&&v.Attributes.TryGetValue("DontAttack",out var safe)&&safe.Length==1&&safe[0]==1&&
                v.Attributes.TryGetValue("MissionKey",out var key)&&key.Length==4&&key[0]==phase&&BitConverter.ToUInt16(key,1)==instance&&key[3]==2;
        }
        return views.Skip(cursor).GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>v.Goid==599&&v.Name==mob.Name&&Side(v)&&
            v.Attributes.TryGetValue("Position",out var pose)&&pose.Length==24&&BitConverter.ToDouble(pose)==mob.X&&BitConverter.ToDouble(pose,8)==mob.Y&&BitConverter.ToDouble(pose,16)==mob.Z);
    }
    // Exact current-site lookup; no historical source or door is selected by the fixture.
    public static uint DoorAt(IEnumerable<string> rows,double[] pose)
    {
        var ids=rows.Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])).Select(l=>l.Split(','))
            .Where(f=>f.Length>=11&&f[0]=="1"&&new ushort[]{417,419}.Contains(BitConverter.ToUInt16(Convert.FromHexString(f[4]),0))&&
                Enumerable.Range(0,3).All(i=>double.Parse(f[6+i],System.Globalization.CultureInfo.InvariantCulture)==pose[i]))
            .Select(f=>BitConverter.ToUInt32(Convert.FromHexString(f[2]),0)).Distinct().Take(2).ToArray();
        if(ids.Length!=1)throw new InvalidDataException("Exact current-site door is missing or ambiguous");return ids[0];
    }
    public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
}
