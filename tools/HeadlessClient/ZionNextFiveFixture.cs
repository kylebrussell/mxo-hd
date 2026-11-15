namespace HeadlessClient;
public static class ZionNextFiveFixture
{
    public static readonly int[] Missions={101208,101209,101210,101211,101212};
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202,101203,101204,101205,101206,101207};
    public static CharacterSeed BeforeMargin(CharacterSeed seed,int[] selected,int[] prior)
    {
        if(prior.Any(id=>id<=0)||prior.Distinct().Count()!=prior.Length||prior.Intersect(selected).Any()||prior.Contains(101213))
            throw new InvalidOperationException("Earlier receipts must be positive, unique and disjoint from selected/next missions");
        return seed with {CompletedMissions=prior};
    }
    public static byte[] ClientPosition(IReadOnlyList<double> root)
    {
        if(root.Count!=3||root.Any(v=>!double.IsFinite(v)||!float.IsFinite((float)v)))throw new ArgumentException("Invalid current root");
        return new byte[]{2,0,1,8}.Concat(root.SelectMany(v=>BitConverter.GetBytes((float)v))).Concat(new byte[]{0,0}).ToArray();
    }
    public static ViewCreation? CurrentBody(IReadOnlyList<ViewCreation> views,hds.MissionProgress.SavedObject obj,ushort instance)
    {
        int life=0;for(int i=0;i<views.Count;i++)if(views[i].ViewId==2)life=i+1;
        return views.Skip(life).GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>v.Goid==obj.Goid&&
            v.Attributes.TryGetValue("MissionKey",out var key)&&key.Length==4&&BitConverter.ToUInt16(key,1)==instance&&key[3]==2&&
            v.Attributes.TryGetValue("Position",out var position)&&position.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(position,i*8)==obj.OpenFloor!.Centre[i])&&
            v.Attributes.TryGetValue("Orientation",out var orientation)&&Convert.ToHexString(orientation)==obj.OpenFloor.Quaternion);
    }
    public const string NextTitle="It's Hard To Find Good Help";
    public static uint ExpectedKill(int npcLevel)=>(uint)(67d*5d*49d*Math.Pow(3d,(1d+npcLevel-50d)/7d));
    public static ViewCreation? CurrentNpc(IReadOnlyList<ViewCreation> views,int cursor,hds.MissionProgress.SavedMob mob)
    {
        if(cursor<0||cursor>views.Count)return null;
        bool Side(ViewCreation v)
        {
            bool present=v.Attributes.TryGetValue("IsEnemy",out var side);
            if(present&&(side!.Length!=1||side[0]>1))return false;
            if(!mob.Friendly)return present&&side![0]==1;
            // Friendly mission actors omit IsEnemy and publish DontAttack=1 (Mob.toObject).
            return (!present||side![0]==0)&&v.Attributes.TryGetValue("DontAttack",out var safe)&&safe.Length==1&&safe[0]==1;
        }
        return views.Skip(cursor).GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>v.Goid==599&&v.Name==mob.Name&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
            Math.Abs(BitConverter.ToDouble(pos)-mob.X)<1&&Math.Abs(BitConverter.ToDouble(pos,8)-mob.Y)<1&&Math.Abs(BitConverter.ToDouble(pos,16)-mob.Z)<1&&
            Side(v));
    }
    public static IReadOnlyList<ViewCreation> CurrentHostiles(IReadOnlyList<ViewCreation> views,int cursor,hds.MissionProgress.SavedState saved,int mission,int phase)
    {
        if(saved.Mission!=mission||saved.Phase!=phase)throw new InvalidDataException("Wrong current hostile mission/phase");
        return saved.Mobs.Where(m=>m.Phase==phase&&!m.Friendly&&!m.Dead&&m.Health>0)
            .Select(m=>CurrentNpc(views,cursor,m)).Where(v=>v!=null&&(!v.Attributes.TryGetValue("IsDead",out var dead)||dead.Length==1&&dead[0]==0))
            .Select(v=>v!).DistinctBy(v=>(v.ViewId,v.SpawnId)).ToArray();
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
