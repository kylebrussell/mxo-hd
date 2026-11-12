using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public class AbilityResurrectionTests
{
    private static WorldClient Client(uint id, bool dead = false)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData(); c.playerData.setCharID(id); c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true); c.playerData.isDead = dead;
        c.playerInstance = new PlayerCharacter(); c.playerInstance.Level.setValue((byte)50);
        c.playerInstance.MaxHealth.setValue((ushort)777); c.playerInstance.Health.setValue((ushort)(dead ? 0 : 500));
        c.playerInstance.InnerStrengthMax.setValue((ushort)333); c.playerInstance.InnerStrengthAvailable.setValue((ushort)10);
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17020,495,2693));
        // Bot Alive has no socket timeout, but this test never registers a bot or sends packets.
        typeof(WorldClient).GetField("<IsBot>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(c,true);
        c.Alive = true;
        return c;
    }

    // Real7.6005 capture, built independently of the server offer serializer:54 bytes, no name,
    // ability at2, flags6/7 and uninitialised padding (ca at11).
    private static byte[] NativeBody(uint ability)
    {
        byte[] body=new byte[52];BitConverter.GetBytes(ability).CopyTo(body,0);body[9]=0xca;
        return body;
    }

    [Fact]
    public void OfferHasNativePrefixNonzeroOfferIdAndRelativeCasterName()
    {
        byte[] b = AbilityResurrection.BuildApproval(416,"Medic");
        Assert.Equal(62,b.Length); Assert.Equal(new byte[]{0x81,0x88},b[..2]);
        Assert.Equal(416u,BitConverter.ToUInt32(b,2)); Assert.NotEqual(0u,BitConverter.ToUInt32(b,8));
        Assert.NotEqual(BitConverter.ToUInt32(b,8),BitConverter.ToUInt32(AbilityResurrection.BuildApproval(416,"Medic"),8));
        Assert.Equal(54,BitConverter.ToUInt16(b,52)); Assert.Equal(6,BitConverter.ToUInt16(b,54));
        Assert.Equal("Medic\0",System.Text.Encoding.ASCII.GetString(b,56,6));
    }

    [Theory]
    [InlineData(0,0,true)] [InlineData(0,1,false)] [InlineData(1,0,false)]
    public void CapturedShortYesNoAndTimeoutConsumeOneOfferDespiteUninitialisedPadding(byte timeout,byte refusal,bool yes)
    {
        byte[] b=AbilityResurrection.BuildApproval(325,"Medic");
        var approval=new AbilityResurrection.Approval(b,60);var payload=NativeBody(325);
        payload[4]=timeout;payload[5]=refusal;payload[6]=0x7b;payload[28]=0x42;
        Assert.NotEqual(BitConverter.ToUInt32(b,8),BitConverter.ToUInt32(payload,6));
        Assert.True(approval.TryComplete(payload,59,out bool accepted)); Assert.Equal(yes,accepted);
        Assert.False(approval.TryComplete(payload,59,out _)); Assert.False(approval.Pending(59));
    }

    [Fact]
    public void WrongAbilityExpiredTruncatedAndInvalidFlagRepliesDoNotApprove()
    {
        byte[] b=AbilityResurrection.BuildApproval(416,"Medic");var p=NativeBody(416);
        var a=new AbilityResurrection.Approval(b,60);
        Assert.False(a.TryComplete(p[..^1],1,out _));
        Assert.False(a.TryComplete(b[2..],1,out _)); // offer-name echo is not the native reply format
        Assert.False(a.TryComplete(NativeBody(375),1,out _));
        p[4]=2; Assert.False(a.TryComplete(p,1,out _));p[4]=0;
        p[5]=2; Assert.False(a.TryComplete(p,1,out _));p[5]=0;
        Assert.True(a.TryComplete(p,2,out bool yes)); Assert.True(yes);
        Assert.False(a.TryComplete(p,3,out _));
        Assert.False(new AbilityResurrection.Approval(b,60).TryComplete(p,60,out _));
        Assert.False(new AbilityResurrection.Approval(AbilityResurrection.BuildApproval(325,"Medic"),60).TryComplete(p,1,out _));
    }

    [Theory]
    [InlineData(325,1,1)] [InlineData(326,777,333)] [InlineData(375,777,333)] [InlineData(416,777,333)]
    public void CallbacksRestoreScriptResourcesAndPreserveTheActualPvpChoice(ushort ability,ushort hp,ushort inner)
    {
        foreach(bool flagged in new[]{false,true})
        {
            var c=Client(2,true); c.playerData.pvpFlag=flagged; c.playerData.conditionStateFlags=uint.MaxValue;
            c.playerData.deathPenalty=.9f; int life=c.playerData.lifeGeneration;
            AbilityResurrection.RestoreVitals(c,ability);
            Assert.False(c.playerData.isDead); Assert.Equal(hp,PvpCombat.HealthOf(c));
            Assert.Equal(inner,AbilityHandler.CurrentInnerStrength(c)); Assert.Equal(flagged,c.playerData.pvpFlag);
            Assert.Equal(flagged?PvpFlag.PvpBit:0u,PvpFlag.FlagsOf(c)); Assert.Equal(.9f,c.playerData.deathPenalty);
            Assert.Equal(life+1,c.playerData.lifeGeneration);
        }
    }

    [Fact]
    public void CorpseGateChecksDeadFriendlyRangeAndWorldState()
    {
        var caster=Client(1);var target=Client(2,true);
        Assert.True(AbilityResurrection.CanTarget(caster,target,325));
        target.playerData.isDead=false;Assert.False(AbilityResurrection.CanTarget(caster,target,325));target.playerData.isDead=true;
        target.playerData.duelDeath=true;Assert.False(AbilityResurrection.CanTarget(caster,target,325));target.playerData.duelDeath=false;
        target.playerData.respawnPending=true;Assert.False(AbilityResurrection.CanTarget(caster,target,325));target.playerData.respawnPending=false;
        target.playerData.worldChangePending=true;Assert.False(AbilityResurrection.CanTarget(caster,target,325));target.playerData.worldChangePending=false;
        target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17520,495,2693));
        Assert.False(AbilityResurrection.CanTarget(caster,target,325)); Assert.True(AbilityResurrection.CanTarget(caster,target,416));
        caster.playerInstance.OrganizationID.setValue((byte)1);target.playerInstance.OrganizationID.setValue((byte)2);target.playerData.pvpFlag=true;
        Assert.False(AbilityResurrection.CanTarget(caster,target,416));
        target.playerData.setOnWorld(false);Assert.False(AbilityResurrection.CanTarget(caster,target,416));
    }

    [Fact]
    public void LeavingTheWorldChangesItsGenerationEvenWhenTheNextWorldHasTheSameDistrict()
    {
        var c=Client(1);int generation=c.playerData.worldGeneration;
        c.playerData.setOnWorld(false);c.playerData.setOnWorld(true);
        Assert.Equal(generation+1,c.playerData.worldGeneration);
    }
}
