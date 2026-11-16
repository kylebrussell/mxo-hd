using hds;
namespace HeadlessClient;
public static class ZionAfter101217Fixture
{
    public static readonly int[] Missions={101301,101302,101303,101304,101306};
    public static readonly int[] Prerequisites={1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202,101203,101204,101205,101206,101207,101208,101209,101210,101211,101212,101213,101214,101215,101216,101217};
    public static readonly uint[] MissionExperience={309010,308750,357500,423360,423360};
    public static readonly uint[] MissionInformation={94500,94500,94500,98000,98000};
    public const string SourcePin="MXO_ZION_AFTER101217_SOURCE_SHA256";
    public static void RequirePin(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Zion after1212 requires exact source pin via "+SourcePin);}
    public const int NextMission=102101,LayoutSeed=101301;
    public const string NextTitle="Better Killing Through Chemistry";
    public static byte NpcLevel(ViewCreation target)
    {if(target.Goid!=599||!target.Attributes.TryGetValue("Level",out var level)||level.Length!=1)throw new InvalidDataException("NPC03 Level requires its one-byte schema field");return level[0];}
    public static uint ExpectedKill(int npcLevel)=>(uint)(67d*5d*49d*Math.Pow(3d,(1d+npcLevel-50d)/7d));
    public static CharacterSeed BeforeMargin(CharacterSeed seed,int[] selected,int[] prior)
    {
        if(!selected.SequenceEqual(Missions)||prior.Length!=37||!prior.Order().SequenceEqual(Prerequisites.Order())||prior.Contains(101305)||
            prior.Any(id=>id<=0)||prior.Distinct().Count()!=prior.Length||prior.Intersect(selected).Any()||prior.Contains(NextMission))
            throw new InvalidOperationException("Earlier receipts must be positive, unique and disjoint from selected/next missions");
        return seed with {CompletedMissions=prior};
    }
    public static byte[] ClientPosition(IReadOnlyList<double> root)
    {
        if(root.Count!=3||root.Any(v=>!double.IsFinite(v)||!float.IsFinite((float)v)))throw new ArgumentException("Invalid current root");
        return new byte[]{2,0,1,8}.Concat(root.SelectMany(v=>BitConverter.GetBytes((float)v))).Concat(new byte[]{0,0}).ToArray();
    }
    public static IReadOnlyList<ViewCreation> CurrentHostiles(OwnViewLifeSnapshot stream,string handle,hds.MissionProgress.SavedState saved,int mission,int phase,IReadOnlySet<ushort> currentAuthoredNpcs)
    {
        if(saved.Mission!=mission||saved.Phase!=phase)throw new InvalidDataException("Wrong current hostile mission/phase");
        return saved.Mobs.Where(m=>(m.Hostile||currentAuthoredNpcs.Contains(m.Npc))&&!m.Friendly&&!m.Dead&&m.Health>0)
            .Select(m=>ZionAfter1212FollowerReadiness.CurrentNpc(stream,handle,m,saved.Instance)).Where(v=>v!=null).Select(v=>v!).DistinctBy(v=>(v.ViewId,v.SpawnId)).ToArray();
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
    public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(id=>$"({character},{id})"));
}
