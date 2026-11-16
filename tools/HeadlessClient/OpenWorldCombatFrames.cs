namespace HeadlessClient;
/// <summary>Closed current-view combat grammar. No arbitrary sized skip, resync, or inferred NPC health/physical pose.</summary>
public sealed class OpenWorldCombatFrames
{
    public sealed record View(ushort Goid,byte Spawn,bool Known);
    public sealed record Frame(int Length,string Kind,ushort? DeletedView=null);
    sealed class Combat { public byte Spawn; public Dictionary<byte,uint> Fighters=new();public bool OwnParticipant,FinalSeen; }
    readonly Func<ushort,View?> view;readonly uint own;
    readonly Dictionary<ushort,Combat> combats=new();readonly HashSet<(ushort,byte)> retired=new();readonly Queue<(ushort,byte)> handoffs=new();
    public OpenWorldCombatFrames(Func<ushort,View?> currentView,uint ownHandle){view=currentView;own=ownHandle;}
    static void Need(bool yes,string why){if(!yes)throw new InvalidDataException("Combat frame: "+why);}
    bool Character(uint handle){ushort id=(ushort)handle;var current=view(id);return current is{Known:true}&&(current.Goid==12||current.Goid==599)&&(handle>>16)==current.Spawn;}
    static bool Prefix(byte[] b,int at,string text){var raw=Convert.FromHexString(text);return at>=0&&at+raw.Length<=b.Length&&b.AsSpan(at,raw.Length).SequenceEqual(raw);}
    static void FiniteDoubles(byte[] b,int at){for(int i=0;i<3;i++)Need(double.IsFinite(BitConverter.ToDouble(b,at+i*8)),"nonfinite centre/position");}
    static void Entry(byte[] b,int at,Dictionary<byte,uint> fighters)
    {
        byte a=b[at],c=b[at+1];Need(a!=c&&fighters.ContainsKey(a)&&fighters.ContainsKey(c),"entry references foreign fighter slot");
        for(int i=0;i<6;i++)Need(float.IsFinite(BitConverter.ToSingle(b,at+6+i*4)),"nonfinite entry offset");
        // Remaining fixed121B fields are the exact serialized timing/animation/result fields, not a new damage decoder.
    }
    Combat Current(ushort id)
    {Need(combats.TryGetValue(id,out var c),"unintroduced combat view");var actual=view(id);Need(actual is{Known:true,Goid:55}&&actual.Spawn==c!.Spawn,"combat view birth was deleted/reused");return c!;}
    public bool TryRead(byte[] b,int at,out Frame frame)
    {
        frame=null!;if(at<0||at+2>b.Length)return false;ushort id=BitConverter.ToUInt16(b,at);
        if(Prefix(b,at,"010002"))
        {
            Need(at+5<=b.Length,"truncated sized header");int total=3+BitConverter.ToUInt16(b,at+3);Need(total>=41&&at+total<=b.Length,"bounded sized initialize length");
            ushort combatId=BitConverter.ToUInt16(b,at+5);var birth=view(combatId);Need(birth is{Known:true,Goid:55}&&!retired.Contains((combatId,birth.Spawn)),"initialize lacks current combat55 birth");Need(b[at+7]==1,"initialize flag");FiniteDoubles(b,at+8);
            uint mask=BitConverter.ToUInt32(b,at+36);Need(mask!=0&&(mask&~63u)==0,"initialize fighter mask");int cursor=at+40;var fighters=new Dictionary<byte,uint>();
            for(byte slot=1;slot<=6;slot++)if((mask&(1u<<(slot-1)))!=0){Need(cursor+4<at+total,"truncated fighter handle");uint handle=BitConverter.ToUInt32(b,cursor);Need(Character(handle),"foreign/stale fighter handle");fighters.Add(slot,handle);cursor+=4;}
            Need(fighters.Values.Distinct().Count()==fighters.Count,"duplicate fighter handle");int count=b[cursor++];Need(count<=1&&cursor+count*121==at+total,"initialize exact entry/length");if(count==1)Entry(b,cursor,fighters);
            if(combats.TryGetValue(combatId,out var old))Need(old.Spawn==birth!.Spawn,"unretired combat incarnation replaced");
            combats[combatId]=new(){Spawn=birth!.Spawn,Fighters=fighters,OwnParticipant=fighters.Values.Contains(own)};frame=new(total,"initialize");return true;
        }
        if(Prefix(b,at,"0100010100")&&at+7<=b.Length)
        {
            ushort deleted=BitConverter.ToUInt16(b,at+5);if(!combats.TryGetValue(deleted,out var ended))return false;
            _=Current(deleted);combats.Remove(deleted);retired.Add((deleted,ended.Spawn));if(ended.OwnParticipant)handoffs.Enqueue((deleted,ended.Spawn));frame=new(7,"combat-delete",deleted);return true;
        }
        if(id==2&&Prefix(b,at+2,"010c"))
        {
            Need(handoffs.Count>0&&Character(own),"own handoff without an owned retired combat");Need(at+21<=b.Length,"truncated own handoff");for(int i=0;i<3;i++)Need(float.IsFinite(BitConverter.ToSingle(b,at+5+i*4)),"nonfinite own handoff");handoffs.Dequeue();frame=new(21,"own-handoff");return true;
        }
        if(!combats.ContainsKey(id))return false;var combat=Current(id);Need(at+3<=b.Length,"truncated known combat view");
        if(b[at+2]==1)
        {
            Need(at+36<=b.Length,"truncated round/final header");FiniteDoubles(b,at+3);uint mask=BitConverter.ToUInt32(b,at+31);int cursor=at+35;
            Need(mask==0||mask==0x80000000,"unproved round fighter-change mask");
            if(mask==0x80000000){Need(cursor+5<=b.Length,"truncated final mask");uint removed=BitConverter.ToUInt32(b,cursor);cursor+=4;uint known=combat.Fighters.Keys.Aggregate(0u,(m,s)=>m|(1u<<(s-1)));Need((removed&~known)==0,"final removes foreign slot");int count=b[cursor++];Need(count==0,"final cannot carry round entries");foreach(byte slot in combat.Fighters.Keys.ToArray())if((removed&(1u<<(slot-1)))!=0)combat.Fighters.Remove(slot);combat.FinalSeen=true;}
            else {Need(combat.Fighters.Values.All(Character),"round fighter lifecycle changed");int count=b[cursor++];Need(count==1&&cursor+121<=b.Length,"exact121B round entry");Entry(b,cursor,combat.Fighters);cursor+=121;}
            frame=new(cursor-at,mask==0?"round":"final");return true;
        }
        if(b[at+2]==2)
        {Need(combat.FinalSeen&&combat.OwnParticipant&&at+28<=b.Length&&b[at+3]==1,"position requires owned final and exact flag");FiniteDoubles(b,at+4);frame=new(28,"final-own-position");return true;}
        throw new InvalidDataException("Combat frame: unsupported known-view subrecord");
    }
}
