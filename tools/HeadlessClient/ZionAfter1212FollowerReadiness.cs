using hds;
using System.Xml.Linq;
namespace HeadlessClient;

/// <summary>Current-session, read-only readiness for an exit after a disclosed player movement aid.</summary>
public static class ZionAfter1212FollowerReadiness
{
    public sealed record Observation(ushort Npc,ushort View,byte Spawn,long Life,int Birth,int PositionSection,double[] Position);
    private sealed record Live(ViewCreation View,string Raw,int Birth,int PositionSection,double[]? Position,bool Known,bool Dead);
    public static ushort[] OpenFollowers(XElement definition,MissionProgress.SavedState state)
    {
        var xml=new System.Xml.XmlDocument();xml.LoadXml(definition.ToString());var mission=MissionXml.Parse(xml.DocumentElement!);
        if(mission.id!=state.Mission||state.Phase<0||state.Phase>=mission.phases.Count)throw new InvalidDataException("Wrong escort mission/phase");
        return mission.phases[state.Phase].missionObjectives.Where(o=>o.type==MissionGoalType.Escort&&o.escortTo=="exit"&&!state.Done.Contains(o.goal)&&
            (o.afterGoal<0||state.Done.Contains(o.afterGoal))).SelectMany(o=>o.Npcs).Distinct().ToArray();
    }
    static bool SameEntry(MissionProgress.SavedState before,MissionProgress.SavedState current)
    {try{ZionAfter1212RidePlan.SameEntry(before,current);return true;}catch(InvalidDataException){return false;}}
    public static IReadOnlyList<Observation> Require(OwnViewLifeSnapshot stream,long expectedLife,string handle,
        MissionProgress.SavedState before,MissionProgress.SavedState current,IReadOnlyList<ushort> followers,double[] player)
    {
        if(!stream.Current||!stream.OwnViewReady||stream.Life!=expectedLife||stream.OwnBirthSection<0||stream.Events.Any(e=>e.Life!=stream.Life)||
            player.Length!=3||player.Any(v=>!double.IsFinite(v))||!SameEntry(before,current)||followers.Count==0)
            throw new InvalidDataException("Wrong current escort owner/life/site");
        var live=Read(stream,handle);
        var result=new List<Observation>();
        foreach(ushort id in followers)
        {
            var old=before.Mobs.Single(m=>m.Npc==id);var mobs=current.Mobs.Where(m=>m.Npc==id).ToArray();
            if(mobs.Length!=1||!current.Following.Contains(id)||!Guid.TryParseExact(old.Identity,"N",out _)||mobs[0].Identity!=old.Identity||mobs[0].Dead||mobs[0].Health==0||
                mobs[0].Name!=old.Name||mobs[0].Friendly!=old.Friendly||mobs[0].Phase!=old.Phase)throw new InvalidDataException("Wrong live following identity");
            var mob=mobs[0];byte[] name=CharacterNameBytes(mob.Name);bool Match(Live v)=>v.View.Goid==599&&v.View.Attributes.TryGetValue("CharacterName",out var actualName)&&actualName.SequenceEqual(name)&&v.View.Attributes.TryGetValue("MissionKey",out var key)&&key.Length==4&&key[0]==mob.Phase&&BitConverter.ToUInt16(key,1)==current.Instance&&key[3]==2&&
                v.View.Attributes.TryGetValue("Level",out var level)&&level.Length==1&&level[0]==mob.Level&&
                (mob.Friendly?v.View.Attributes.TryGetValue("DontAttack",out var safe)&&safe.SequenceEqual(new byte[]{1})&&(!v.View.Attributes.TryGetValue("IsEnemy",out var side)||side.SequenceEqual(new byte[]{0})):
                    v.View.Attributes.TryGetValue("IsEnemy",out var enemy)&&enemy.SequenceEqual(new byte[]{1}));
            double[] saved={mob.X,mob.Y,mob.Z};
            // Retail mission groups can share name, level and side. Their current positions distinguish the bodies.
            var matches=live.Values.Where(v=>Match(v)&&v.Position!=null&&
                Enumerable.Range(0,3).All(i=>Math.Abs(v.Position[i]-saved[i])<1)).ToArray();
            if(matches.Length!=1||!matches[0].Known||matches[0].Dead||matches[0].Position==null)throw new InvalidDataException("No unique current live escort view");
            var chosen=matches[0];var position=chosen.Position!;
            if(result.Any(r=>r.View==chosen.View.ViewId))throw new InvalidDataException("Escort view already belongs to another follower");
            if(position.Any(v=>!double.IsFinite(v))||Enumerable.Range(0,3).Any(i=>Math.Abs(position[i]-saved[i])>=1)||
                Enumerable.Range(0,3).Sum(i=>Math.Pow(position[i]-player[i],2))>1500d*1500d||
                Enumerable.Range(0,3).Sum(i=>Math.Pow(saved[i]-player[i],2))>1500d*1500d)throw new InvalidDataException("Current follower has not reached existing escort range");
            result.Add(new(id,chosen.View.ViewId,chosen.View.SpawnId,stream.Life,chosen.Birth,chosen.PositionSection,position));
        }
        if(!stream.Current)throw new InvalidDataException("Escort own life changed");return result;
    }

    private static Dictionary<ushort,Live> Read(OwnViewLifeSnapshot stream,string handle)
    {
        if(!stream.Current||!stream.OwnViewReady||stream.OwnBirthSection<0||stream.Events.Any(e=>e.Life!=stream.Life))throw new InvalidDataException("Wrong current NPC own life");
        var schemas=new CurrentViewSchemas();
        var live=new Dictionary<ushort,Live>();var retired=new HashSet<(ushort,byte)>();bool own=false;
        foreach(var e in stream.Events)
        {
            if(e.Section<stream.OwnBirthSection)continue;var bytes=e.Bytes;var births=WireFormat.FindCreationEvents(bytes);int at=0;
            if(e.Section==stream.OwnBirthSection)
            {
                var self=births.LastOrDefault(b=>b.Creation.ViewId==2&&b.Creation.Goid==12&&b.Creation.Name==handle)??throw new InvalidDataException("Wrong current own2 identity");
                schemas.Birth(self.Creation);at=self.Offset+self.Length;own=true;
            }
            if(!own)throw new InvalidDataException("Follower precedes current own2");
            while(at<bytes.Length)
            {
                var birth=births.FirstOrDefault(b=>b.Offset==at);
                if(birth!=null)
                {
                    var v=birth.Creation;schemas.Birth(v);string raw=Convert.ToHexString(bytes.AsSpan(at,birth.Length));
                    if(!live.TryGetValue(v.ViewId,out var prior)||prior.Raw!=raw)
                    {
                        if(prior!=null)retired.Add((prior.View.ViewId,prior.View.SpawnId));
                        double[]? pos=v.Attributes.TryGetValue("Position",out var p)&&p.Length==24?Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(p,i*8)).ToArray():null;
                        bool dead=v.Attributes.TryGetValue("IsDead",out var d)&&(d.Length!=1||d[0]!=0);
                        live[v.ViewId]=new(v,raw,e.Section,e.Section,pos,!retired.Contains((v.ViewId,v.SpawnId)),dead);
                    }
                    at+=birth.Length;continue;
                }
                ushort? kind=at+2<=bytes.Length?schemas.Lookup(BitConverter.ToUInt16(bytes,at),live.TryGetValue(BitConverter.ToUInt16(bytes,at),out var addressed)?addressed.View.Goid:null):null;
                if(!MervFollowupViewFrames.TryRead(bytes,at,kind,out var frame))throw new InvalidDataException($"Unknown current view frame: class {kind}");
                if(frame.Type==MervFollowupViewFrames.Kind.Delete)
                {if(live.Remove(frame.View,out var old))retired.Add((old.View.ViewId,old.View.SpawnId));}
                else if(live.TryGetValue(frame.View,out var changed))
                {
                    if(frame.Type==MervFollowupViewFrames.Kind.Death)live[frame.View]=changed with{Dead=true};
                    else if(frame.PositionOffset>=0)live[frame.View]=changed with{Position=Enumerable.Range(0,3).Select(i=>(double)BitConverter.ToSingle(bytes,at+frame.PositionOffset+i*4)).ToArray(),PositionSection=e.Section};
                }
                at+=frame.Length;
            }
        }
        if(!stream.Current)throw new InvalidDataException("NPC own life changed");return live;
    }
    // Mob.CharacterName -> Attribute(size32).setValue(string) -> ASCII bytes copied/truncated/zero-padded.
    // Compare every wire byte; identical truncated names still require unique current candidates.
    public static byte[] CharacterNameBytes(string savedName)
    {
        if(savedName==null)throw new InvalidDataException("Saved NPC name required");
        var bytes=new byte[32];var source=System.Text.Encoding.ASCII.GetBytes(savedName);
        Array.Copy(source,bytes,Math.Min(source.Length,bytes.Length));return bytes;
    }
    public static ViewCreation? CurrentNpc(OwnViewLifeSnapshot stream,string handle,MissionProgress.SavedMob mob,ushort instance)
    {
        if(instance==0||!Guid.TryParseExact(mob.Identity,"N",out _)||mob.Level<=0||mob.Level>255)throw new InvalidDataException("Current saved NPC identity required");
        byte[] name=CharacterNameBytes(mob.Name);
        bool Match(Live v)
        {
            if(v.View.Goid!=599||!v.View.Attributes.TryGetValue("CharacterName",out var actualName)||!actualName.SequenceEqual(name)||!v.Known||v.Position==null||v.Dead!=mob.Dead||
                !v.View.Attributes.TryGetValue("Level",out var level)||!level.SequenceEqual(new[]{(byte)mob.Level}))return false;
            bool keyPresent=v.View.Attributes.TryGetValue("MissionKey",out var key);
            if((mob.Friendly&&!keyPresent)||keyPresent&&(key!.Length!=4||key[0]!=mob.Phase||BitConverter.ToUInt16(key,1)!=instance||key[3]!=2))return false;
            bool enemy=v.View.Attributes.TryGetValue("IsEnemy",out var side);
            if(mob.Friendly?(!v.View.Attributes.TryGetValue("DontAttack",out var safe)||!safe.SequenceEqual(new byte[]{1})||enemy&&!side!.SequenceEqual(new byte[]{0})):
                (!enemy||!side!.SequenceEqual(new byte[]{1})))return false;
            return Enumerable.Range(0,3).All(i=>Math.Abs(v.Position[i]-new[]{mob.X,mob.Y,mob.Z}[i])<1);
        }
        var matches=Read(stream,handle).Values.Where(Match).ToArray();if(matches.Length==0)return null;
        if(matches.Length!=1)throw new InvalidDataException("Ambiguous current saved NPC view");var chosen=matches[0];
        var attributes=chosen.View.Attributes.ToDictionary(p=>p.Key,p=>(byte[])p.Value.Clone());
        attributes["Position"]=chosen.Position!.SelectMany(BitConverter.GetBytes).ToArray();attributes["IsDead"]=new[]{(byte)(chosen.Dead?1:0)};
        if(!stream.Current)throw new InvalidDataException("NPC own life changed");
        return new(chosen.View.ViewId,chosen.View.Goid,chosen.View.SpawnId,attributes){RelatedStaticObjectId=chosen.View.RelatedStaticObjectId};
    }
}
