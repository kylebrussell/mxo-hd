using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;

public sealed class EngageFoesMode:NativeQaMode
{
    public override string Name=>"engage-foes-taunt";
    public override NativeQaModePlan Plan{get;}=new(EngageFoesFixture.Abilities,25300,-1498,Array.Empty<string>(),
        new[]{"UPDATE characters SET alignment=1,pvpflag=0,healthC=10000,healthM=10000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        new[]{"UPDATE characters SET x=24699,y=495,z=-1498,healthC=10000,healthM=10000,innerStrC=2000,innerStrM=2000 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES(6111,0,2147485696,50,1,'Awakened'),(6111,1,2147501056,50,1,'Spy Style')"},
        DevCommands:true,extraQaMetadata:new(){["EngageFoesAbility"]=610,["Blocker"]="QAFriend6111",["CastSeconds"]=2,["ReuseSeconds"]=30,["InnerStrengthCost"]=50,
            ["TargetRadiusMetres"]=6,["SelectionMetres"]=16,["ExistingEnragedState"]=93,["ExistingEnragedInputSeconds"]=6,["RandomControls"]=false,["DirectObjectRegistrationProven"]=false},
        readyMessages:new[]{"Engage Foes: QAFriend uses ordinary Block/interlock with full-HP WinterFrost. Select actual WinterFrost and use Actions→Coder→Utility Technician→Engage Foes in Coder Style. Expect50IS/2s/30s and NPC turn attribution to CSR110. Target-centred6m, not around caster. No RNG override or NPC HP/aggro injection. See docs/ABILITY-ENGAGE-FOES.md."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var a=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),"QAFriend","QAFriend");context.Helpers.Add("QAFriend",a);
        await a.EnterWorldAsync();var npc=await EngageFoesFixture.BlockNpcAsync(a);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"engage-foes-ready.json"),JsonSerializer.Serialize(new{Blocker=a.CharId,Npc=npc,
            Preparation="Ordinary Block/40 plus read-only current HP status, no direct effect/aggro/HP mutation",Rpcs=a.World.RpcSnapshot(),Views=a.World.ViewSnapshot().Select(Convert.ToHexString)}));
        return Array.Empty<byte>();
    }
}
