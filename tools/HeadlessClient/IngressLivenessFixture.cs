using hds;
namespace HeadlessClient;
public static class IngressLivenessFixture
{
    public static ushort[] Abilities=>new ushort[]{2,17};
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_INGRESS_LIVENESS_QA"]="1",["MXO_PACKET_LOG"]="1",
        ["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"};
    public static async Task<HeadlessPlayer> CsrAsync(ScenarioContext context)
    {
        if(!context.Database.Name.StartsWith("mxotest_",StringComparison.Ordinal))throw new InvalidOperationException("Owned disposable database required");
        var template=await context.AddPlayerAsync(new CharacterSeed("IngressTemplate"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),
            X=23828,Y=495,Z=13984,CompletedMissions=new[]{986},OwnedLoadedAbilities=Abilities.Select(a=>(a,(ushort)50)).ToArray()});template.Dispose();
        await context.Database.ExecuteAsync($@"DELETE FROM characters WHERE charId=110;
DELETE FROM rsivalues WHERE charId=110;DELETE FROM inventory WHERE charId=110;DELETE FROM char_abilities WHERE char_id=110;DELETE FROM char_missions WHERE charId=110;
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
SELECT 990110,'CSR_IngressQA',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=(SELECT userId FROM characters WHERE charId={template.CharId});
CREATE TEMPORARY TABLE ingress_character AS SELECT * FROM characters WHERE charId={template.CharId};
UPDATE ingress_character SET charId=110,userId=990110,handle='CSR_Tester',is_online=0;INSERT INTO characters SELECT * FROM ingress_character;DROP TEMPORARY TABLE ingress_character;
CREATE TEMPORARY TABLE ingress_rsi AS SELECT * FROM rsivalues WHERE charId={template.CharId};UPDATE ingress_rsi SET charId=110;
INSERT INTO rsivalues SELECT * FROM ingress_rsi;DROP TEMPORARY TABLE ingress_rsi;
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) SELECT 110,slot,ability_id,level,is_loaded,ability_name FROM char_abilities WHERE char_id={template.CharId};
INSERT INTO char_missions(charId,missionId,completions) SELECT 110,missionId,completions FROM char_missions WHERE charId={template.CharId};");
        return await HeadlessPlayer.LoginAsync(context.Server.Address,"CSR_IngressQA","CSR_Tester");
    }
}
