using hds;
namespace HeadlessClient;
public static class GhostStrikeFixture
{
    public const int CsrX=24800,FloorY=495,CsrZ=-1498,EnemyX=25200,FriendZ=-1098;
    // Literal stored type39 prerequisite closure of175/365;54 memory (+ordinary17) fits level50 capacity100.
    //1116 is a hidden grant from175, not a separately archived/loaded player action.
    public static ushort[] Abilities=>new ushort[]{2,17,113,130,185,143,1112,151,121,152,1125,123,155,120,292,142,131,175,291,1128,290,363,1130,365};
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",
        ["MXO_GHOST_STRIKE_QA"]="1",["MXO_GHOST_STRIKE_QA_ROLL"]="",["MXO_PACKET_LOG"]="1"};
    public static CharacterSeed Seed(string name,bool enemy=false)=>new(name){X=enemy?EnemyX:CsrX,Y=FloorY,Z=CsrZ,Level=50,
        Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=enemy?2:1,Health=5000,MaxHealth=5000,
        InnerStrength=2000,MaxInnerStrength=2000,Items=new[]{((int)GhostStrikeInvisibilityTool.Slot,GhostStrikeInvisibilityTool.Goid,1)},OwnedLoadedAbilities=Abilities.Select(a=>(a,(ushort)50)).ToArray()};
    // Local disposable schema only. Keep the control's actual CSR110 restriction identical to NativeQa.
    public static async Task<HeadlessPlayer> CsrAsync(ScenarioContext context)
    {
        if(!context.Database.Name.StartsWith(ThrowawayDatabase.Prefix,StringComparison.Ordinal))throw new InvalidOperationException("Owned schema required");
        var template=await context.AddPlayerAsync(Seed("GhostTemplate"));template.Dispose();
        await context.Database.ExecuteAsync($@"DELETE FROM characters WHERE charId=110;
DELETE FROM rsivalues WHERE charId=110;DELETE FROM inventory WHERE charId=110;DELETE FROM char_abilities WHERE char_id=110;DELETE FROM char_missions WHERE charId=110;
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
SELECT 990110,'CSR_GhostQA',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=(SELECT userId FROM characters WHERE charId={template.CharId});
CREATE TEMPORARY TABLE ghost_character AS SELECT * FROM characters WHERE charId={template.CharId};
UPDATE ghost_character SET charId=110,userId=990110,handle='CSR_Tester',is_online=0;
INSERT INTO characters SELECT * FROM ghost_character; DROP TEMPORARY TABLE ghost_character;
CREATE TEMPORARY TABLE ghost_rsi AS SELECT * FROM rsivalues WHERE charId={template.CharId};UPDATE ghost_rsi SET charId=110;
INSERT INTO rsivalues SELECT * FROM ghost_rsi;DROP TEMPORARY TABLE ghost_rsi;
INSERT INTO inventory(charId,goid,slot,count,purity) SELECT 110,goid,slot,count,purity FROM inventory WHERE charId={template.CharId};
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
SELECT 110,slot,ability_id,level,is_loaded,ability_name FROM char_abilities WHERE char_id={template.CharId};
");
        return await HeadlessPlayer.LoginAsync(context.Server.Address,"CSR_GhostQA","CSR_Tester");
    }
}
