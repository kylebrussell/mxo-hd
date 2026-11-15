using System.Text.Json;
using hds;
namespace HeadlessClient;
/// <summary>Package-local current-life NPC evidence; never changes views, ACKs or mission state.</summary>
public static class MervAfterCurrentNpc
{
    public sealed record Scope(int Mission,int Phase,ushort Instance,string MissionIdentity,long Started,ushort Npc,string Descriptor);
    public sealed record Point(double X,double Y,double Z);
    public sealed record CurrentView(ViewCreation View,Point Position,int CreationSection,int PositionSection,string PositionBytes,long OwnLife,bool WireMissionKeyAbsent)
    {
        public string IdentityAssociation=>WireMissionKeyAbsent
            ? "SQL accepted mission/instance/NPC UUID + current-view corroboration; no wire ownership proof"
            : "SQL accepted mission/NPC UUID + current-view corroboration and exact wire instance key";
    }
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static MissionProgress.SavedMob Owned(MissionProgress.SavedState state,ushort npc)
    {
        if(state.Version is not (1 or 2)||state.Mission<=0||state.Instance==0||state.Phase<0||state.Started<=0||
            !Guid.TryParseExact(state.Identity,"N",out _)||!state.Flags.GetValueOrDefault("entered")||state.Flags.GetValueOrDefault("failed"))
            throw new InvalidDataException("No current entered owned NPC mission scope");
        var actor=state.Mobs.Single(m=>m.Npc==npc);
        if(!Guid.TryParseExact(actor.Identity,"N",out _)||!actor.Dead&&actor.Health<=0||!new[]{actor.X,actor.Y,actor.Z}.All(double.IsFinite))throw new InvalidDataException("Invalid saved NPC UUID/pose");
        return actor;
    }
    public static Scope Capture(MissionProgress.SavedState state,ushort npc)
    {
        var actor=Owned(state,npc);
        return new(state.Mission,state.Phase,state.Instance,state.Identity,state.Started,npc,
            JsonSerializer.Serialize(new{actor.Identity,actor.Name,actor.Phase,actor.Level,actor.Rsi,actor.Friendly,actor.Dead,actor.X,actor.Y,actor.Z},Fields));
    }
    private sealed class LiveView
    {
        internal ViewCreation View;
        internal Point? Position;
        internal int Birth, PositionSection;
        internal string PositionBytes;
        internal bool GenerationKnown = true;
        internal bool Dead;
        internal bool DynamicBirth;
    }

    static bool OrdinaryUnkeyedHostile(LiveView selected,MissionProgress.SavedMob actor)
    {
        var v=selected.View;
        bool Bytes(string name,string expected)=>v.Attributes.TryGetValue(name,out var raw)&&raw.SequenceEqual(Convert.FromHexString(expected));
        if(!selected.DynamicBirth||actor.Friendly||string.IsNullOrWhiteSpace(actor.Rsi)||v.Goid!=599||v.Attributes.ContainsKey("DontAttack")||
            !Bytes("IsEnemy","01")||!Bytes("IsFriendly","00")||!Bytes("TitleAbility","00100000")||
            !Bytes("ConditionStateFlags","00001000")||!Bytes("CombatantMode","22")||!Bytes("NPCRank","01"))return false;
        if(!v.Attributes.TryGetValue("IsDead",out var dead)||dead.Length!=1||dead[0]>1||
            !v.Attributes.TryGetValue("CurrentState",out var state)||state.Length!=4||BitConverter.ToUInt32(state)!=(dead[0]==1?0x7fdu:0x806u))return false;
        try{return Bytes("MoreInfoID",actor.Rsi);}catch(FormatException){return false;}
    }

    public static CurrentView Require(OwnViewLifeSnapshot stream, Scope scope, MissionProgress.SavedState state)
    {
        var actor=Owned(state,scope.Npc);
        if(Capture(state,scope.Npc)!=scope)throw new InvalidDataException("NPC owned mission/phase/UUID/pose changed");
        Point expected=new(actor.X,actor.Y,actor.Z);
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
            int cursor=start;
            while(cursor<section.Length)
            {
                var birth=creations.FirstOrDefault(c=>c.Offset==cursor);
                if(birth!=null)
                {
                    var view=birth.Creation;
                    if(live.TryGetValue(view.ViewId,out var previous)&&previous.View.SpawnId==view.SpawnId&&
                        previous.View.Goid==view.Goid&&previous.View.Attributes.Count==view.Attributes.Count&&
                        previous.View.Attributes.All(a=>view.Attributes.TryGetValue(a.Key,out var bytes)&&bytes.SequenceEqual(a.Value)))
                    {cursor+=birth.Length;continue;}
                    if(previous!=null)retired.Add((previous.View.ViewId,previous.View.SpawnId));
                    Point? position=view.Attributes.TryGetValue("Position",out var p)&&p.Length==24
                        ?new(BitConverter.ToDouble(p),BitConverter.ToDouble(p,8),BitConverter.ToDouble(p,16)):null;
                    live[view.ViewId]=new LiveView {View=view,Position=position,Birth=e.Section,PositionSection=e.Section,
                        PositionBytes=p==null?"":Convert.ToHexString(p),DynamicBirth=section[cursor+2]==0x0c,
                        Dead=view.Attributes.TryGetValue("IsDead",out var dead)&&dead.Length==1&&dead[0]!=0,
                        GenerationKnown=!retired.Contains((view.ViewId,view.SpawnId))};
                    cursor+=birth.Length;continue;
                }
                ushort? goid=cursor+2<=section.Length&&live.TryGetValue(BitConverter.ToUInt16(section,cursor),out var addressed)
                    ?addressed.View.Goid:cursor+2<=section.Length&&BitConverter.ToUInt16(section,cursor)==2?(ushort)12:null;
                if(!MervAfterCombatFrames.TryRead(section,cursor,goid,out var frame))
                {
                    // Unknown frame has no proven boundary or actor. Do not search its payload for IDs/births.
                    foreach(var current in live.Values){current.GenerationKnown=false;current.Position=null;}
                    break;
                }
                if(frame.Type==MervAfterCombatFrames.Kind.Delete)
                {if(live.Remove(frame.View,out var removed))retired.Add((removed.View.ViewId,removed.View.SpawnId));}
                else if(live.TryGetValue(frame.View,out var current))
                {
                    if(frame.PositionOffset>=0)
                    {
                        int pos=cursor+frame.PositionOffset;
                        current.Position=new(BitConverter.ToSingle(section,pos),BitConverter.ToSingle(section,pos+4),BitConverter.ToSingle(section,pos+8));
                        current.PositionSection=e.Section;current.PositionBytes=Convert.ToHexString(section.AsSpan(pos,12));
                    }
                    if(frame.Type==MervAfterCombatFrames.Kind.Death)current.Dead=true;
                }
                cursor+=frame.Length;
            }
        }
        if (!ownBirthSeen) throw new InvalidDataException("No current own-view birth");
        var candidates = live.Values.Where(v => v.View.Goid == 599 && v.View.Name == actor.Name &&
            v.Position!=null && Math.Abs(v.Position.X-actor.X)<1 && Math.Abs(v.Position.Y-actor.Y)<1 && Math.Abs(v.Position.Z-actor.Z)<1).ToArray();
        if (candidates.Length != 1) throw new InvalidDataException("Expected exactly one current live carried generation");
        var selected = candidates[0];
        var v=selected.View;
        bool enemy=v.Attributes.TryGetValue("IsEnemy",out var side)&&side.Length==1&&side[0]==1;
        bool safe=v.Attributes.TryGetValue("DontAttack",out var noAttack)&&noAttack.Length==1&&noAttack[0]==1;
        byte[] expectedKey={(byte)actor.Phase,(byte)state.Instance,(byte)(state.Instance>>8),2};
        bool hasKey=v.Attributes.TryGetValue("MissionKey",out var key);
        bool level=v.Attributes.TryGetValue("Level",out var rankLevel)&&rankLevel.Length==1&&rankLevel[0]==actor.Level;
        if(enemy==actor.Friendly || actor.Friendly&&!safe || !level ||
            (hasKey?!key!.SequenceEqual(expectedKey):actor.Friendly||!OrdinaryUnkeyedHostile(selected,actor)) ||
            actor.Dead!=selected.Dead)throw new InvalidDataException("NPC current side/level/mission key/death mismatch");
        var latest = selected.Position;
        if (!selected.GenerationKnown || latest == null || !double.IsFinite(latest.X) || !double.IsFinite(latest.Y) || !double.IsFinite(latest.Z) ||
            Math.Abs(latest.X - expected.X) >= 1 || Math.Abs(latest.Y - expected.Y) >= 1 || Math.Abs(latest.Z - expected.Z) >= 1)
            throw new InvalidDataException("Latest current live carried position does not match current owned XYZ");
        if (!stream.Current) throw new InvalidDataException("Own-view life changed while reading carried generation");
        return new CurrentView(selected.View, latest, selected.Birth, selected.PositionSection, selected.PositionBytes,
            stream.Life,!hasKey);
    }
}
