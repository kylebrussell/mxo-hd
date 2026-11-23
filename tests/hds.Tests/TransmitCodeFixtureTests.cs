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
        Assert.False(mode.Plan.FixedCallbackRolls);Assert.Equal("32766",mode.Plan.EnvironmentOverrides["MXO_FREECAST_VIRAL_QA_ATTACK15"]);
        Assert.Contains(mode.Plan.ReadyMessages, message => message.Contains("OURS"));
    }
    [Fact]public void RetainedNormalHackerCastHasStyled538DamageBeforeTheBonus()
    {
        var bytes=Convert.FromHexString("816717002800f900007c000010000000280580390000070f005472616e736d6974436173746572000f005472616e736d69745461726765740002001a02000000000000");
        Assert.True(TransmitCodeFixture.Damage(bytes,330,"TransmitCaster","TransmitTarget",out int done,out int resisted));
        Assert.Equal(538,done);Assert.Equal(0,resisted);
        Assert.Equal(done,(int)(424*(double)(float)1.27)); //existing normal Hacker172+27, not a target593 change
        double zero=.66/220*20-.33,withBonus=.66/220*42-.33;
        Assert.Equal(510,(int)Math.Floor(done*(1-withBonus)/(1-zero)));
        Assert.Equal(510,TypedAbilityResistance.Amount(done,50,50,22));
    }    [Fact]public void RetainedSourcedDotValueUsesItsActualRandomTotalAndPrimaryInterval()
    {
        var modifier=Convert.FromHexString("80bc1d00200100da01000085044f040000000000f041");
        Assert.True(TransmitCodeFixture.DotOracle(modifier,out int total,out int raw,out int final));
        Assert.Equal(1103,total);Assert.Equal(92,raw);Assert.Equal(87,final);
        var damage=Convert.FromHexString("816717002800f900007c000010000000680780390000070f005472616e736d6974436173746572000f005472616e736d69745461726765740002005700000005000000");
        Assert.True(TransmitCodeFixture.Damage(damage,474,"TransmitCaster","TransmitTarget",out int done,out int absorbed));
        Assert.Equal(final,done);Assert.Equal(raw-final,absorbed);
        for(int length=0;length<modifier.Length;length++)Assert.False(TransmitCodeFixture.DotOracle(modifier[..length],out _,out _,out _));
        foreach(int offset in new[]{0,2,6,7,11,17,21}){var bad=modifier.ToArray();bad[offset]^=1;Assert.False(TransmitCodeFixture.DotOracle(bad,out _,out _,out _));}
        foreach(int value in new[]{1099,1100,1117}){var valid=modifier.ToArray();BitConverter.GetBytes(value).CopyTo(valid,13);Assert.True(TransmitCodeFixture.DotOracle(valid,out _,out int r,out int f));Assert.Equal(value>=1110?93:92,r);Assert.Equal(r==93?88:87,f);}
        foreach(int value in new[]{0,1098,1118,int.MaxValue}){var bad=modifier.ToArray();BitConverter.GetBytes(value).CopyTo(bad,13);Assert.False(TransmitCodeFixture.DotOracle(bad,out _,out _,out _));}
        Assert.False(TransmitCodeFixture.DotOracle(modifier.Concat(new byte[]{0}).ToArray(),out _,out _,out _));
    }}
