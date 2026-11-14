using hds;
using System.Text.RegularExpressions;

namespace HeadlessClient;
public static class EngageFoesFixture
{
    public const string ExpectedSourceEnvironment="MXO_ENGAGE_FOES_SOURCE_SHA256";
    public static void RequireSource(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!string.Equals(expected,actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Engage Foes proof requires root-pinned source hash via "+ExpectedSourceEnvironment);}
    public static ushort[] Abilities=>new ushort[]{2,1008,610};
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"};
    public static CharacterSeed Seed(string name,bool blocker=false)=>new(name){X=blocker?24699:25300,Y=495,Z=-1498,Organization=1,
        Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Health=10000,MaxHealth=10000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=(blocker?new ushort[]{2,17}:Abilities).Select(id=>(id,(ushort)50)).ToArray()};
    public static ViewCreation? CurrentNpc(IEnumerable<ViewCreation> views,string name)=>views.GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>
        v.Goid==599&&v.Name==name&&v.Attributes.TryGetValue("Health",out var health)&&health.Length==2&&BitConverter.ToUInt16(health)>0);
    public static async Task<ViewCreation> BlockNpcAsync(HeadlessPlayer actor)
    {
        await actor.WaitUntilAsync(()=>CurrentNpc(actor.World.CreationSnapshot(),"WinterFrost")!=null,"actual live WinterFrost",TimeSpan.FromSeconds(15));
        var npc=CurrentNpc(actor.World.CreationSnapshot(),"WinterFrost")!;
        actor.SendRpc("42 03 00 00 00 00 00"); // ordinary Block tactic, no effect/aggro/health injection
        actor.SendRpc($"40 {WireFormat.ToHex(BitConverter.GetBytes(npc.ViewId))} {WireFormat.ToHex(BitConverter.GetBytes((ushort)npc.SpawnId))} 00 6b");
        await actor.WaitUntilAsync(()=>actor.World.CreationSnapshot().Any(v=>v.Goid==55),"ordinary Block interlock",TimeSpan.FromSeconds(15));
        await RequireFullHealthAsync(actor,npc);
        return npc;
    }
    public static async Task<int> RequireFullHealthAsync(HeadlessPlayer observer,ViewCreation npc)
    {
        int maximum=BitConverter.ToUInt16(npc.Attributes["MaxHealth"]);
        observer.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(npc.ViewId))} {npc.SpawnId:x2}");
        int cursor=observer.World.RpcSnapshot().Count;observer.SkipReceived();observer.SendChat("?combatdepth status");
        await observer.WaitUntilAsync(()=>observer.World.RpcSnapshot().Skip(cursor).Any(r=>System.Text.Encoding.ASCII.GetString(r.Bytes).Contains("Combat depth combatants")),"read-only current NPC status",TimeSpan.FromSeconds(10));
        string status=System.Text.Encoding.ASCII.GetString(observer.World.RpcSnapshot().Skip(cursor).Last(r=>System.Text.Encoding.ASCII.GetString(r.Bytes).Contains("Combat depth combatants")).Bytes);
        var hp=Regex.Match(status,@"HP (\d+)");Check.That(hp.Success,"Actual current NPC HP response");Check.Equal(maximum,int.Parse(hp.Groups[1].Value),"NPC at full HP before610, generic damaged-target retaliation cannot mask missing initializer");
        return maximum;
    }
}
