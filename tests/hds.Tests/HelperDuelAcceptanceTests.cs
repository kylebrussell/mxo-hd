using HeadlessClient;
using hds;
namespace hds.Tests;
public class HelperDuelAcceptanceTests
{
    [Fact] public void ActualNativeFailedApprovalContainsTheChallengeThatNeedsSevenByteResponse()
    {
        byte[] challenge=Convert.FromHexString("5104000200");var reply=HelperDuelAcceptance.Reply(challenge,0x00020004);
        Assert.Equal(Convert.FromHexString("52040002000100"),reply);
        Assert.True(DuelPackets.TryParseResponse(reply[1..],out uint handle,out bool accepts,out byte reason));Assert.Equal(0x00020004u,handle);Assert.True(accepts);Assert.Equal(0,reason);
    }
    [Theory][InlineData("")][InlineData("51")][InlineData("51040002")][InlineData("510400020000")][InlineData("5504000200")][InlineData("818804000200")][InlineData("5100000000")][InlineData("5104000300")]
    public void MalformedWrongKindZeroAndDifferentSpawnChallengesCannotBeAccepted(string hex)=>Assert.Throws<ArgumentException>(()=>HelperDuelAcceptance.Reply(Convert.FromHexString(hex),0x00020004));
    [Theory][InlineData(0u)][InlineData(0x00020002u)][InlineData(0x01020004u)]
    public void ExpectedHandleMustBeAVisibleRemoteActor(uint handle)=>Assert.Throws<ArgumentException>(()=>HelperDuelAcceptance.Reply(Convert.FromHexString("5104000200"),handle));
    [Fact] public void ProfileHasOnlyOrdinaryAbilitiesAndExplicitPveWithoutOtherPrivileges()
    {
        var p=NativeQa.NativeQaModes.Find("duel-helper").Plan;Assert.Equal(new ushort[]{2,17},p.Abilities);Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor("duel-helper");Assert.Equal("0",env["MXO_PVP_SERVER"]);Assert.Equal("0",env["MXO_DEV_COMMANDS"]);foreach(string key in new[]{"MXO_GM_CHARACTER_IDS","MXO_CALLBACK_QA_ROLL15","MXO_CALLBACK_QA_ROLL100"})Assert.Equal("",env[key]);
    }
}
