using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using HeadlessClient;
namespace hds.Tests;

public class InvisibilityToolRegistrationTests
{
    static readonly byte[] Base = Convert.FromHexString("80B25C0400000802");
    static readonly byte[] Add = Convert.FromHexString("80BC45035C0400AF0000005C04000000000000000000");
    static readonly byte[] Remove = Convert.FromHexString("80BC45035C0400AF0000005C04000000000100000000");

    [Fact] public void DedicatedFixtureUsesOwnedParentAndNormalToolAndRequiresExactSourcePin()
    {
        var seed=InvisibilityToolRegistrationFixture.Seed("Test");
        Assert.Contains(seed.OwnedLoadedAbilities!,a=>a.Ability==175);
        Assert.DoesNotContain(seed.OwnedLoadedAbilities!,a=>a.Ability==1116);
        Assert.Equal((40,460u,1),Assert.Single(seed.Items));
        var request=InvisibilityToolRegistrationFixture.ToolRequest();
        Assert.Equal(43,request.Length);Assert.Equal((ushort)1116,BitConverter.ToUInt16(request,2));
        Assert.Equal(40,request[37]);Assert.Equal(0,request[38]);
        string hash=new('A',64);
        foreach(string? wrong in new[]{null,"",new string('B',64),new string('G',64)})
            Assert.Throws<InvalidOperationException>(()=>InvisibilityToolRegistrationFixture.RequireSource(wrong,hash));
        InvisibilityToolRegistrationFixture.RequireSource(hash.ToLowerInvariant(),hash);
    }

    [Fact] public void ExactStoredParentQualifiesWithoutBroadeningOtherToolGrants()
    {
        Assert.True(AbilityGrantRegistration.Handles(175,1116));
        Assert.Equal(new ushort[]{1116},AbilityProgression.GrantsOf(175));
        Assert.Equal((ushort)0,AbilityProgression.GrantLevel(175,50));
        Assert.False(AbilityGrantRegistration.Handles(120,1114));
        Assert.False(AbilityGrantRegistration.Handles(175,1117));
        Assert.DoesNotContain(((ushort)175,(ushort)1116),AbilityGrantRegistration.ObservedPairs);
        var mutable=Assert.IsAssignableFrom<IList<(ushort Source,ushort Target)>>(AbilityGrantRegistration.NativeToolPairs);
        Assert.True(mutable.IsReadOnly);
        Assert.Throws<NotSupportedException>(()=>mutable.Add((120,1114)));
    }

    [Fact] public void ActualReadyHandlerPublishesBaseThenMembershipAndTracksParentAndLife()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.messageQueue=new();
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        c.playerData.passives.Loaded[175]=50;c.playerData.passives.Loaded[1116]=0;
        byte[][] Messages()=>c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m=>m.content.Skip(1).ToArray()).ToArray();
        AbilityGrantRegistration.LoadedChanged(c);Assert.Empty(Messages());
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});
        Assert.Equal(new[]{Base,Add},Messages());
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(2,Messages().Length);
        c.playerData.passives.Loaded.Remove(175); // A stale derived child alone must not qualify.
        AbilityGrantRegistration.LoadedChanged(c);
        Assert.Equal(Convert.FromHexString("80B35C04"),Messages()[2]);Assert.Equal(Remove,Messages()[3]);
        c.playerData.passives.Loaded[175]=50;AbilityGrantRegistration.LoadedChanged(c);
        Assert.Equal(Base,Messages()[4]);Assert.Equal(Add,Messages()[5]);
        c.messageQueue.RPCMessagesQueue.Clear();
        c.playerData.setOnWorld(false);c.playerData.setOnWorld(true);
        AbilityGrantRegistration.LoadedChanged(c);Assert.Empty(Messages());
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(new[]{Base,Add},Messages());
    }
}
