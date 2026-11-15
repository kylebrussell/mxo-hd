using HeadlessClient;
using hds;
namespace hds.Tests;

public class TransmitCodeFixtureTests
{
    private static readonly byte[] Literal=Convert.FromHexString("816717002700F900007C000010000000480780360000070E00416674657257686F72754E656F000D005A6F6D626965204C757263680002004500000002000000");
    [Fact]public void CompletePrimaryDamageTargetShapeDecodesItsTwoValues()
    {
        Assert.True(TransmitCodeFixture.Damage(Literal,466,"AfterWhoruNeo","Zombie Lurch",out int done,out int resisted));
        Assert.Equal(69,done);Assert.Equal(2,resisted);
    }
    [Fact]public void EveryTruncationAndExtraSuffixRefusesInsteadOfSearchingForAConvenientValue()
    {
        for(int length=0;length<Literal.Length;length++)Assert.False(TransmitCodeFixture.Damage(Literal[..length],466,"AfterWhoruNeo","Zombie Lurch",out _,out _));
        Assert.False(TransmitCodeFixture.Damage(Literal.Concat(new byte[]{0}).ToArray(),466,"AfterWhoruNeo","Zombie Lurch",out _,out _));
    }
    [Theory][InlineData(6)][InlineData(10)][InlineData(11)][InlineData(15)][InlineData(19)][InlineData(22)][InlineData(54)]
    public void WrongTemplateFlagIdentityOffsetTypeAndValueCountRefuse(int offset)
    {
        var bad=Literal.ToArray();bad[offset]^=1;Assert.False(TransmitCodeFixture.Damage(bad,466,"AfterWhoruNeo","Zombie Lurch",out _,out _));
    }
    [Fact]public void NamesAndAbilityMustMatchTheExactCurrentCaseActors()
    {
        Assert.False(TransmitCodeFixture.Damage(Literal,474,"AfterWhoruNeo","Zombie Lurch",out _,out _));
        Assert.False(TransmitCodeFixture.Damage(Literal,466,"Other","Zombie Lurch",out _,out _));
        Assert.False(TransmitCodeFixture.Damage(Literal,466,"AfterWhoruNeo","Other",out _,out _));
    }
    [Fact]public void TheEntireRequiredGraphIsLoadedAndFitsNormalCapacityWithoutChangingParents()
    {
        foreach(var required in AbilityPrerequisites.Current.Closure(593))
        {Assert.Contains(required.Key,TransmitCodeFixture.DefenderAbilities);Assert.True(required.Value<=50);}
        Assert.Contains((ushort)593,TransmitCodeFixture.DefenderAbilities);
        int memory=TransmitCodeFixture.DefenderAbilities.Sum(id=>(int)AbilityCatalog.Find(id)!.Number("Ability/InventoryMemCost"));
        Assert.True(memory<=AbilityMemory.ForLevel(50));
        var mode=NativeQa.NativeQaModes.Find("transmit-code-resistance");Assert.Equal("transmit-code-resistance",mode.Name);
        Assert.False(mode.Plan.FixedCallbackRolls);Assert.Equal("65",mode.Plan.EnvironmentOverrides["MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW"]);
        Assert.Contains("OURS",mode.Plan.ReadyMessages.Single());
    }
}
