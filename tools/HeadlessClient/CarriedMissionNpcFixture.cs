using System.Text.Json;
using System.Text.Json.Serialization;
namespace HeadlessClient;
/// <summary>Read-only disposable fixture evidence; never grants items or mutates mission/NPC state.</summary>
public static class CarriedMissionNpcFixture
{
    public sealed record Token(int Mission,int Instance,int OriginPhase,ushort Npc,string Name,string Identity,bool Friendly)
    {
        [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? CheckpointVersion {get;init;}
        [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? AcceptedIdentity {get;init;}
        [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? AcceptedStarted {get;init;}
    }
    static (string Identity,long Started) V2Owner(JsonElement root)
    {
        if(!root.TryGetProperty("Identity",out var identity)||identity.ValueKind!=JsonValueKind.String||!Guid.TryParseExact(identity.GetString(),"N",out _)||
            !root.TryGetProperty("Started",out var started)||started.ValueKind!=JsonValueKind.Number||!started.TryGetInt64(out long at)||at<=0||
            !root.TryGetProperty("Instance",out var instance)||instance.ValueKind!=JsonValueKind.Number||!instance.TryGetInt32(out int id)||id<=0||id>ushort.MaxValue)
            throw new InvalidDataException("Malformed V2 carried accepted-owner scope");
        return(identity.GetString()!,at);
    }
    public sealed record Point(double X,double Y,double Z);
    private static JsonElement Mob(JsonElement root,int mission,int phase,ushort npc,string name,bool friendly,bool giver)
    {
        int version=root.GetProperty("Version").GetInt32();
        if((version!=1&&version!=2) || root.GetProperty("Mission").GetInt32()!=mission || root.GetProperty("Phase").GetInt32()!=phase)
            throw new InvalidDataException("Wrong owned checkpoint mission/phase/version");
        if(version==2)_=V2Owner(root);
        var rows=root.GetProperty("Mobs").EnumerateArray().Where(m=>m.GetProperty("Npc").GetUInt16()==npc).ToArray();
        if(rows.Length!=1)throw new InvalidDataException("Expected one exact carried NPC identity");var m=rows[0];
        if(m.GetProperty("Name").GetString()!=name || m.GetProperty("Dead").GetBoolean() || m.GetProperty("Health").GetInt32()<=0 || m.GetProperty("Friendly").GetBoolean()!=friendly || giver&&!m.GetProperty("Giver").GetBoolean())
            throw new InvalidDataException("Carried NPC name/life/side/giver mismatch");
        return m;
    }
    public static Token Capture(string payload,int mission,int phase,ushort npc,string name,bool friendly,OwnViewLifeSnapshot? ownerLife=null)
    {
        using var doc=JsonDocument.Parse(payload);var root=doc.RootElement;var m=Mob(root,mission,phase,npc,name,friendly,false);
        if(!root.GetProperty("Following").EnumerateArray().Any(n=>n.GetUInt16()==npc))throw new InvalidDataException("NPC was not actually following before the phase transition");
        string identity=m.GetProperty("Identity").GetString()!;if(!Guid.TryParseExact(identity,"N",out _))throw new InvalidDataException("Invalid carried identity");
        var token=new Token(mission,root.GetProperty("Instance").GetInt32(),phase,npc,name,identity,friendly);
        if(root.GetProperty("Version").GetInt32()==2)
        {
            var owner=V2Owner(root);
            if(ownerLife==null||!ownerLife.Current||!ownerLife.OwnViewReady)throw new InvalidDataException("V2 carried capture requires its current own-view life");
            token=token with{CheckpointVersion=2,AcceptedIdentity=owner.Identity,AcceptedStarted=owner.Started};
        }
        return token;
    }
    public static Point Resolve(string payload,Token token,int phase,bool giver)
    {
        using var doc=JsonDocument.Parse(payload);var root=doc.RootElement;var m=Mob(root,token.Mission,phase,token.Npc,token.Name,token.Friendly,giver);
        int expectedVersion=token.CheckpointVersion??1;
        if((expectedVersion!=1&&expectedVersion!=2)||root.GetProperty("Version").GetInt32()!=expectedVersion)throw new InvalidDataException("Carried checkpoint version cannot change or downgrade");
        if(expectedVersion==2)
        {
            var owner=V2Owner(root);
            if(owner.Identity!=token.AcceptedIdentity||owner.Started!=token.AcceptedStarted)
                throw new InvalidDataException("Carried NPC belongs to another V2 acceptance");
        }
        if(phase<=token.OriginPhase || root.GetProperty("Instance").GetInt32()!=token.Instance || m.GetProperty("Identity").GetString()!=token.Identity)
            throw new InvalidDataException("Carried checkpoint identity/instance/phase mismatch");
        var point=new Point(m.GetProperty("X").GetDouble(),m.GetProperty("Y").GetDouble(),m.GetProperty("Z").GetDouble());
        if(!double.IsFinite(point.X)||!double.IsFinite(point.Y)||!double.IsFinite(point.Z))throw new InvalidDataException("Nonfinite carried position");return point;
    }
    // Opt-in only for this chain's observed recreated-on-new-phase path. No historical retained/deleted-view fallback.
    public static ViewCreation RecreatedView(IReadOnlyList<ViewCreation> rows,int phaseCursor,Token token,Point? expected=null)
    {
        if(token.CheckpointVersion.HasValue)throw new InvalidDataException("V2 carried evidence requires the current-own-life reader");
        return RecreatedViewCore(rows,phaseCursor,token,expected);
    }
    private static ViewCreation RecreatedViewCore(IReadOnlyList<ViewCreation> rows,int phaseCursor,Token token,Point? expected=null)
    {
        bool SideMatches(ViewCreation v)
        {
            if(v.Goid!=599)return false;
            bool hasEnemy=v.Attributes.TryGetValue("IsEnemy",out var enemy);
            if(hasEnemy && (enemy!.Length!=1 || enemy[0]>1))return false;
            if(!token.Friendly)return hasEnemy&&enemy![0]==1;
            // Mob's known friendly mission profile omits IsEnemy, emits DontAttack=1 and its owned MissionKey.
            // TalkDefaultable is not a side flag: hostile actors may also offer dialogue.
            return (!hasEnemy||enemy![0]==0) && v.Attributes.TryGetValue("DontAttack",out var safe)&&safe.Length==1&&safe[0]==1 &&
                v.Attributes.TryGetValue("MissionKey",out var key)&&key.Length==4&&BitConverter.ToUInt16(key,1)==token.Instance&&key[3]==2;
        }
        var view=rows.Skip(phaseCursor).GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>v.Name==token.Name&&SideMatches(v)&&
            (expected==null||v.Attributes.TryGetValue("Position",out var at)&&at.Length==24&&Math.Abs(BitConverter.ToDouble(at)-expected.X)<1&&Math.Abs(BitConverter.ToDouble(at,8)-expected.Y)<1&&Math.Abs(BitConverter.ToDouble(at,16)-expected.Z)<1));
        return view??throw new InvalidDataException("No matching current-phase recreated carried view; old history is not admissible");
    }
    public sealed record CurrentView(ViewCreation View,Point Position,int CreationSection,int PositionSection,string PositionBytes,long OwnLife=-1,bool Retained=false);
    // Strict profile only. Position is the latest observed current-generation value, never a matching history search.
    public static CurrentView RecreatedCurrentView(IReadOnlyList<ViewCreation> rows,int phaseCursor,Token token,Point expected,IReadOnlyList<byte[]> sections,int sectionCursor)
    {
        var view=RecreatedView(rows,phaseCursor,token);int birth=-1;
        for(int i=sectionCursor;i<sections.Count;i++)
            if(WireFormat.FindCreations(sections[i]).Any(v=>v.ViewId==view.ViewId&&v.SpawnId==view.SpawnId&&v.Goid==view.Goid))birth=i;
        if(birth<0||!view.Attributes.TryGetValue("Position",out var initial)||initial.Length!=24)throw new InvalidDataException("No current-generation creation/position in this phase");
        Point? latest=new(BitConverter.ToDouble(initial),BitConverter.ToDouble(initial,8),BitConverter.ToDouble(initial,16));int positionSection=birth;string bytes=Convert.ToHexString(initial);
        byte[] id=BitConverter.GetBytes(view.ViewId),deleted=new byte[]{1,0,1,1,0}.Concat(id).ToArray();
        for(int i=birth+1;i<sections.Count;i++)
        {
            byte[] section=sections[i];
            if(BytePattern.IndexOf(section,deleted,0)>=0)throw new InvalidDataException("Current carried view was deleted after creation");
            // The actual follower producer Mob.updateAnimation is viewLE,02,0e,animation,yaw,XYZ-f32.
            // Accept only completely framed lists of these known18-byte records; do not scan floats for a convenient match.
            bool movements=section.Length>0&&section.Length%18==0&&Enumerable.Range(0,section.Length/18).All(j=>section[j*18+2]==2&&section[j*18+3]==0x0e);
            if(movements)
            {
                for(int at=0;at<section.Length;at+=18)if(BitConverter.ToUInt16(section,at)==view.ViewId)
                {latest=new(BitConverter.ToSingle(section,at+6),BitConverter.ToSingle(section,at+10),BitConverter.ToSingle(section,at+14));positionSection=i;bytes=Convert.ToHexString(section.AsSpan(at+6,12));}
            }
            else if(BytePattern.IndexOf(section,id,0)>=0)
            {
                // An unsupported record which could mention this view invalidates position evidence until a later
                // fully framed actual position. This is conservative QA refusal, not a general03 decoder.
                latest=null;positionSection=i;bytes="";
            }
        }
        if(latest==null||!double.IsFinite(latest.X)||!double.IsFinite(latest.Y)||!double.IsFinite(latest.Z)||
            Math.Abs(latest.X-expected.X)>=1||Math.Abs(latest.Y-expected.Y)>=1||Math.Abs(latest.Z-expected.Z)>=1)
            throw new InvalidDataException("Latest current-generation carried position does not match current owned XYZ");
        return new(view,latest,birth,positionSection,bytes);
    }
    private sealed class LiveView
    {
        internal ViewCreation View;
        internal Point? Position;
        internal int Birth, PositionSection;
        internal string PositionBytes;
        internal bool GenerationKnown = true;
    }

    // Portable accepted-NPC identity is separate from this particular current session's receipt.
    // Event object provenance rejects mixing another log's records even when both life counters are zero.
    private static OwnViewLifeSnapshot RequireV2Stream(OwnViewLifeSnapshot stream,Func<OwnViewLifeSnapshot>? readCurrent)
    {
        if(!stream.Current||!stream.OwnViewReady||stream.Owner==null||readCurrent==null)throw new InvalidDataException("Current V2 carried session reader required");
        // The caller supplies WorldSession.CurrentOwnViewLife, which owns its snapshot lock.
        var current=readCurrent();
        if(!current.Current||!current.OwnViewReady||!ReferenceEquals(current.Owner,stream.Owner)||current.Life!=stream.Life||current.OwnBirthSection!=stream.OwnBirthSection||current.Events.Count<stream.Events.Count||
            Enumerable.Range(0,stream.Events.Count).Any(i=>!ReferenceEquals(current.Events[i],stream.Events[i])))
            throw new InvalidDataException("V2 carried receipt contains stale or foreign session events");
        return current;
    }
    public static CurrentView CurrentLiveView(OwnViewLifeSnapshot stream, Token token, string checkpoint,
        int phase, bool giver, int phaseSectionCursor,Func<OwnViewLifeSnapshot>? readCurrent=null)
    {
        Point expected = Resolve(checkpoint, token, phase, giver);
        if(token.CheckpointVersion==2)stream=RequireV2Stream(stream,readCurrent);
        if (!stream.Current || !stream.OwnViewReady || stream.OwnBirthSection < 0 || stream.Events.Any(e => e.Life != stream.Life))
            throw new InvalidDataException("No current own-view life; previous-life events are inadmissible");
        var live = new Dictionary<ushort, LiveView>();
        var retired = new HashSet<(ushort View, byte Spawn)>();
        bool ownBirthSeen = false;
        foreach (var e in stream.Events)
        {
            if (e.Section < stream.OwnBirthSection) continue;
            byte[] section = e.Bytes;
            var creations = WireFormat.FindCreationEvents(section);
            int start = 0;
            if (e.Section == stream.OwnBirthSection)
            {
                var own = creations.LastOrDefault(c => c.Creation.ViewId == 2 && c.Creation.Goid == 12);
                if (own == null) throw new InvalidDataException("Current own-view birth is absent");
                ownBirthSeen = true;
                start = own.Offset + own.Length;
            }
            if (!ownBirthSeen) throw new InvalidDataException("Current own-view birth must precede carried objects");
            var known = new List<(int Start, int End, WireFormat.CreationEvent? Birth, ushort? Deleted)>();
            foreach (var creation in creations.Where(c => c.Offset >= start))
                known.Add((creation.Offset, creation.Offset + creation.Length, creation, null));
            for (int at = start; at + 7 <= section.Length; at++)
            {
                if (creations.Any(c => at >= c.Offset && at < c.Offset + c.Length)) continue;
                if (section[at] == 1 && section[at + 1] == 0 && section[at + 2] == 1 && section[at + 3] == 1 && section[at + 4] == 0)
                {
                    known.Add((at, at + 7, null, BitConverter.ToUInt16(section, at + 5)));
                    at += 6;
                }
            }
            foreach (var record in known.OrderBy(k => k.Start))
            {
                if (record.Deleted.HasValue)
                {
                    if (live.Remove(record.Deleted.Value, out var removed)) retired.Add((removed.View.ViewId, removed.View.SpawnId));
                    continue;
                }
                var view = record.Birth!.Creation;
                if (live.TryGetValue(view.ViewId, out var previous) && previous.View.SpawnId == view.SpawnId &&
                    previous.View.Goid == view.Goid && previous.View.Attributes.Count == view.Attributes.Count &&
                    previous.View.Attributes.All(a => view.Attributes.TryGetValue(a.Key, out var bytes) && bytes.SequenceEqual(a.Value)))
                    continue; // retransmitted identical birth is not a new generation or position
                if (previous != null) retired.Add((previous.View.ViewId, previous.View.SpawnId));
                Point? position = view.Attributes.TryGetValue("Position", out var p) && p.Length == 24
                    ? new Point(BitConverter.ToDouble(p), BitConverter.ToDouble(p, 8), BitConverter.ToDouble(p, 16)) : null;
                live[view.ViewId] = new LiveView { View=view, Position=position, Birth=e.Section,
                    PositionSection=e.Section, PositionBytes=p == null ? "" : Convert.ToHexString(p),
                    GenerationKnown=!retired.Contains((view.ViewId, view.SpawnId)) };
            }
            bool movements = start == 0 && section.Length > 0 && section.Length % 18 == 0 &&
                Enumerable.Range(0, section.Length / 18).All(j => section[j * 18 + 2] == 2 && section[j * 18 + 3] == 0x0e);
            if (movements)
            {
                for (int at = 0; at < section.Length; at += 18)
                    if (live.TryGetValue(BitConverter.ToUInt16(section, at), out var current))
                    {
                        current.Position = new Point(BitConverter.ToSingle(section, at + 6), BitConverter.ToSingle(section, at + 10), BitConverter.ToSingle(section, at + 14));
                        current.PositionSection=e.Section; current.PositionBytes=Convert.ToHexString(section.AsSpan(at + 6, 12));
                    }
            }
            else
            {
                // Only unparsed gaps can invalidate position evidence; creation bytes are not movement records.
                var gaps = new List<byte[]>(); int at = start;
                foreach (var record in known.OrderBy(k => k.Start))
                {
                    if (record.Start > at) gaps.Add(section[at..record.Start]);
                    at = Math.Max(at, record.End);
                }
                if (at < section.Length) gaps.Add(section[at..]);
                foreach (var current in live.Values)
                    if (gaps.Any(g => BytePattern.IndexOf(g, BitConverter.GetBytes(current.View.ViewId), 0) >= 0))
                    {
                        current.Position=null; current.PositionSection=e.Section; current.PositionBytes="";
                        if (gaps.Any(g => BytePattern.IndexOf(g, new byte[] { 1, 0, 0x0c }, 0) >= 0 ||
                                          BytePattern.IndexOf(g, new byte[] { 1, 0, 8 }, 0) >= 0)) current.GenerationKnown=false;
                    }
            }
        }
        if (!ownBirthSeen) throw new InvalidDataException("No current own-view birth");
        var candidates = live.Values.Where(v => v.View.Goid == 599 && v.View.Name == token.Name).ToArray();
        if (candidates.Length != 1) throw new InvalidDataException("Expected exactly one current live carried generation");
        var selected = candidates[0];
        _ = RecreatedViewCore(new[] { selected.View }, 0, token); // name, side and exact owned instance key, no history search
        var latest = selected.Position;
        if (!selected.GenerationKnown || latest == null || !double.IsFinite(latest.X) || !double.IsFinite(latest.Y) || !double.IsFinite(latest.Z) ||
            Math.Abs(latest.X - expected.X) >= 1 || Math.Abs(latest.Y - expected.Y) >= 1 || Math.Abs(latest.Z - expected.Z) >= 1)
            throw new InvalidDataException("Latest current live carried position does not match current owned XYZ");
        if(token.CheckpointVersion==2)_=RequireV2Stream(stream,readCurrent);
        if (!stream.Current) throw new InvalidDataException("Own-view life changed while reading carried generation");
        return new CurrentView(selected.View, latest, selected.Birth, selected.PositionSection, selected.PositionBytes,
            stream.Life, selected.Birth < phaseSectionCursor);
    }
    public static async Task<string> ReadOwnedAsync(ScenarioContext context,HeadlessPlayer actor)
    {
        if(!context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal)||actor.CharId<=2)throw new InvalidOperationException("Disposable owned checkpoint required");
        string hex=await context.Database.ScalarAsync($"SELECT HEX(payload) FROM char_active_missions WHERE charId={actor.CharId}");
        if(string.IsNullOrEmpty(hex)||hex=="NULL")throw new InvalidDataException("No active owned checkpoint");return System.Text.Encoding.UTF8.GetString(Convert.FromHexString(hex));
    }
}
