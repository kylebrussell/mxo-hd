using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;
public sealed class ReverseBulldogMode:NativeQaMode
{
    public override string Name=>"reverse-bulldog-controls";
    private static string Load(uint character,IEnumerable<ushort> abilities)=>"DELETE FROM char_abilities WHERE char_id="+character+"; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES"+
        string.Join(",",abilities.Select((a,i)=>$"({character},{i},{0x80000000u|((uint)a<<10)},50,1,'ReverseBulldog QA')"));
    public override NativeQaModePlan Plan{get;}=new(ReverseBulldogFixture.Abilities,17020,2693,Array.Empty<string>(),
        new[]{"UPDATE characters SET alignment=1,pvpflag=1,rotation=64,healthC=10000,healthM=10000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        new[]{"UPDATE characters SET level=50,exp="+hds.PlayerProgression.ExperienceForLevel(50)+",x=CASE WHEN charId=6114 THEN 17220 ELSE 17020 END,y=495,z=2693,rotation=64,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=10000,healthM=10000,innerStrC=2000,innerStrM=2000 WHERE charId IN(6111,6114)",
            Load(6111,ReverseBulldogFixture.Abilities),Load(6114,new ushort[]{2,17})},
        extraQaMetadata:new(){["Ability"]=388,["RawStunSeconds"]=8,["ExplicitTimerSeconds"]=16,["StunMarkerSeconds"]=.01,["TimerMarkerSeconds"]=.1,["InnerStrengthCost"]=60,["ReuseSeconds"]=60,["Target"]="QAEnemy6114",["NegativeHelper"]="QAFriend6111 normal Sneak/388 after CSR's actual 16-second timer",["NoRandomOverrides"]=true},
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="0"},
        readyMessages:new[]{"Reverse Bull Dog388: CSR110 starts2m behind QAEnemy10000HP, both facing+X. Use ordinary Actions/Spy/Sneak, target actual QAEnemy, then Actions/Master Assassin/Reverse Bull Dog in Spy Style. Expect60IS/60s reuse, Stun102 input8 then734/681 timer16. QAFriend is a disclosed pre-sneaked headless helper: after YOUR actual16timer it sends one ordinary388 negative. REQUIRE reverse-bulldog-negative.json Passed=true and retain current state/timer IDs/natural OFF. No HP/aggro/state/RNG injection. See docs/ABILITY-REVERSE-BULLDOG-CONTROLS.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach(string name in new[]{"QAEnemy","QAFriend"})
        {var p=await HeadlessPlayer.LoginAsync(context.Address,name,name);context.Helpers.Add(name,p);await p.EnterWorldAsync();}
        var target=context.Helpers["QAEnemy"];var helper=context.Helpers["QAFriend"];
        await helper.WaitUntilAsync(()=>ReverseBulldogFixture.Current(helper.World.CreationSnapshot(),"QAEnemy")!=null,"actual helper target view",TimeSpan.FromSeconds(15));
        var targetView=ReverseBulldogFixture.Current(helper.World.CreationSnapshot(),"QAEnemy")!;
        var targetPose=ReverseBulldogFixture.Position(targetView);var helperSelf=helper.World.CreationSnapshot().Last(v=>v.ViewId==2);var helperPose=ReverseBulldogFixture.Position(helperSelf);
        Check.That(targetPose.SequenceEqual(new double[]{17220,495,2693})&&helperPose.SequenceEqual(new double[]{17020,495,2693}),"Actual visible rear setup2m apart, both helper logins entered");
        Check.Equal((ushort)10000,BitConverter.ToUInt16(targetView.Attributes["Health"]),"Actual healthy target before native input");
        await ReverseBulldogFixture.SneakAsync(helper);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"reverse-bulldog-ready.json"),JsonSerializer.Serialize(new{Target=targetView,TargetPose=targetPose,HelperPose=helperPose,TargetHealth=10000,Helper=helper.CharId,TargetCharacter=target.CharId,PlannedNativePose=new[]{17020,495,2693},Preparation="Both helpers actual login/entry; QAFriend ordinary Sneak; no native action or state injection"}));
        _=NegativeAfterNative(context,helper,target);
        return Array.Empty<byte>();
    }
    private static async Task NegativeAfterNative(NativeQaBootstrapContext context,HeadlessPlayer helper,HeadlessPlayer target)
    {
        Exception? failure=null;bool passed=false;int cursor=0;byte[]? sent=null;
        try
        {
            await target.WaitUntilAsync(()=>ReverseBulldogFixture.Mods(target).Any(m=>!m.Off&&m.Parent==734&&m.Target==681&&m.Seconds==16),"CSR's actual388 timer16 before normal negative",TimeSpan.FromMinutes(15));
            var timer=ReverseBulldogFixture.Mods(target).Last(m=>!m.Off&&m.Parent==734&&m.Target==681&&m.Seconds==16);
            var stun=ReverseBulldogFixture.Mods(target).Last(m=>!m.Off&&m.Parent==388&&m.Target==102);cursor=target.World.RpcSnapshot().Count;
            var view=ReverseBulldogFixture.Current(helper.World.CreationSnapshot(),"QAEnemy")!;sent=ReverseBulldogFixture.Cast(helper,388,view);
            await target.WaitUntilAsync(()=>ReverseBulldogFixture.Mods(target,cursor).Any(m=>m.Off&&m.Parent==388&&m.Target==710&&m.Kind==9),"helper's actual terminal marker OFF",TimeSpan.FromSeconds(10));
            Check.That(!ReverseBulldogFixture.Mods(target,cursor).Any(m=>!m.Off&&(m.Parent==388&&m.Target==102||m.Parent==734&&m.Target==681)),"existing timer refuses both helper callbacks");
            Check.That(!ReverseBulldogFixture.Off(target,stun)&&!ReverseBulldogFixture.Off(target,timer),"existing state/timer identities retained");passed=true;
            Console.WriteLine("Reverse Bull Dog normal helper negative PASS; record exact natural state/timer OFF in native one-pass.");
        }
        catch(Exception e){failure=e;Console.Error.WriteLine("Reverse Bull Dog helper negative FAILED: "+e);}
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"reverse-bulldog-negative.json"),JsonSerializer.Serialize(new{Passed=passed,Failure=failure?.ToString(),Cursor=cursor,
                Preparation="QAFriend ordinary Sneak then full388 request only after actual CSR-created734/681 timer16; no state/health/RNG override",Sent=sent==null?null:Convert.ToHexString(sent),
                TargetRpcs=target.World.RpcSnapshot(),HelperRpcs=helper.World.RpcSnapshot(),TargetViews=target.World.ViewSnapshot().Select(Convert.ToHexString),HelperViews=helper.World.ViewSnapshot().Select(Convert.ToHexString),NativePositiveUiProven=false},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
