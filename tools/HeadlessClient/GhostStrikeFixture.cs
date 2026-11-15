using hds;
namespace HeadlessClient;
public static class GhostStrikeFixture
{
    public static ushort[] Abilities=>new ushort[]{2,17,1112,1116,365};
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",
        ["MXO_GHOST_STRIKE_QA"]="1",["MXO_GHOST_STRIKE_QA_ROLL"]="",["MXO_PACKET_LOG"]="1"};
    public static CharacterSeed Seed(string name,bool enemy=false)=>new(name){X=24800+(enemy?-200:0),Y=495,Z=-1498,Level=50,
        Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=enemy?2:1,Health=5000,MaxHealth=5000,
        InnerStrength=2000,MaxInnerStrength=2000,OwnedLoadedAbilities=Abilities.Select(a=>(a,(ushort)50)).ToArray()};
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
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
SELECT 110,slot,ability_id,level,is_loaded,ability_name FROM char_abilities WHERE char_id={template.CharId};
");
        return await HeadlessPlayer.LoginAsync(context.Server.Address,"CSR_GhostQA","CSR_Tester");
    }
}
