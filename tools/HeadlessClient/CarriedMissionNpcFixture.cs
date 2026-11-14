using System.Text.Json;
namespace HeadlessClient;
/// <summary>Read-only disposable fixture evidence; never grants items or mutates mission/NPC state.</summary>
public static class CarriedMissionNpcFixture
{
    public sealed record Token(int Mission,int Instance,int OriginPhase,ushort Npc,string Name,string Identity,bool Friendly);
    public sealed record Point(double X,double Y,double Z);
    private static JsonElement Mob(JsonElement root,int mission,int phase,ushort npc,string name,bool friendly,bool giver)
    {
        if(root.GetProperty("Version").GetInt32()!=1 || root.GetProperty("Mission").GetInt32()!=mission || root.GetProperty("Phase").GetInt32()!=phase)
            throw new InvalidDataException("Wrong owned checkpoint mission/phase/version");
        var rows=root.GetProperty("Mobs").EnumerateArray().Where(m=>m.GetProperty("Npc").GetUInt16()==npc).ToArray();
        if(rows.Length!=1)throw new InvalidDataException("Expected one exact carried NPC identity");var m=rows[0];
        if(m.GetProperty("Name").GetString()!=name || m.GetProperty("Dead").GetBoolean() || m.GetProperty("Health").GetInt32()<=0 || m.GetProperty("Friendly").GetBoolean()!=friendly || giver&&!m.GetProperty("Giver").GetBoolean())
            throw new InvalidDataException("Carried NPC name/life/side/giver mismatch");
        return m;
    }
    public static Token Capture(string payload,int mission,int phase,ushort npc,string name,bool friendly)
    {
        using var doc=JsonDocument.Parse(payload);var root=doc.RootElement;var m=Mob(root,mission,phase,npc,name,friendly,false);
        if(!root.GetProperty("Following").EnumerateArray().Any(n=>n.GetUInt16()==npc))throw new InvalidDataException("NPC was not actually following before the phase transition");
        string identity=m.GetProperty("Identity").GetString()!;if(!Guid.TryParseExact(identity,"N",out _))throw new InvalidDataException("Invalid carried identity");
        return new(mission,root.GetProperty("Instance").GetInt32(),phase,npc,name,identity,friendly);
    }
    public static Point Resolve(string payload,Token token,int phase,bool giver)
    {
        using var doc=JsonDocument.Parse(payload);var root=doc.RootElement;var m=Mob(root,token.Mission,phase,token.Npc,token.Name,token.Friendly,giver);
        if(phase<=token.OriginPhase || root.GetProperty("Instance").GetInt32()!=token.Instance || m.GetProperty("Identity").GetString()!=token.Identity)
            throw new InvalidDataException("Carried checkpoint identity/instance/phase mismatch");
        var point=new Point(m.GetProperty("X").GetDouble(),m.GetProperty("Y").GetDouble(),m.GetProperty("Z").GetDouble());
        if(!double.IsFinite(point.X)||!double.IsFinite(point.Y)||!double.IsFinite(point.Z))throw new InvalidDataException("Nonfinite carried position");return point;
    }
    // Opt-in only for this chain's observed recreated-on-new-phase path. No historical retained/deleted-view fallback.
    public static ViewCreation RecreatedView(IReadOnlyList<ViewCreation> rows,int phaseCursor,Token token)
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
        var view=rows.Skip(phaseCursor).LastOrDefault(v=>v.Name==token.Name&&SideMatches(v));
        return view??throw new InvalidDataException("No matching current-phase recreated carried view; old history is not admissible");
    }
    public static async Task<string> ReadOwnedAsync(ScenarioContext context,HeadlessPlayer actor)
    {
        if(!context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal)||actor.CharId<=2)throw new InvalidOperationException("Disposable owned checkpoint required");
        string hex=await context.Database.ScalarAsync($"SELECT HEX(payload) FROM char_active_missions WHERE charId={actor.CharId}");
        if(string.IsNullOrEmpty(hex)||hex=="NULL")throw new InvalidDataException("No active owned checkpoint");return System.Text.Encoding.UTF8.GetString(Convert.FromHexString(hex));
    }
}
