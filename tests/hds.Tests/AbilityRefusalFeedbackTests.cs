using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class AbilityRefusalFeedbackTests:IDisposable
{
    readonly WorldClient oldClient=Store.currentClient;readonly ServerConfig oldConfig=Store.config;
    readonly Dictionary<string,WorldClient>? oldClients=WorldServer.Clients;
    readonly FieldInfo loaderField=typeof(DataLoader).GetField("Instance",BindingFlags.Static|BindingFlags.NonPublic)!;
    readonly object? oldLoader;
    readonly string[] keys={"MXO_ABILITY_REFUSAL_QA","MXO_DEV_COMMANDS","MXO_DB_NAME"};readonly string?[] oldEnv;
    public AbilityRefusalFeedbackTests()
    {
        oldLoader=loaderField.GetValue(null);oldEnv=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        foreach(string key in keys)Environment.SetEnvironmentVariable(key,key=="MXO_DB_NAME"?"mxotest_misleading":"1");
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_refusal"});
        WorldServer.Clients=new();var loader=(DataLoader)RuntimeHelpers.GetUninitializedObject(typeof(DataLoader));var ability=new AbilityItem();ability.setAbilityID(77);ability.setAbilityName("Repair RSI");ability.setCastAnimStart(new byte[4]);loader.AbilityDB=new(){ability};loaderField.SetValue(null,loader);
    }
    public void Dispose(){Store.currentClient=oldClient;Store.config=oldConfig;WorldServer.Clients=oldClients!;loaderField.SetValue(null,oldLoader);for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],oldEnv[i]);}
    static byte[] Hex(string s)=>Convert.FromHexString(s);
    static WorldClient Client(ushort inner=500)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        c.playerData=new ClientData();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        c.playerInstance=new PlayerCharacter();c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
        c.playerInstance.InnerStrengthAvailable.setValue(inner);c.playerInstance.InnerStrengthMax.setValue((ushort)1000);c.messageQueue=new();c.viewMan=new();return c;
    }
    static byte[][] Rpcs(WorldClient c)=>c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m=>m.content.Skip(1).ToArray()).ToArray();
    [Fact] public void NativeNineteenByteFieldsAreFullWidthAndLossless()
    {
        byte[] raw=Hex("80baefcdab89fe98badcfe1032547678563412");Assert.True(AbilityRefusals.TryDecode(raw,out var r));
        Assert.Equal(0x89abcdefu,r.AbilityID);Assert.Equal(0xfedcba98u,r.ErrorID);Assert.Equal(0x76543210u,r.InnerstrengthDeficit);Assert.Equal(0x12345678u,r.StatesNotMet);Assert.Equal(0xfe,r.ToolsSlot);Assert.Equal(raw,AbilityRefusals.Encode(r));
        foreach(var bad in new[]{raw[..18],raw.Concat(new byte[]{0}).ToArray(),new byte[19]})Assert.False(AbilityRefusals.TryDecode(bad,out _));
    }
    [Fact] public void LiteralMessagesPreserveDefaultsAndKnownRetailReuseRecord()
    {
        Assert.Equal(Hex("80ba660300006b050000000000000000000000"),AbilityRefusals.Encode(new(870,AbilityRefusals.ReuseWait)));
        Assert.Equal(Hex("80ba4d0000006b110000000f00000000000000"),AbilityRefusals.Encode(new(77,AbilityRefusals.InsufficientInnerStrength,15)));
        Assert.Equal(Hex("80ba4d0000006b070000000000000000000000"),AbilityRefusals.Encode(new(77,AbilityRefusals.NotLoaded)));
        Assert.Equal(Hex("80ba840000006b0d0000000000000000000000"),AbilityCooldowns.BuildDenied(132,CombatStyles.DeniedWrongStyle));
        Assert.Equal((byte)6,AbilityCooldowns.DeniedBusy);Assert.Equal((byte)6,AbilityCooldowns.DeniedLegacyAmbiguous);
    }
    [Fact] public void PoorFixtureUsesActualMinimumAndCappedRegeneration()
    {
        var c=Client(0);c.playerInstance.InnerStrengthMax.setValue((ushort)0);
        AbilityBuffs.RefreshVitals(c,false);
        Assert.Equal((ushort)1,AbilityHandler.MaxInnerStrength(c));
        Assert.Equal((ushort)1,AbilityHandler.RegenerateInnerStrength(c));
        for(int i=0;i<5;i++)Assert.Null(AbilityHandler.RegenerateInnerStrength(c));
        Assert.Equal((ushort)1,AbilityHandler.CurrentInnerStrength(c));
        Assert.Equal(9,AbilityCosts.Effective(c,77)-AbilityHandler.CurrentInnerStrength(c));
    }
    [Fact] public void ActualInsufficientPredicateReportsExactPositiveSnapshotWithoutStartingAnything()
    {
        var c=Client(4);double beforeCast=c.playerData.castEndsAt;Store.currentClient=c;Assert.Equal((ushort)10,AbilityCosts.Effective(c,77));byte[] request={77,0,2,0};new AbilityHandler().ProcessAbility(ref request);
        Assert.Contains(Rpcs(c),r=>r.SequenceEqual(Hex("80ba4d0000006b110000000600000000000000")));
        Assert.DoesNotContain(Rpcs(c),r=>r.Length>=2&&r[0]==0x80&&(r[1]==0xac||r[1]==0xbd));
        Assert.Equal((ushort)4,AbilityHandler.CurrentInnerStrength(c));Assert.Equal((ushort)1000,PvpCombat.HealthOf(c));Assert.Equal(beforeCast,c.playerData.castEndsAt);Assert.Empty(c.playerData.cooldownEndsAt);
    }
    [Fact] public void ActualTimerPredicateReportsReuseAndPreservesCostAndTimer()
    {
        var c=Client(500);double beforeCast=c.playerData.castEndsAt;Store.currentClient=c;double end=TimeUtils.GetSimTimeSeconds()+100;c.playerData.cooldownEndsAt[77]=end;
        byte[] request={77,0,2,0};new AbilityHandler().ProcessAbility(ref request);
        Assert.Equal(Hex("80ba4d0000006b050000000000000000000000"),Assert.Single(Rpcs(c)));
        Assert.Equal((ushort)500,AbilityHandler.CurrentInnerStrength(c));Assert.Equal(end,c.playerData.cooldownEndsAt[77]);Assert.Equal(beforeCast,c.playerData.castEndsAt);Assert.Equal((ushort)1000,PvpCombat.HealthOf(c));
    }
    [Fact] public void SuccessfulNormalCastStillPaysOnceHealsAndStartsItsExistingTimer()
    {
        var c=Client(500);c.playerInstance.Health.setValue((ushort)100);Store.currentClient=c;
        byte[] request={77,0,2,0};new AbilityHandler().ProcessAbility(ref request);double end=c.playerData.castEndsAt;Assert.True(end>TimeUtils.GetSimTimeSeconds());
        DelayedActions.RunDue(end+1);Assert.True(PvpCombat.HealthOf(c)>100);Assert.Equal((ushort)490,AbilityHandler.CurrentInnerStrength(c));
        Assert.True(AbilityCooldowns.IsCoolingDown(c.playerData,77,TimeUtils.GetSimTimeSeconds()));Assert.DoesNotContain(Rpcs(c),r=>r.Length>=2&&r[0]==0x80&&r[1]==0xba);
    }
    [Fact] public void FixedProbeGuardsAndNamesCannotParameterizeAuthority()
    {
        bool Allowed(string o="1",string d="1",string db="mxotest_x",uint ch=110,bool alive=true,bool world=true,bool dead=false,bool pending=false,bool shutdown=false,bool la=false)=>AbilityRefusalQa.Allowed(o,d,db,ch,alive,world,dead,pending,shutdown,la);
        Assert.True(Allowed());Assert.False(Allowed(o:"true"));Assert.False(Allowed(d:"0"));foreach(string db in new[]{"reality_hd","mxotest_","mxotest_bad-name"})Assert.False(Allowed(db:db));
        foreach(uint ch in new uint[]{0,2,111})Assert.False(Allowed(ch:ch));Assert.False(Allowed(alive:false));Assert.False(Allowed(world:false));Assert.False(Allowed(dead:true));Assert.False(Allowed(pending:true));Assert.False(Allowed(shutdown:true));Assert.False(Allowed(la:true));
        foreach(string name in new[]{"reuse 999","inner 999","7","77","unloaded extra"})Assert.False(AbilityRefusalQa.TrySample(name,out _));
    }
    [Fact] public void ActualDisplayEnqueueChangesNoResourcesTimersOrLoadedMembershipAndPrunesOnReset()
    {
        var c=Client(123);double beforeCast=c.playerData.castEndsAt;Store.currentClient=c;c.playerData.passives.Loaded[77]=50;c.playerData.cooldownEndsAt[77]=12345;
        c.messageQueue.addRpcMessage(new byte[]{0x81,0x49});var stamp=new AbilityRefusalQa.Stamp(c);
        foreach(string name in new[]{"reuse","inner","unloaded"})Assert.True(AbilityRefusalQa.Enqueue(c,name,stamp));
        Assert.Equal((ushort)123,AbilityHandler.CurrentInnerStrength(c));Assert.Equal((ushort)1000,PvpCombat.HealthOf(c));Assert.Equal((ushort)50,c.playerData.passives.Loaded[77]);Assert.Equal(12345,c.playerData.cooldownEndsAt[77]);Assert.Equal(beforeCast,c.playerData.castEndsAt);
        c.TransportFence.PublishIncoming(true,()=>{});long epoch=c.TransportFence.Capture();c.TransportFence.ApplyReset(epoch,()=>{});c.TransportFence.Resume(epoch);
        Assert.False(AbilityRefusalQa.Enqueue(c,"inner",stamp));Assert.True(c.TransportFence.TryRun(epoch,()=>c.messageQueue.PruneUnassignedWorldRpcs(c.playerData.worldGeneration,epoch)));
        Assert.Equal(new byte[]{0x81,0x49},Assert.Single(Rpcs(c)));
        var fresh=new AbilityRefusalQa.Stamp(c);c.playerData.lifeGeneration++;Assert.False(AbilityRefusalQa.Enqueue(c,"reuse",fresh));
        Store.config.dbParams.DatabaseName="reality_hd";Assert.False(AbilityRefusalQa.Enqueue(c,"reuse",new AbilityRefusalQa.Stamp(c)));
    }
    [Fact] public void ModeUsesProvenClosedLoadoutWithoutGmOrRandomControls()
    {
        var plan=new NativeQa.Modes.AbilityRefusalFeedbackMode().Plan;Assert.Equal(new ushort[]{2,17,45,1006,77},plan.Abilities);Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal("1",plan.EnvironmentOverrides["MXO_ABILITY_REFUSAL_QA"]);Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")["MXO_ABILITY_REFUSAL_QA"]);
    }
}
