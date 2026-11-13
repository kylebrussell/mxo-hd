using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ZionPrimaChain(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("ZionChain"){Level=50,X=17000,Z=2300,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,Organization=1});
        await context.Database.ExecuteAsync("CREATE TABLE IF NOT EXISTS char_missions (charId INT UNSIGNED NOT NULL,missionId INT NOT NULL,completions INT NOT NULL DEFAULT 1,PRIMARY KEY(charId,missionId))");
        await context.Database.ExecuteAsync(ZionChainFixture.SeedPrerequisites(p.CharId));
        await p.EnterWorldAsync();
        foreach(int id in ZionChainFixture.Missions)
        {
            XElement mission=Directory.GetFiles(Path.Combine(context.RepositoryRoot,"data","missions"),"zion_arc_*.xml")
                .Select(XElement.Load).Single(m=>(int)m.Attribute("id")! == id);
            int logStart=context.Server.DebugLog().Length;int ends=p.Received("80 a2 00").Count;
            p.SkipReceived();p.SendRpc("80 94 05 00 01");await p.WaitForTextAsync((string)mission.Attribute("title")!);
            await p.WaitForRpcAsync("80 97 05 00");p.SendRpc("80 98 06 00 00 00 01 02 00");await p.WaitForRpcAsync("80 99");
            p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
            int phase=0;
            foreach(var area in mission.Elements("phase"))
            {
                int rpcStart=p.World.RpcSnapshot().Count;int viewsStart=p.World.CreationSnapshot().Count;
                await EnterMissionArea(context,p);
                foreach(var goal in area.Elements("objective"))
                {
                    string type=(string)goal.Attribute("type")!;int number=(int)goal.Attribute("goal")!;
                    bool Done()=>p.World.RpcSnapshot().Skip(rpcStart).Any(r=>BytePattern.Parse($"80 a0 {number:x2} 06 00 01").IsMatch(r.Bytes));
                    if(type=="Protect" || Done())continue;
                    Console.WriteLine($"  Zion{id} phase{phase+1} goal{number} {type}");
                    switch(type)
                    {
                        case "Talk":case "Get":case "Give":case "Defeat":
                        {
                            var npc=mission.Elements("npc").Single(n=>(int)n.Attribute("id")! == int.Parse((string)goal.Attribute("npc")!));
                            var view=await ZionNpc(context,p,npc);
                            if(type=="Defeat") await DefeatMissionNpc(p,view);
                            else {string action=type=="Talk"?"09":type=="Get"?"0d":"0e";p.SendRpc($"80 c7 {MissionNpcId(view)} {action} 00");}
                            break;
                        }
                        case "Search":case "Use":
                        {
                            int objectId=(int)goal.Attribute("object")!;var view=await ZionObject(context,p,objectId);
                            int loot=p.Received("81 19").Count;
                            bool computer=hds.MissionStaging.IsComputer(view.Goid);
                            string label=type=="Use"?"Hack":computer?"Take":"Open";
                            byte action=hds.ObjectStates.UseNumber(view.Goid,label)
                                ?? throw new Exception($"No native {label} usable for object {view.Goid}");
                            if(computer)
                            {
                                Check.That(view.Attributes.TryGetValue("CurrentState",out var initialState),"computer state is visible");
                                await p.WaitUntilAsync(()=>
                                {
                                    // Creation snapshots are immutable: goal gates and Hack/Take transitions arrive as updates.
                                    var updates=ViewUpdateValues(p,view.ViewId,"01 08");
                                    uint current=updates.Count>0?updates[^1]:BitConverter.ToUInt32(initialState!);
                                    return hds.ObjectStates.Offered(view.Goid,current).Contains(action);
                                },$"native {label} usable is enabled by current computer state",TimeSpan.FromSeconds(10));
                            }
                            Console.WriteLine($"    native {label} use {action} on {view.Goid}");
                            p.SendRpc($"80 c7 {MissionNpcId(view)} {action:x2} 00");
                            await p.WaitUntilAsync(()=>Done() || p.Received("81 19").Count>loot,"object action or container loot",TimeSpan.FromSeconds(10));
                            if(!Done())p.SendRpc("81 1f");
                            break;
                        }
                        case "Escort":
                        {
                            foreach(string key in ((string)goal.Attribute("npc")!).Split(','))
                            {
                                var npc=mission.Elements("npc").Single(n=>(string)n.Attribute("id")! == key);
                                if((int?)npc.Attribute("at")==phase+1) {await ZionNpc(context,p,npc);await Task.Delay(800);}
                            }
                            string destination=(string?)goal.Attribute("to")??"exit";
                            if(destination=="exit")await LeaveMissionArea(p);
                            else if(destination=="hardline")await ZionUpload(p);
                            else if(destination.StartsWith("npc:"))await ZionNpc(context,p,mission.Elements("npc").Single(n=>(string)n.Attribute("id")! == destination[4..]));
                            else throw new Exception("Unhandled escort destination "+destination);
                            break;
                        }
                        case "Upload":await ZionUpload(p);break;
                        case "DefeatAll":
                        {
                            string enemy=(string?)area.Attribute("enemy")=="Machines"?"Machine Program":"Exile Soldier";
                            int count=(int)area.Attribute("enemies")!;
                            await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(viewsStart).Count(v=>v.Name==enemy)>=count,"all authored wave views",TimeSpan.FromSeconds(15));
                            var enemies=p.World.CreationSnapshot().Skip(viewsStart).Where(v=>v.Name==enemy).TakeLast(count).ToArray();
                            foreach(var target in enemies)await DefeatMissionNpc(p,target);
                            break;
                        }
                        default:throw new Exception("Unhandled Zion chain goal "+type);
                    }
                    await p.WaitUntilAsync(Done,$"Zion{id} phase{phase+1} actual goal{number} done",TimeSpan.FromSeconds(20));
                }
                string Log()=>context.Server.DebugLog()[logStart..];
                await p.WaitUntilAsync(()=>Log().Contains($"[MISSION] Phase {phase} goals done"),"phase goals complete",TimeSpan.FromSeconds(10));
                if(Log().Contains($"[MISSION] Phase {phase} goals done; waiting"))await LeaveMissionArea(p);
                phase++;
                if(phase<mission.Elements("phase").Count())
                    await p.WaitUntilAsync(()=>Regex.Matches(Log(),@"\[MISSION\] Phase \d+ at a").Count>phase,"next phase staged",TimeSpan.FromSeconds(15));
                else await p.WaitUntilAsync(()=>p.Received("80 a2 00").Count>ends,"mission completed over wire",TimeSpan.FromSeconds(15));
            }
            Check.Equal(1L,await context.Database.LongAsync($"SELECT completions FROM char_missions WHERE charId={p.CharId} AND missionId={id}"),"Chain completion recorded once");
            Check.Equal(0,p.World.Problems.Count,"Zion chain protocol problems");
        }
        Check.Equal(7L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={p.CharId} AND missionId BETWEEN 2304 AND 2310"),"All seven earned completions, not fixture grants");
    }

    private static async Task<ViewCreation> ZionNpc(ScenarioContext context,HeadlessPlayer p,XElement npc)
    {
        string name=(string)npc.Attribute("handle")!;await VisitMissionNpc(context,p,name);
        bool friendly=(string)npc.Attribute("type")! == "FRIENDLY";
        await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Any(v=>v.Name==name && (v.Attributes.ContainsKey("TalkDefaultable")==friendly)),name+" side-specific view",TimeSpan.FromSeconds(10));
        return p.World.CreationSnapshot().Last(v=>v.Name==name && (v.Attributes.ContainsKey("TalkDefaultable")==friendly));
    }
    private static Task<ViewCreation> ZionObject(ScenarioContext context,HeadlessPlayer p,int id)=>ZionObject(context.Server,p,id);

    private static async Task<ViewCreation> ZionObject(ServerInstance server,HeadlessPlayer p,int id,int creationStart=0)
    {
        string pattern=@"\[MISSION\] Object "+id+@" \([^)]+\) is a (\d+) at ([^,\r\n]+),([^,\r\n]+),([^\r\n]+)";
        var matches=Regex.Matches(server.DebugLog(),pattern);Check.That(matches.Count>0,"Object placement "+id);
        var match=matches.Last();double D(int i)=>double.Parse(match.Groups[i].Value,CultureInfo.InvariantCulture);
        ushort goid=ushort.Parse(match.Groups[1].Value);double x=D(2),y=D(3),z=D(4);
        await MoveForMissionTest(p,x+100,y,z);
        bool At(ViewCreation v)=>v.Goid==goid && v.Attributes.TryGetValue("Position",out var pos) && Math.Abs(BitConverter.ToDouble(pos)-x)<1 && Math.Abs(BitConverter.ToDouble(pos,16)-z)<1;
        await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(creationStart).Any(At),"correct placed object view",TimeSpan.FromSeconds(10));
        return p.World.CreationSnapshot().Skip(creationStart).Last(At);
    }
    private static async Task ZionUpload(HeadlessPlayer p)
    {
        await MoveForMissionTest(p,17000,495,2200);await Task.Delay(1200);
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(592).Any(v=>Math.Abs(BitConverter.ToDouble(v.Attributes["Position"])-17000)<10),"Mara hardline booth",TimeSpan.FromSeconds(10));
        var booth=p.ViewsOfGoid(592).Last(v=>Math.Abs(BitConverter.ToDouble(v.Attributes["Position"])-17000)<10);
        p.SendRpc($"80 c7 {MissionNpcId(booth)} 04 00");await p.WaitForRpcAsync("81 18");
    }
}
