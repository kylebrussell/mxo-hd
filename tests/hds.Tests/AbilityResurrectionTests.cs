using System.Reflection;
using System.Runtime.CompilerServices;
using HeadlessClient;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class AbilityResurrectionTests
{
    private static WorldClient Client(uint id, bool dead = false, bool capturePackets = false)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData(); c.playerData.setCharID(id); c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true); c.playerData.isDead = dead;
        c.playerInstance = new PlayerCharacter(); c.playerInstance.Level.setValue((byte)50);
        c.playerInstance.MaxHealth.setValue((ushort)777); c.playerInstance.Health.setValue((ushort)(dead ? 0 : 500));
        c.playerInstance.InnerStrengthMax.setValue((ushort)333); c.playerInstance.InnerStrengthAvailable.setValue((ushort)10);
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17020,495,2693));
        if (capturePackets)
        {
            c.messageQueue = new();
            c.viewMan = new();
            c.playerData.isBot = true; // This in-memory client has no persisted PvP flag.
            typeof(WorldClient).GetField("lastUsedTime", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c, TimeUtils.getUnixTimeUint32());
            typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c, long.MaxValue);
        }
        else
        {
            // The other tests need no wire queue and Bot Alive has no socket timeout.
            typeof(WorldClient).GetField("<IsBot>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(c, true);
        }
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
    [InlineData(325,1,1)] [InlineData(326,777,333)] [InlineData(375,777,333)] [InlineData(416,777,333)] [InlineData(438,777,333)]
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

    [Theory]
    [InlineData(0, 605, 1080)]
    [InlineData(104, 610, 1090)] // PR321: effective level 45
    [InlineData(144, 615, 1090)] // PR316: effective level 46
    public void RejuvenateRestoresLiveMaximaAfterDeathEffectRecovery(double elapsed, ushort health, ushort innerStrength)
    {
        var recipient = Client(6111, dead: true);
        recipient.playerInstance.MaxHealth.setValue((ushort)500);
        recipient.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        var loaded = new (ushort Ability, ushort Level)[] { (45, 50), (51, 50), (70, 50) };
        AbilityPassives.SetLoaded(recipient, loaded, newWorld: true);
        Assert.Equal(620, NumericalUtils.ByteArrayToUint16(recipient.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(1100, AbilityHandler.MaxInnerStrength(recipient));

        recipient.playerData.deathPenalty = DeathPenalty.Recover(DeathPenalty.AfterDeath(1), elapsed);
        recipient.playerData.deathPenaltyAt = DeathPenalty.Now;
        AbilityPassives.Refresh(recipient, present: false);
        Assert.Equal(health, NumericalUtils.ByteArrayToUint16(recipient.playerInstance.MaxHealth.getValue(), 1));
        Assert.Equal(innerStrength, AbilityHandler.MaxInnerStrength(recipient));

        AbilityResurrection.RestoreVitals(recipient, 416);
        Assert.Equal(health, PvpCombat.HealthOf(recipient));
        Assert.Equal(innerStrength, AbilityHandler.CurrentInnerStrength(recipient));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void RejuvenateNativeReplyRunsTheScriptCallbackOnlyAfterConsent(bool recipientFlagged, bool accepted)
    {
        const ushort rejuvenate = 416;
        var caster = Client(110, capturePackets: true);
        var recipient = Client(6111, dead: true, capturePackets: true);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        recipient.playerInstance.OrganizationID.setValue((byte)1);
        recipient.playerData.pvpFlag = recipientFlagged;
        recipient.playerData.deathPenalty = .9f;
        recipient.playerInstance.IsDead.setValue(true);
        int life = recipient.playerData.lifeGeneration;
        var priorClient = Store.currentClient;
        var priorClients = WorldServer.Clients;
        WorldServer.Clients = new() { ["Caster"] = caster, ["Recipient"] = recipient };
        try
        {
            Assert.True(AbilityResurrection.Request(caster, recipient, rejuvenate,
                caster.playerData.worldGeneration, caster.playerData.lifeGeneration,
                recipient.playerData.worldGeneration, life));
            var offer = Assert.Single(recipient.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()).content.AsSpan(1);
            Assert.True(offer.StartsWith(new byte[] { 0x81, 0x88, 0xa0, 0x01, 0x00, 0x00 }));
            Assert.True(recipient.playerData.isDead);
            Assert.False(caster.playerData.pvpFlag);

            Store.currentClient = recipient;
            var reply = NativeBody(rejuvenate);
            if (!accepted) reply[5] = 1;
            var rpc = new RpcManager();
            rpc.HandleRpc((int)RPCRequestHeader.CLIENT_ABILITY_APPROVAL, ref reply);
            rpc.HandleRpc((int)RPCRequestHeader.CLIENT_ABILITY_APPROVAL, ref reply);

            Assert.Equal(recipientFlagged, recipient.playerData.pvpFlag);
            Assert.Equal(accepted && recipientFlagged, caster.playerData.pvpFlag);
            Assert.Equal(.9f, recipient.playerData.deathPenalty);
            if (!accepted)
            {
                Assert.True(recipient.playerData.isDead);
                Assert.Equal(life, recipient.playerData.lifeGeneration);
                Assert.Equal(0, PvpCombat.HealthOf(recipient));
                Assert.Empty(recipient.messageQueue.ObjectMessagesQueue);
                Assert.Empty(caster.messageQueue.RPCMessagesQueue);
                return;
            }

            Assert.False(recipient.playerData.isDead);
            Assert.Equal(life + 1, recipient.playerData.lifeGeneration);
            Assert.Equal(777, PvpCombat.HealthOf(recipient));
            Assert.Equal(333, AbilityHandler.CurrentInnerStrength(recipient));
            var objectPackets = recipient.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>();
            var stand = BytePattern.Parse("02 00 01 28 ?? 40 00 3f a0 01");
            var aliveAndStand = Assert.Single(objectPackets, message => stand.Occurs(message.content)).content;
            Assert.True(BytePattern.Parse("02 00 02 80 80 80 d1 00 4d 01 09 03").Occurs(aliveAndStand));
            Assert.Equal(AbilityResurrection.RestoreEffect, NumericalUtils.ByteArrayToUint32(recipient.playerInstance.EffectID.getValue(), 1));
            Assert.Single(caster.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(), message =>
                BytePattern.Parse("81 67 ?? ?? ?? ?? 40 28 00 c6 01 a0 01 00 00").Occurs(message.content));
            Assert.Single(recipient.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(), message =>
                BytePattern.Parse("81 67 ?? ?? ?? ?? 3e 28 00 c6 00 a0 01 00 00").Occurs(message.content));
        }
        finally
        {
            CombatManager.RemoveClient(recipient);
            WorldServer.Clients = priorClients;
            Store.currentClient = priorClient;
        }
    }

    [Fact]
    public void CorpseGateChecksDeadFriendlyRangeAndWorldState()
    {
        var caster=Client(1);var target=Client(2,true);
        Assert.True(AbilityResurrection.CanTarget(caster,target,325));
        Assert.True(AbilityResurrection.Handles(438)); Assert.True(AbilityResurrection.CanTarget(caster,target,438));
        target.playerData.isDead=false;Assert.False(AbilityResurrection.CanTarget(caster,target,438));Assert.False(AbilityResurrection.CanTarget(caster,target,325));target.playerData.isDead=true;
        target.playerData.duelDeath=true;Assert.False(AbilityResurrection.CanTarget(caster,target,325));target.playerData.duelDeath=false;
        target.playerData.respawnPending=true;Assert.False(AbilityResurrection.CanTarget(caster,target,325));target.playerData.respawnPending=false;
        target.playerData.worldChangePending=true;Assert.False(AbilityResurrection.CanTarget(caster,target,325));target.playerData.worldChangePending=false;
        target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17520,495,2693));
        Assert.False(AbilityResurrection.CanTarget(caster,target,325)); Assert.True(AbilityResurrection.CanTarget(caster,target,416));
        caster.playerInstance.OrganizationID.setValue((byte)1);target.playerInstance.OrganizationID.setValue((byte)2);target.playerData.pvpFlag=true;
        Assert.False(AbilityResurrection.CanTarget(caster,target,416));
        target.playerData.setOnWorld(false);Assert.False(AbilityResurrection.CanTarget(caster,target,416));
    }

    [Theory]
    [InlineData(null, "mxotest_qa", false)]
    [InlineData("true", "mxotest_qa", false)]
    [InlineData("1", "reality_hd", false)]
    [InlineData("1", "MXOTEST_qa", false)]
    [InlineData("1", "mxotest_", false)]
    [InlineData("1", "mxotest_qa", true)]
    public void QaReviveRequiresExactDevAndConfiguredThrowawayDatabase(string? dev, string database, bool allowed)
    {
        var caller=Client(110); var target=Client(111,true);
        Assert.Equal(allowed,AbilityResurrection.QaReviveAllowed(caller,target,dev!,database));
        Assert.True(target.playerData.isDead);
        Assert.Equal(0,PvpCombat.HealthOf(target));
    }

    [Fact]
    public void QaReviveAllowsDeadSelfAndRejectsProtectedCharactersAndWorldTransitions()
    {
        var c=Client(110,true);
        bool Allowed()=>AbilityResurrection.QaReviveAllowed(c,c,"1","mxotest_qa");
        Assert.True(Allowed());
        c.playerData.respawnPending=true; Assert.False(Allowed()); c.playerData.respawnPending=false;
        c.playerData.worldChangePending=true; Assert.False(Allowed()); c.playerData.worldChangePending=false;
        c.playerData.waitForRPCShutDown=true; Assert.False(Allowed()); c.playerData.waitForRPCShutDown=false;
        c.playerData.duelDeath=true; Assert.False(Allowed()); c.playerData.duelDeath=false;
        c.playerData.isDead=false; Assert.False(Allowed()); c.playerData.isDead=true;
        c.playerData.setDistrictId((uint)MxOLocations.LA); Assert.False(Allowed());
        c.playerData.setDistrictId(2); // Different district from another caller.
        Assert.False(AbilityResurrection.QaReviveAllowed(Client(111),c,"1","mxotest_qa"));
        foreach(uint id in new uint[]{0,2}) {
            c.playerData.setCharID(id); Assert.False(Allowed());
        }
    }

    [Fact]
    public void LeavingTheWorldChangesItsGenerationEvenWhenTheNextWorldHasTheSameDistrict()
    {
        var c=Client(1);int generation=c.playerData.worldGeneration;
        c.playerData.setOnWorld(false);c.playerData.setOnWorld(true);
        Assert.Equal(generation+1,c.playerData.worldGeneration);
    }
}
