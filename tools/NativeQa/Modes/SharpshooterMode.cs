using HeadlessClient;
namespace NativeQa.Modes;
public sealed class SharpshooterMode:NativeQaMode
{
    public override string Name=>"sharpshooter-freefire";
    public override NativeQaModePlan Plan {get;}=new(SharpshooterFixture.Abilities,17000,2300,Array.Empty<string>(),
        new[]{"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id=2147635200"},
        new[]{PvpOpponentsFixture.NativeSql,
            "DELETE FROM inventory WHERE charId IN(110,6111,6114) AND slot=105;DELETE FROM inventory WHERE charId=110 AND slot=10;INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,537,10,1,0);"},
        DevCommands:false,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_PVP_SERVER"]="1",["MXO_PVP_MAX_SAFE_LEVEL"]="15",["MXO_PACKET_LOG"]="1"},
        extraQaMetadata:new(){["SharpshooterPolicy"]="Recovered gun-vs-fists math; OURS ordinary free-fire caller, short<=3m and distinct live opponent snapshots; interlock/140 unchanged",
            ["Native148InitiallyLoaded"]=false,["PistolBagSlot"]=10,["PistolGoid"]=537,["RandomOverrides"]=false,["HelpersUnarmedInventory"]=true},
        readyMessages:new[]{"OURS Sharpshooter148: equip owned Revolver1 from bag10 normally, Free Fire at unarmed QAEnemy near1.2m. With148off pistol modifier-5, normally loaded148 gives0, unload restores-5; parent stats unchanged. No fixed damage/hit promise: ordinary RNG is natural. Optional QAFriend melee QAEnemy makes two actual opponents: off-20/on0. Real native gun-target/far-range controls and checklist docs/ABILITY-SHARPSHOOTER.md. No140/interlock consumer claim."});
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach(string name in new[]{"QAEnemy","QAFriend"})
        {
            var p=await HeadlessPlayer.LoginAsync(context.Address,name,name);context.Helpers.Add(name,p);await PvpOpponentsFixture.EnterAsync(p);
        }
        return context.Helpers["QAEnemy"].World.ViewSnapshot().First();
    }
}
