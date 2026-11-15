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
    public static byte NpcLevel(ViewCreation view)
    {
        if(view.Goid!=599||!view.Attributes.TryGetValue("Level",out var level)||level.Length!=1||level[0]==0)
            throw new InvalidDataException("Expected the NPC03 one-byte nonzero Level field");
        return level[0];
    }
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
        int ownLife=0;for(int i=0;i<views.Count;i++)if(views[i].ViewId==2)ownLife=i+1;
        return views.Skip(Math.Max(cursor,ownLife)).GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>v.Goid==599&&v.Name==mob.Name&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
            Math.Abs(BitConverter.ToDouble(pos)-mob.X)<1&&Math.Abs(BitConverter.ToDouble(pos,8)-mob.Y)<1&&Math.Abs(BitConverter.ToDouble(pos,16)-mob.Z)<1&&
            Side(v));
    }
    public static IReadOnlyDictionary<ushort,byte> HostileWirePhases(System.Xml.Linq.XElement definition,int mission,int phase)
    {
        var xml=new System.Xml.XmlDocument();xml.LoadXml(definition.ToString());var parsed=hds.MissionXml.Parse(xml.DocumentElement!);
        if(parsed.id!=mission||phase<0||phase>=parsed.phases.Count)throw new InvalidDataException("Wrong authored current hostile mission/phase");
        // MissionNpc.phase is the authored MissionKey byte, independent of its at/stage (MissionXml.ParseNpc).
        var result=parsed.npcs.Where(n=>n.at==0||n.at==phase+1).ToDictionary(n=>n.id,n=>n.phase);
        var area=parsed.phases[phase];int count=checked(area.enemies+area.waves.Sum(w=>w.count));
        if(area.enemies<0||area.waves.Any(w=>w.count<0)||count>=50)throw new InvalidDataException("Generated hostile count leaves its current-stage ID domain");
        // SpawnWave uses1000+(phaseIndex+1)*50+progress.hostiles.Count and leaves MissionNpc.phase at0.
        int start=checked(1000+(phase+1)*50);
        for(int i=0;i<count;i++)
        {
            ushort id=checked((ushort)(start+i));if(result.ContainsKey(id))throw new InvalidDataException("Authored/generated hostile identity collision");
            result.Add(id,0);
        }
        return result;
    }
    public static IReadOnlyList<ViewCreation> CurrentHostiles(IReadOnlyList<ViewCreation> views,int cursor,hds.MissionProgress.SavedState saved,int mission,int phase,System.Xml.Linq.XElement definition)
    {
        if(saved.Mission!=mission||saved.Phase!=phase)throw new InvalidDataException("Wrong current hostile mission/phase");
        var expected=HostileWirePhases(definition,mission,phase);
        bool OwnerKey(ViewCreation view,hds.MissionProgress.SavedMob mob)=>!view.Attributes.TryGetValue("MissionKey",out var key)||
            key.Length==4&&key[0]==mob.Phase&&BitConverter.ToUInt16(key,1)==saved.Instance&&key[3]==2;
        return saved.Mobs.Where(m=>expected.TryGetValue(m.Npc,out byte wirePhase)&&m.Phase==wirePhase&&!m.Friendly&&!m.Dead&&m.Health>0)
            .Select(m=>(Mob:m,View:CurrentNpc(views,cursor,m))).Where(pair=>pair.View!=null&&OwnerKey(pair.View,pair.Mob)&&
                (!pair.View.Attributes.TryGetValue("IsDead",out var dead)||dead.Length==1&&dead[0]==0))
            .Select(pair=>pair.View!).DistinctBy(v=>(v.ViewId,v.SpawnId)).ToArray();
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
