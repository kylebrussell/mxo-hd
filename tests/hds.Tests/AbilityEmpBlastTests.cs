using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;
public class AbilityEmpBlastTests
{
    [Fact] public void OnlyExplicitTargetGrantEnablesScriptDamage()
    {
        var target=new Mob();var other=new Mob();
        Assert.Equal(0,AbilityEmpBlast.Damage(target));
        target.SetAbilityGrant(1025,true);Assert.Equal(0,AbilityEmpBlast.Damage(target));
        target.SetAbilityGrant(1026,true);Assert.Equal(5000,AbilityEmpBlast.Damage(target));
        Assert.Equal(0,AbilityEmpBlast.Damage(other));
        target.SetAbilityGrant(1026,false);Assert.Equal(0,AbilityEmpBlast.Damage(target));
    }
    [Theory]
    [InlineData("1","mxotest_emp",true)]
    [InlineData("true","mxotest_emp",false)]
    [InlineData("0","mxotest_emp",false)]
    [InlineData("1","reality_hd",false)]
    [InlineData("1","Mxotest_emp",false)]
    [InlineData("1",null,false)]
    public void WeaknessFixtureRequiresExactDevAndDisposableDatabase(string dev,string? db,bool expected)
        => Assert.Equal(expected,AbilityEmpBlast.FixtureAllowed(dev,db));
    [Fact] public void ShippedContractRemainsDistinctFromMarker()
    {
        var e=AbilityCatalog.Find(1025);
        Assert.Equal(10,e.Number("Ability/RadiusOfEffect"));Assert.Equal(1,e.CastSeconds);
        Assert.Equal(30,e.Number("Ability/RecastTime"));Assert.Equal(4,e.Number("Ability/CastDirectObjects"));
        Assert.Equal(1,e.Number("Ability/CastWhenUsed"));
        Assert.False(AbilityCatalog.Find(1026).DirectDamage);
    }
    [Fact] public void TryRefuseEnforcesLoadedAndOutsideCombatGates()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(51025);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)19);
        client.messageQueue = new MessageQueue();

        Assert.True(AbilityEmpBlast.TryRefuse(client, out uint reason));
        Assert.Equal(AbilityRefusals.NotLoaded, reason);

        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (1025, 1) });
        Assert.False(AbilityEmpBlast.TryRefuse(client, out reason));
        Assert.Equal(0u, reason);

        var combat = CombatManager.GetHandler(client);
        combat.isCombatRunning = true;
        try
        {
            Assert.True(AbilityEmpBlast.TryRefuse(client, out reason));
            Assert.Equal(AbilityRefusals.MustBeOutsideCombat, reason);
        }
        finally { combat.isCombatRunning = false; }
    }
}
