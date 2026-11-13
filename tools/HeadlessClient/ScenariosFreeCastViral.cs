using System.Security.Cryptography;
using System.Text.Json;
namespace HeadlessClient;
public static partial class Scenarios
{
    // Only explicitly listed legacy scenarios opt in. Preserve their other outcome controls verbatim.
    private static IReadOnlyDictionary<string,string> FreeCastViralLandingEnvironment(IReadOnlyDictionary<string,string> original)=>new Dictionary<string,string>(original)
    {
        ["MXO_DEV_COMMANDS"]="1",["MXO_FREECAST_VIRAL_QA"]="1",
        ["MXO_FREECAST_VIRAL_QA_ATTACK15"]="32766",["MXO_FREECAST_VIRAL_QA_DEFENSE15"]="15636"
    };
    private static async Task PrepareFreeCastViralFixture(ScenarioContext context,HeadlessPlayer caster,params ushort[] abilities)
    {
        // Test-only SQL establishes ownership; the existing strict QA bridge runs the normal80ae load handler.
        foreach(ushort id in abilities)
        {
            int code=unchecked((int)(0x80000000u | (uint)id<<10));
            await context.Database.ExecuteAsync($"DELETE FROM char_abilities WHERE char_id={caster.CharId} AND ability_id={code}");
            int slot=500;while(await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={caster.CharId} AND slot={slot}")>0)slot++;
            Check.That(slot<0x208,"Owned active fixture ability fits an ordinary archive slot");
            await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({caster.CharId},{slot},{code},50,0)");
            int before=caster.Received($"80 b2 {Le(id)}").Count;
            caster.SendChat("?qaload "+id);
            await caster.WaitUntilAsync(()=>caster.Received($"80 b2 {Le(id)}").Count>before,"Owned fixture ability loaded through ordinary80ae: "+id,TimeSpan.FromSeconds(5));
        }
    }
    private static IReadOnlyDictionary<string,string> FreeCastViralEnvironment(bool hit)=>new Dictionary<string,string>(StillNpcs)
    {
        ["MXO_DEV_COMMANDS"]="1",["MXO_FREECAST_VIRAL_QA"]="1",["MXO_FREECAST_VIRAL_QA_ATTACK15"]=hit?"32766":"0",
        ["MXO_FREECAST_VIRAL_QA_DEFENSE15"]=hit?"0":"32766",["MXO_PVP_SERVER"]="1",
        ["MXO_CALLBACK_QA_ROLL15"]="",["MXO_CALLBACK_QA_ROLL100"]=""
    };
    private static async Task FreeCastViralWire(ScenarioContext context,bool hit)
    {
        var caster=await context.AddPlayerAsync(NearNpc("ViralCaster") with{Organization=1,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var victim=await context.AddPlayerAsync(NearNpc("ViralVictim",100) with{Organization=2,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000});
        var observer=await context.AddPlayerAsync(NearNpc("ViralObserver",200) with{Organization=1,Health=5000,MaxHealth=5000});
        foreach(var p in new[]{caster,victim,observer})
        {
            await LoadAbilitiesAsync(context,p,2,17,1006,33,57);
            await p.EnterWorldAsync();
        }
        caster.SendChat("?pvpflag on");await caster.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        victim.SendChat("?pvpflag on");await victim.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        await observer.WaitForViewNamedAsync(caster.Handle);await observer.WaitForViewNamedAsync(victim.Handle);
        string outcome=hit?"f1":"f2";
        string Pattern(ushort id)=>$"81 67 ?? ?? ?? ?? {outcome} 00 00 24 01 {Le((uint)id)}";
        var npc=await caster.WaitForViewNamedAsync("WinterFrost");await observer.WaitForViewNamedAsync("WinterFrost");
        int start=context.Server.DebugLog().Length;int isBefore=2000;
        Cast(caster,33,npc.ViewId);await caster.WaitForRpcAsync("80 ac 21 00");
        var npcLine=await caster.WaitForRpcAsync(Pattern(33),TimeSpan.FromSeconds(8));await observer.WaitForRpcAsync(Pattern(33));
        Check.That(context.Server.DebugLog()[start..].Contains("[FREECAST-VIRAL] ability=33 source=ViralCaster target=WinterFrost inputs=0/0"),"Normal80b9 NPC cast consumes modeled zero totals and explicit NPC neutral policy");
        Check.That(ViewUpdateValues(caster,2,"02 80 80 80 10").Any(v=>v<isBefore),"Normal cast IS cost remains paid for either outcome");
        if(hit)await caster.WaitUntilAsync(()=>context.Server.DebugLog()[start..].Contains("[ABILITY] 33 lands"),"NPC hit reaches ordinary damage executor",TimeSpan.FromSeconds(5));
        else
        {
            await Task.Delay(500);Check.That(!context.Server.DebugLog()[start..].Contains("[ABILITY] 33 lands"),"Deflection skips NPC landing effects");
            Check.Equal(BitConverter.ToUInt16(npc.Attributes["Health"]),ViewUpdateValues(caster,npc.ViewId,"04 80 80 80 c0").Last(),"Deflection effect carries unchanged NPC HP");
        }
        var enemyView=await caster.WaitForViewNamedAsync(victim.Handle);int healthBefore=OwnHealth(victim);start=context.Server.DebugLog().Length;
        Cast(caster,57,enemyView.ViewId);await caster.WaitForRpcAsync("80 ac 39 00");
        var playerLine=await caster.WaitForRpcAsync(Pattern(57),TimeSpan.FromSeconds(8));await victim.WaitForRpcAsync(Pattern(57));await observer.WaitForRpcAsync(Pattern(57));
        if(hit)await victim.WaitUntilAsync(()=>OwnHealth(victim)<healthBefore,"Legal PvP hit reaches ordinary damage executor",TimeSpan.FromSeconds(5));
        else {await Task.Delay(500);Check.Equal(healthBefore,OwnHealth(victim),"Deflection preserves PvP HP");Check.That(!context.Server.DebugLog()[start..].Contains("[PVP] ViralCaster's"),"No post-deflect PvP landing");}
        Check.Equal(2,context.Server.DebugLog().Split('\n').Count(l=>l.Contains("[FREECAST-VIRAL]") && l.Contains("source=ViralCaster")),"Exactly one contest per normal legal recipient");
        Check.Equal(0,new[]{caster,victim,observer}.Sum(p=>p.World.Problems.Count),"Free-cast result/effect framing");
        string stem=Path.Combine(Path.GetTempPath(),"mxo-freecast-viral-"+(hit?"hit":"deflect"));
        await File.WriteAllTextAsync(stem+"-success.log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
        await File.WriteAllTextAsync(stem+"-wire.json",JsonSerializer.Serialize(new{SourceAssembly=context.Server.SourceAssembly,SourceConfiguration=new DirectoryInfo(Path.GetDirectoryName(context.Server.SourceAssembly)!).Parent!.Name,SourceAssemblySha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),NpcResult=Convert.ToHexString(npcLine.Bytes),PlayerResult=Convert.ToHexString(playerLine.Bytes),ReceivedViewLists=caster.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),NativeRenderingProven=false},new JsonSerializerOptions{WriteIndented=true}));
    }
}
