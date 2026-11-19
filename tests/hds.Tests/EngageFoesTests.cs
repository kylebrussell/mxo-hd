using hds;
using hds.shared;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace hds.Tests;
[Collection("DelayedActions")]
public class EngageFoesTests
{
    private sealed class Fixture:IDisposable
    {
        internal readonly WorldClient Source;
        internal readonly Mob Mob=new();
        internal readonly ClientView View;
        internal Fixture()
        {
            Source=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
            Source.playerData=new ClientData();Source.playerInstance=new PlayerCharacter();Source.viewMan=new ViewManager();Source.messageQueue=new MessageQueue();
            typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Source,new WorldSendFence());
            typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Source,TimeUtils.getUnixTimeUint32());
            typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Source,long.MaxValue);
            Source.Alive=true;Source.playerData.setOnWorld(true);Source.playerData.setDistrictId(1);Source.playerData.setCharID(610);
            Source.playerInstance.Health.setValue((ushort)1000);Source.playerInstance.MaxHealth.setValue((ushort)1000);Source.playerInstance.Level.setValue((byte)50);
            Mob.setEntityId(6100001);Mob.setDistrict(1);Mob.setHealthC(1000);Mob.setHealthM(1000);Mob.setName("EngageUnit");Mob.setGoId(new byte[]{0x57,2});
            lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Add(Mob);
            View=Source.viewMan.addView(Mob.getEntityId(),599);View.viewCreated=true;View.spawnId=7;
            // Suppress retaliation side effects in these witness/consumer units; wire tests exercise actual combat.
            CombatManager.GetHandler(Source).isCombatRunning=true;
        }
        internal AbilityEngageFoes.Cast Cast()
        {
            var normal=AbilityCastInterrupts.Begin(Source,610,2);
            var cast=AbilityEngageFoes.Capture(Source,610,View.ViewID,normal);
            Assert.True(AbilityCastInterrupts.Complete(normal,()=>{}));Assert.True(cast.Current);return cast;
        }
        internal AbilityEngageFoes.Recipient Stamp(AbilityEngageFoes.Cast cast)=>AbilityEngageFoes.Before(cast,Source,Mob,View,610);
        public void Dispose()
        {
            lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Remove(Mob);
            // This fixture owns no live server, online-client table or combat session.
            var handlers=(Dictionary<WorldClient,CombatHandler>)typeof(CombatManager).GetField("Handlers",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            lock(handlers)handlers.Remove(Source);
        }
    }

    [Fact]public void Only610UsesItsStoredInitializerAndExistingEnrage()
    {
        Assert.True(AbilityEngageFoes.HasInitializer(610));Assert.False(AbilityEngageFoes.HasInitializer(647));Assert.False(AbilityEngageFoes.HasInitializer(611));
        var entry=AbilityCatalog.Find(610);Assert.Equal(3,entry.Number("Ability/CastDirectObjects"));Assert.Equal(6,entry.Number("Ability/RadiusOfEffect"));
        Assert.Equal(50,entry.Number("Ability/AbilityInnerStrengthCost"));Assert.Equal(2,entry.CastSeconds);Assert.Equal(30,entry.Number("Ability/RecastTime"));
        Assert.True(TempMods.TryGet(610,out var effect));var mod=Assert.Single(effect.Mods);Assert.Equal((ushort)93,mod.Target);Assert.Equal(6,mod.Seconds);
    }

    [Fact]
    public void TauntUsesTheCasterFourMetreBoundaryAndRejectsAnUnseenTarget()
    {
        using var f = new Fixture();
        f.Source.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 0));
        f.Mob.setXPos(400);
        f.Mob.setYPos(495);
        var outside = new Mob();
        outside.setEntityId(6100010);
        outside.setDistrict(1);
        outside.setXPos(401);
        outside.setYPos(495);
        outside.setHealthC(500);
        var outsideView = f.Source.viewMan.addView(outside.getEntityId(), 599);
        outsideView.viewCreated = true;
        lock (WorldServer.mobs.SyncRoot) WorldServer.mobs.Add(outside);
        try
        {
            Assert.Same(f.Mob, Assert.Single(CombatSpecials.CastTaunt(f.Source)));
            Assert.Same(f.Source, CombatSpecials.MostHated(f.Mob));
            Assert.Null(CombatSpecials.MostHated(outside));
            f.Mob.setXPos(401);
            outside.setXPos(399);
            f.Source.viewMan.removeViewByViewId(outsideView.ViewID);
            Assert.Empty(CombatSpecials.CastTaunt(f.Source));
        }
        finally { lock (WorldServer.mobs.SyncRoot) WorldServer.mobs.Remove(outside); }
    }
    [Fact]public void ActualLandedRecipientGetsOneOwnerAndNoDamage()
    {
        using var f=new Fixture();var cast=f.Cast();var stamp=f.Stamp(cast);Assert.NotNull(stamp);
        Assert.True(AbilityEngageFoes.After(stamp));Assert.Same(f.Source,CombatSpecials.MostHated(f.Mob));
        Assert.False(AbilityEngageFoes.After(stamp));Assert.False(AbilityEngageFoes.After(f.Stamp(cast)));Assert.Equal((ushort)1000,f.Mob.getHealthC());
    }
    [Fact]public void SecondaryRecipientHasAnIndependentDeathAndViewWitness()
    {
        using var f=new Fixture();var cast=f.Cast();var secondary=new Mob();secondary.setEntityId(6100002);secondary.setDistrict(1);secondary.setHealthC(500);secondary.setHealthM(500);
        lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Add(secondary);
        try
        {
            var view=f.Source.viewMan.addView(secondary.getEntityId(),599);view.viewCreated=true;view.spawnId=8;
            var stamp=AbilityEngageFoes.Before(cast,f.Source,secondary,view,610);Assert.NotNull(stamp);
            secondary.setIsDead(true);secondary.setIsDead(false);
            Assert.True(cast.Current);Assert.False(AbilityEngageFoes.After(stamp));Assert.Null(CombatSpecials.MostHated(secondary));
            stamp=AbilityEngageFoes.Before(cast,f.Source,secondary,view,610);view.spawnId++;
            Assert.True(cast.Current);Assert.False(AbilityEngageFoes.After(stamp));Assert.Null(CombatSpecials.MostHated(secondary));
            Assert.Null(AbilityEngageFoes.Before(cast,f.Source,null!,view,610)); //Player/no NPC can never get this consumer.
        }
        finally{lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Remove(secondary);}
    }
    [Fact]public void PendingPrimaryDeletionRejectsInitialLanding()
    {
        using var f=new Fixture();var cast=f.Cast();Assert.True(cast.Current);
        f.View.viewNeedsToBeDeleted=true;Assert.False(cast.Current);
    }
    [Fact]public void SuccessfullyLandedSecondaryDoesNotDependOnPrimarySurvival()
    {
        using var f=new Fixture();var cast=f.Cast();var secondary=new Mob();secondary.setEntityId(6100002);secondary.setDistrict(1);secondary.setHealthC(500);secondary.setHealthM(500);
        lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Add(secondary);
        try
        {
            var view=f.Source.viewMan.addView(secondary.getEntityId(),599);view.viewCreated=true;view.spawnId=8;
            var stamp=AbilityEngageFoes.Before(cast,f.Source,secondary,view,610);Assert.NotNull(stamp);
            f.Mob.setIsDead(true);f.View.viewNeedsToBeDeleted=true;
            Assert.False(cast.Current);Assert.NotNull(AbilityEngageFoes.Before(cast,f.Source,secondary,view,610));
            Assert.True(AbilityEngageFoes.After(stamp));Assert.Same(f.Source,CombatSpecials.MostHated(secondary));
        }
        finally{lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Remove(secondary);}
    }
    [Fact]public void PendingRecipientDeletionRejectsBeforeAfterAndRetainedOwner()
    {
        using var f=new Fixture();var cast=f.Cast();var stamp=f.Stamp(cast);
        f.View.viewNeedsToBeDeleted=true;Assert.Null(f.Stamp(cast));Assert.False(AbilityEngageFoes.After(stamp));
        f.View.viewNeedsToBeDeleted=false;Assert.True(AbilityEngageFoes.After(f.Stamp(cast)));Assert.Same(f.Source,CombatSpecials.MostHated(f.Mob));
        f.View.viewNeedsToBeDeleted=true;Assert.Null(CombatSpecials.MostHated(f.Mob));
    }
    [Fact]public void ActualShippedBuffFlagAndDefaultNpcAreaAreRetained()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"data","abilityIDs.csv")))root=Path.GetDirectoryName(root)!;
        var row=File.ReadLines(Path.Combine(root,"data","abilityIDs.csv")).Single(l=>l.StartsWith("610;",StringComparison.Ordinal)).Split(';');
        Assert.True(bool.Parse(row[3]));Assert.True(AbilityEngageFoes.HasInitializer(610)); //The normal-wire case proves actual610-only routing past this legacy flag.
        Assert.True(AbilityTargets.WithinArea(0,495,0,600,495,0,6));Assert.False(AbilityTargets.WithinArea(0,495,0,601,495,0,6));
        Assert.False(AbilityTargets.WithinArea(0,495,0,0,0,0,6));
        Assert.Equal(4,AbilityCatalog.Find(647).Number("Ability/RadiusOfEffect"));Assert.True(CombatSpecials.ActsOnCast(647));Assert.False(CombatSpecials.ActsOnCast(610));
    }
    [Fact]public void QaProfilesUseNormalLevel50XpExplicitLoadoutAndNoRandomControl()
    {
        var mode=NativeQa.NativeQaModes.Find("engage-foes-taunt");Assert.Equal(HeadlessClient.EngageFoesFixture.Abilities,mode.Plan.Abilities);
        Assert.False(mode.Plan.FixedCallbackRolls);Assert.False(mode.Plan.GmCharacter110);Assert.Contains("QAFriend6111",mode.Plan.ExtraQaMetadata["Blocker"].GetString());
        var source=HeadlessClient.EngageFoesFixture.Seed("EngageUnit");Assert.Equal(24699,source.X);Assert.Equal(source.X,mode.Plan.X);Assert.True(CombatHandler.InMeleeReach((float)(Math.Abs(source.X-24499)/100.0)));Assert.Equal("Trainman500/500",mode.Plan.ExtraQaMetadata["FullHpPrimary"].GetString());Assert.Equal(50,source.Level);Assert.Equal((int)PlayerProgression.ExperienceForLevel(50),source.Experience);
        Assert.Equal(mode.Plan.Abilities,source.OwnedLoadedAbilities!.Select(x=>x.Ability));
        var blocker=HeadlessClient.EngageFoesFixture.Seed("BlockUnit",true);Assert.DoesNotContain(blocker.OwnedLoadedAbilities!,x=>x.Ability==610);
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor(mode.Name);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL100"]);
    }
    [Theory][InlineData("world")][InlineData("life")][InlineData("pending")][InlineData("dead")][InlineData("off")][InlineData("owner")][InlineData("character")][InlineData("transport")]
    public void CasterChangesBetweenEffectsAndInitializerCannotAuthorizeTaunt(string change)
    {
        using var f=new Fixture();var cast=f.Cast();var stamp=f.Stamp(cast);
        switch(change)
        {
            case "world":f.Source.playerData.worldGeneration++;break;case "life":f.Source.playerData.lifeGeneration++;break;
            case "pending":f.Source.playerData.worldChangePending=true;break;case "dead":f.Source.playerData.isDead=true;break;
            case "off":f.Source.playerData.setOnWorld(false);break;case "owner":f.Source.playerData=new ClientData();break;
            case "character":f.Source.playerData.setCharID(611);break;case "transport":f.Source.TransportFence.PublishIncoming(true,()=>{});f.Source.TransportFence.Resume(f.Source.TransportFence.Capture());break;
        }
        Assert.False(AbilityEngageFoes.After(stamp));Assert.Null(CombatSpecials.MostHated(f.Mob));
    }
    [Theory][InlineData("spawn")][InlineData("view")][InlineData("dead")][InlineData("reuse")][InlineData("friendly")][InlineData("entity")][InlineData("removed")]
    public void ExactRecipientMustSurviveEffectCallbacks(string change)
    {
        using var f=new Fixture();var cast=f.Cast();var stamp=f.Stamp(cast);
        switch(change)
        {
            case "spawn":f.View.spawnId++;break;case "view":f.Source.viewMan.removeViewByViewId(f.View.ViewID);break;
            case "dead":f.Mob.setIsDead(true);break;case "reuse":f.Mob.setIsDead(true);f.Mob.setIsDead(false);break;
            case "friendly":f.Mob.isFriendly=true;break;case "entity":f.View.entityId++;break;
            case "removed":lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Remove(f.Mob);break;
        }
        Assert.False(AbilityEngageFoes.After(stamp));Assert.Null(CombatSpecials.MostHated(f.Mob));
    }
    [Fact]public void UncompletedOrCancelledCastHasNoInitializer()
    {
        using var f=new Fixture();var normal=AbilityCastInterrupts.Begin(f.Source,610,2);var cast=AbilityEngageFoes.Capture(f.Source,610,f.View.ViewID,normal);
        Assert.Null(f.Stamp(cast));AbilityCastInterrupts.Begin(f.Source,610,2);Assert.False(AbilityCastInterrupts.Complete(normal,()=>throw new Exception("stale")));
        Assert.Null(f.Stamp(cast));Assert.Null(CombatSpecials.MostHated(f.Mob));
    }
    [Fact]public void Applied610OwnerExpiresOnResetBut647ConsumerRemainsEquivalent()
    {
        using var f=new Fixture();Assert.True(AbilityEngageFoes.After(f.Stamp(f.Cast())));Assert.Same(f.Source,CombatSpecials.MostHated(f.Mob));
        f.Source.TransportFence.PublishIncoming(true,()=>{});Assert.Null(CombatSpecials.MostHated(f.Mob));
        Assert.True(CombatSpecials.ApplyNpcTaunt(f.Source,f.Mob));Assert.Same(f.Source,CombatSpecials.MostHated(f.Mob));
        Assert.False(CombatSpecials.TurnedAway(f.Mob,f.Source)); //647 still uses its original same-owner shortcut.
    }
    [Fact]public void FailedReplacementRetainsPreviousOwner()
    {
        using var f=new Fixture();Assert.True(CombatSpecials.ApplyNpcTaunt(f.Source,f.Mob));
        int checks=0;
        Assert.False(CombatSpecials.ApplyNpcTaunt(f.Source,f.Mob,()=>++checks==1));
        Assert.Same(f.Source,CombatSpecials.MostHated(f.Mob));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void FailedReplacementDoesNotReviveClearedOrOverwriteNewerRecord(bool newer)
    {
        using var f=new Fixture();Assert.True(CombatSpecials.ApplyNpcTaunt(f.Source,f.Mob));
        int checks=0;
        bool Current()
        {
            if(++checks==1)return true;
            if(checks==2)
            {
                if(newer)Assert.True(CombatSpecials.ApplyNpcTaunt(f.Source,f.Mob));
                else Assert.Null(CombatSpecials.MostHated(f.Mob)); //Third observation clears our now-invalid candidate.
            }
            return false;
        }
        Assert.False(CombatSpecials.ApplyNpcTaunt(f.Source,f.Mob,Current));
        if(newer)Assert.Same(f.Source,CombatSpecials.MostHated(f.Mob));
        else Assert.Null(CombatSpecials.MostHated(f.Mob));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Mob,WeakReference<WorldClient>) OrphanedOwner()
    {
        var f=new Fixture();Assert.True(AbilityEngageFoes.After(f.Stamp(f.Cast())));
        var result=(f.Mob,new WeakReference<WorldClient>(f.Source));f.Dispose();return result;
    }
    [Fact]public void LongLivedNpcDoesNotRetain610CastOrDisconnectedClient()
    {
        var (mob,owner)=OrphanedOwner();
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Assert.False(owner.TryGetTarget(out _));Assert.Null(CombatSpecials.MostHated(mob));GC.KeepAlive(mob);
    }
    [Fact]public void RequiredSourcePinRejectsAbsentMalformedAndWrongBuild()
    {
        string hash=new('a',64);HeadlessClient.EngageFoesFixture.RequireSource(hash,hash.ToUpperInvariant());
        Assert.Throws<InvalidDataException>(()=>HeadlessClient.EngageFoesFixture.RequireSource(null,hash));
        Assert.Throws<InvalidDataException>(()=>HeadlessClient.EngageFoesFixture.RequireSource("wrong",hash));
        Assert.Throws<InvalidDataException>(()=>HeadlessClient.EngageFoesFixture.RequireSource(hash,new('b',64)));
    }
}
