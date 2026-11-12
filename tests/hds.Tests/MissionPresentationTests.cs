using hds;
namespace hds.Tests;
public class MissionPresentationTests
{
    [Fact]
    public void ExplicitOursChatFallbackPreservesBothDialogFlagsAndInteractiveFriendlySilence()
    {
        foreach(byte flags in new byte[]{1,5})
        {
            byte[] dialog=ServerPackets.BuildDialog(0x2800121e,0,0,flags,0,"Boyce","Resolved story text");
            Assert.Equal(flags,dialog[18]);
            Assert.Equal("Boyce: Resolved story text",MissionPresentation.ChatFallback(true,false,"Boyce","Resolved story text"));
        }
        Assert.Null(MissionPresentation.ChatFallback(false,false,"Campbell","Friendly"));
        Assert.Null(MissionPresentation.ChatFallback(true,true,"Hostile","Interactive"));
        Assert.True(MissionPresentation.QaAllowed("1","mxotest_m2",110,true,false));
        Assert.False(MissionPresentation.QaAllowed("0","mxotest_m2",110,true,false));
        Assert.False(MissionPresentation.QaAllowed("1","reality_hd",110,true,false));
        Assert.False(MissionPresentation.QaAllowed("1","mxotest_m2",2,true,false));
        Assert.False(MissionPresentation.QaAllowed("1","mxotest_m2",110,false,false));
        Assert.False(MissionPresentation.QaAllowed("1","mxotest_m2",110,true,true));
    }

    [Fact]
    public void OnlyScriptedCorpsesAreIntroducedToNewViewersWithDeadAttributesAndNpcPose()
    {
        var mob=new Mob();mob.setName("Boyce");mob.setRsiHex("d9040058");mob.setHealthM(175);mob.setHealthC(0);
        mob.setXPos(58550);mob.setYPos(1095);mob.setZPos(12450);mob.setIsDead(true);
        Assert.False(DeathPresentation.CanIntroduceNpc(mob));
        mob.ScriptedCorpse=true;Assert.True(DeathPresentation.CanIntroduceNpc(mob));
        var creation=(AttributeClass3827)mob.getCreationData();
        Assert.Equal(new byte[]{1},creation.IsDead.getValue());
        Assert.Equal(new byte[]{0,0},creation.Health.getValue());
        Assert.Equal(StringUtils.hexStringToBytes("fd070000"),creation.CurrentState.getValue());
        var first=DeathPresentation.NpcCorpse(mob);var late=DeathPresentation.NpcCorpse(mob);
        Assert.Equal(StringUtils.hexStringToBytes("07011d0d00"),first[..5]);
        Assert.Equal(first[..5],late[..5]);Assert.NotEqual(first[5],late[5]);
        mob.ScriptedCorpse=false;mob.setIsDead(false);mob.setHealthC(175);
        Assert.True(DeathPresentation.CanIntroduceNpc(mob));
        Assert.Equal(new byte[]{0},((AttributeClass3827)mob.getCreationData()).IsDead.getValue());
    }
}
