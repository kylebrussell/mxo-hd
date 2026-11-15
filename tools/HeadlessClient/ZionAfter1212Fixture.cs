using hds;
namespace HeadlessClient;
public static class ZionAfter1212Fixture
{
    public static readonly int[] Missions={101213,101214,101215,101216,101217};
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202,101203,101204,101205,101206,101207,101208,101209,101210,101211,101212};
    public static readonly uint[] MissionExperience={68750,68750,243750,149875,281500};
    public static readonly uint[] MissionInformation={91000,91000,91000,91000,91000};
    public const string SourcePin="MXO_ZION_AFTER1212_SOURCE_SHA256";
    public static void RequirePin(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Zion after1212 requires exact source pin via "+SourcePin);}
    public const int NextMission=101301,LayoutSeed=101213;
    public const string NextTitle="Lord of Flies";
    public static byte NpcLevel(ViewCreation target)
    {if(target.Goid!=599||!target.Attributes.TryGetValue("Level",out var level)||level.Length!=1)throw new InvalidDataException("NPC03 Level requires its one-byte schema field");return level[0];}
    public static uint ExpectedKill(int npcLevel)=>(uint)(67d*5d*49d*Math.Pow(3d,(1d+npcLevel-50d)/7d));
    public static CharacterSeed BeforeMargin(CharacterSeed seed,int[] selected,int[] prior)
    {
        if(prior.Any(id=>id<=0)||prior.Distinct().Count()!=prior.Length||prior.Intersect(selected).Any()||prior.Contains(NextMission))
            throw new InvalidOperationException("Earlier receipts must be positive, unique and disjoint from selected/next missions");
        return seed with {CompletedMissions=prior};
    }
    public static byte[] ClientPosition(IReadOnlyList<double> root)
    {
        if(root.Count!=3||root.Any(v=>!double.IsFinite(v)||!float.IsFinite((float)v)))throw new ArgumentException("Invalid current root");
        return new byte[]{2,0,1,8}.Concat(root.SelectMany(v=>BitConverter.GetBytes((float)v))).Concat(new byte[]{0,0}).ToArray();
    }
    public static ViewCreation? CurrentBody(IReadOnlyList<ViewCreation> views,hds.MissionProgress.SavedObject obj,ushort instance,int cursor=0)
    {
        if(cursor<0||cursor>views.Count)return null;int life=cursor;for(int i=0;i<views.Count;i++)if(views[i].ViewId==2)life=Math.Max(life,i+1);
        return views.Skip(life).GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>v.Goid==obj.Goid&&
            v.Attributes.TryGetValue("MissionKey",out var key)&&key.Length==4&&key[0]==0xff&&BitConverter.ToUInt16(key,1)==instance&&key[3]==2&&
            v.Attributes.TryGetValue("Position",out var position)&&position.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(position,i*8)==obj.OpenFloor!.Centre[i])&&
            v.Attributes.TryGetValue("Orientation",out var orientation)&&Convert.ToHexString(orientation)==obj.OpenFloor.Quaternion);
    }
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
    public static IReadOnlyList<ViewCreation> CurrentHostiles(IReadOnlyList<ViewCreation> views,int cursor,hds.MissionProgress.SavedState saved,int mission,int phase,IReadOnlySet<ushort> currentAuthoredNpcs)
    {
        if(saved.Mission!=mission||saved.Phase!=phase)throw new InvalidDataException("Wrong current hostile mission/phase");
        // SavedMob.Phase is the MissionKey wire byte, not the story stage. Hostile is current progress.hostiles.
        return saved.Mobs.Where(m=>(m.Hostile||currentAuthoredNpcs.Contains(m.Npc))&&!m.Friendly&&!m.Dead&&m.Health>0)
            .Select(m=>CurrentNpc(views,cursor,m)).Where(v=>v!=null&&(!v.Attributes.TryGetValue("IsDead",out var dead)||dead.Length==1&&dead[0]==0))
            .Select(v=>v!).DistinctBy(v=>(v.ViewId,v.SpawnId)).ToArray();
    }
    public static IReadOnlyList<byte[]> CompletionSummaries(IReadOnlyList<byte[]> previousSessions,IReadOnlyList<byte[]> currentSession,int cursor)
    {
        var records=previousSessions.Concat(currentSession).ToArray();if(cursor<0||cursor>records.Length)throw new ArgumentOutOfRangeException(nameof(cursor));
        return records.Skip(cursor).Where(b=>b.Length>=2&&b[0]==0x80&&b[1]==0xa2).ToArray();
    }
    public sealed record CompletionCheckpointRow(string? PayloadHex,string? CasReceiptHex);
    public static void RequireCompletedCheckpoint(IReadOnlyList<CompletionCheckpointRow> rows,long earnedCompletions)
    {
        // A retained NULL-payload CAS receipt row is deliberately inactive. Its token is evidence, not progress.
        if(rows.Count>1||rows.Any(row=>row.PayloadHex!=null)||earnedCompletions!=1)throw new InvalidDataException("Completion requires no active payload and exactly one earned receipt");
    }
    public static byte[] RequireCompletion(IReadOnlyList<byte[]> summaries,uint experience,uint information)
    {
        if(summaries.Count!=1)throw new InvalidDataException("Exactly one current-mission completion summary required");var b=summaries[0];
        if(b.Length!=26||b[2]!=0||BitConverter.ToUInt32(b,15)!=experience||BitConverter.ToUInt32(b,11)!=information)throw new InvalidDataException("Current completion summary status/payout mismatch");return b;
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
