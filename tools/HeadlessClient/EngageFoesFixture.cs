using hds;
using System.Text.RegularExpressions;

namespace HeadlessClient;
public static class EngageFoesFixture
{
    public const string InitialNpcsEnvironment="HEADLESS_ENGAGE_FOES_NPCS";
    public const double PrimaryX=24499,PrimaryY=495,PrimaryZ=-1198,SecondaryX=24499,SecondaryY=495,SecondaryZ=-1498;
    public const int SecondaryInitialHealth=10000;
    public const string ExpectedSourceEnvironment="MXO_ENGAGE_FOES_SOURCE_SHA256";
    public static void RequireSource(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!string.Equals(expected,actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Engage Foes proof requires root-pinned source hash via "+ExpectedSourceEnvironment);}
    public static ushort[] Abilities=>new ushort[]{2,1008,610};
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",[InitialNpcsEnvironment]="1"};
    public static CharacterSeed Seed(string name,bool blocker=false)=>new(name){X=24699,Y=495,Z=-1498,Organization=1,
        Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Health=10000,MaxHealth=10000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=(blocker?new ushort[]{2,17}:Abilities).Select(id=>(id,(ushort)50)).ToArray()};
    public static ViewCreation? CurrentNpc(IEnumerable<ViewCreation> views,string name)=>views.GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>
        v.Goid==599&&v.Name==name&&v.Attributes.TryGetValue("Health",out var health)&&health.Length==2&&BitConverter.ToUInt16(health)>0);
    public static Task<ViewCreation> BlockNpcAsync(HeadlessPlayer actor)=>EngageNpcAsync(actor,true);
    public static Task<ViewCreation> DefaultTacticNpcAsync(HeadlessPlayer actor)=>EngageNpcAsync(actor,false);
    private static async Task<ViewCreation> EngageNpcAsync(HeadlessPlayer actor,bool block)
    {
        await actor.WaitUntilAsync(()=>CurrentNpc(actor.World.CreationSnapshot(),"WinterFrost")!=null,"actual live WinterFrost",TimeSpan.FromSeconds(15));
        var npc=CurrentNpc(actor.World.CreationSnapshot(),"WinterFrost")!;
        if(block)actor.SendRpc("42 03 00 00 00 00 00"); // ordinary Block tactic, no effect/aggro/health injection
        actor.SendRpc($"40 {WireFormat.ToHex(BitConverter.GetBytes(npc.ViewId))} {WireFormat.ToHex(BitConverter.GetBytes((ushort)npc.SpawnId))} 00 6b");
        await actor.WaitUntilAsync(()=>actor.World.CreationSnapshot().Any(v=>v.Goid==55),block?"ordinary helper Block interlock":"ordinary default-tactic interlock",TimeSpan.FromSeconds(15));
        return npc;
    }
    public static async Task<int> RequireFullHealthAsync(HeadlessPlayer observer,ViewCreation npc)
    {
        int maximum=BitConverter.ToUInt16(npc.Attributes["MaxHealth"]);
        int actual=await CurrentHealthAsync(observer,npc);Check.Equal(maximum,actual,"NPC at full HP before610, generic damaged-primary retaliation cannot mask missing initializer");return actual;
    }
    public static async Task<int> CurrentHealthAsync(HeadlessPlayer observer,ViewCreation npc)
    {
        observer.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(npc.ViewId))} {npc.SpawnId:x2}");
        int cursor=observer.World.RpcSnapshot().Count;observer.SkipReceived();observer.SendChat("?combatdepth status");
        await observer.WaitUntilAsync(()=>observer.World.RpcSnapshot().Skip(cursor).Any(r=>System.Text.Encoding.ASCII.GetString(r.Bytes).Contains("Combat depth combatants")),"read-only current NPC status",TimeSpan.FromSeconds(10));
        string status=System.Text.Encoding.ASCII.GetString(observer.World.RpcSnapshot().Skip(cursor).Last(r=>System.Text.Encoding.ASCII.GetString(r.Bytes).Contains("Combat depth combatants")).Bytes);
        var hp=Regex.Match(status,@"HP (\d+)");Check.That(hp.Success,"Actual current NPC HP response");return int.Parse(hp.Groups[1].Value);
    }
    public static double[] Position(ViewCreation npc)
    {var b=npc.Attributes["Position"];if(b.Length!=24)throw new InvalidDataException("Expected actual NPC double pose");return new[]{BitConverter.ToDouble(b,0),BitConverter.ToDouble(b,8),BitConverter.ToDouble(b,16)};}
    public static ushort[] ExpectedRecipients(IEnumerable<ViewCreation> views,ViewCreation primary)
    {
        var center=Position(primary);
        return views.GroupBy(v=>v.ViewId).Select(g=>g.Last()).Where(v=>v.Goid==599 && v.Attributes.TryGetValue("IsEnemy",out var enemy)&&enemy.Length==1&&enemy[0]==1 &&
            v.Attributes.TryGetValue("Health",out var hp)&&hp.Length==2&&BitConverter.ToUInt16(hp)>0).Where(v=>{
                var p=Position(v);double dx=p[0]-center[0],dy=p[1]-center[1],dz=p[2]-center[2];return Math.Abs(dy)<150 && dx*dx+dy*dy+dz*dz<=600*600;
            }).Select(v=>v.ViewId).Order().ToArray();
    }
    public static byte[] InitialNpcCsv(byte[] original)
    {
        string text=System.Text.Encoding.Latin1.GetString(original);var rows=text.Split('\n');int winter=0,train=0;
        if(!rows[0].StartsWith("district_id;district;handle;level;hpM;hpC;",StringComparison.Ordinal))throw new InvalidDataException("Unknown test NPC header");
        for(int i=1;i<rows.Length;i++)
        {
            var cells=rows[i].Split(';');if(cells.Length<13)continue;
            if(cells[2]=="WinterFrost")
            {if(cells[0]!="1"||cells[4]!="800"||cells[5]!="800"||cells[7]!="24499"||cells[8]!="495"||cells[9]!="-1498")throw new InvalidDataException("Unexpected WinterFrost baseline");cells[4]=cells[5]="10000";winter++;}
            else if(cells[2]=="Trainman")
            {if(cells[0]!="1"||cells[4]!="500"||cells[5]!="500"||cells[7]!="24499"||cells[8]!="495"||cells[9]!="-1498")throw new InvalidDataException("Unexpected Trainman baseline");cells[9]="-1198";train++;}
            else continue;
            rows[i]=string.Join(';',cells);
        }
        if(winter!=1||train!=1)throw new InvalidDataException("Need exact two named test NPC rows");
        return System.Text.Encoding.Latin1.GetBytes(string.Join('\n',rows));
    }
    public static string? PrepareInitialNpcs(string sourceOutput,string workingDirectory,string database,IReadOnlyDictionary<string,string>? environment)
    {
        if(environment==null||!environment.TryGetValue(InitialNpcsEnvironment,out string? setting)||string.IsNullOrEmpty(setting)||setting=="0")return null;
        if(setting!="1" || !environment.TryGetValue("MXO_DEV_COMMANDS",out var dev)||dev!="1" ||
            !environment.TryGetValue("MXO_MOBS",out var mobs)||mobs!="test" || !database.StartsWith("mxotest_",StringComparison.Ordinal)||database.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='_'))
            throw new InvalidOperationException("Engage Foes initial NPC fixture requires strict marker/dev/test and an actual owned mxotest schema");
        string source=Path.Combine(sourceOutput,"data","mob_just_some_and_winter.csv");byte[] original=File.ReadAllBytes(source),modified=InitialNpcCsv(original);
        string root=Path.GetFullPath(workingDirectory)+Path.DirectorySeparatorChar;string target=Path.GetFullPath(Path.Combine(workingDirectory,"data","mob_just_some_and_winter.csv"));
        if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase)||File.Exists(target)||string.Equals(Path.GetFullPath(source),target,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe private NPC fixture target");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);using(var stream=new FileStream(target,FileMode.CreateNew,FileAccess.Write))stream.Write(modified);
        string proof=Path.Combine(workingDirectory,"engage-foes-initial-npcs.json");
        File.WriteAllText(proof,System.Text.Json.JsonSerializer.Serialize(new{Source=source,SourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)),PrivateCsv=target,
            PrivateSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(modified)),Database=database,Changes=new[]{"WinterFrost.hpM800->10000","WinterFrost.hpC800->10000","Trainman.z-1498->-1198"},
            Primary=new[]{PrimaryX,PrimaryY,PrimaryZ},PrimaryHealth=500,Secondary=new[]{SecondaryX,SecondaryY,SecondaryZ},SecondaryHealth=10000,RuntimeMutation=false},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        return proof;
    }

}
