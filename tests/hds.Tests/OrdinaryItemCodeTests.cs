using hds;
using NativeQa.Modes;

namespace hds.Tests;
public class OrdinaryItemCodeTests
{
    [Theory]
    [InlineData(33,50,0,0,18f)]
    [InlineData(33,50,0,33,12.06f)]
    [InlineData(33,50,25,33,9.045f)]
    [InlineData(1,50,0,33,3f)]
    [InlineData(255,0,0,0,50f)]
    [InlineData(33,65586,0,0,18f)]
    public void NativeTimerUsesSignedTotalsAndFinalClamp(int complexity,int skill,int general,int consumable,float expected)
        => Assert.Equal(expected,OrdinaryItemCode.Seconds(complexity,skill,general,consumable));
    [Fact]
    public void OrdinaryChargesAreDirectAndPreserveOpaqueUpperBits()
    {
        uint source=0x40000000u | 39413u<<7;
        Assert.True(OrdinaryItemCode.Valid(source,2));
        Assert.Equal(source|1,OrdinaryItemCode.Packed(source,1));
        Assert.Equal(39413u,new OrdinaryItemCode.Entry { Base=source }.Goid);
        Assert.False(OrdinaryItemCode.Valid(source,0)); Assert.False(OrdinaryItemCode.Valid(source,128));
        Assert.False(OrdinaryItemCode.Valid(source|1,2));
        Assert.False(OrdinaryItemCode.Valid(CodeStorage.Pack(7332,1),1));
        Assert.Equal(source|1,BitConverter.ToUInt32(OrdinaryItemCode.Update(400,source,1),4));
        Assert.Equal(10,OrdinaryItemCode.Update(400,source,1).Length);
    }
    [Fact]
    public void CostUsesNativeFloatConstantWithoutChangingWriterPolicy()
    {
        var item=new CraftingRules.Item { Price=75 };
        Assert.Equal(8,OrdinaryItemCode.Cost(item,0)); Assert.Equal(9,CraftingRules.InfoCost(item));
        item.Price=137000; Assert.Equal(16439,OrdinaryItemCode.Cost(item,0));
        Assert.Equal(0,OrdinaryItemCode.Cost(item,100));
        Assert.Equal(16604,OrdinaryItemCode.Cost(item,-1));
        Assert.Equal(0,OrdinaryItemCode.Cost(item,101));
        Assert.Throws<ArgumentException>(()=>OrdinaryItemCode.Cost(new CraftingRules.Item(),0));
    }
    [Fact]
    public void SupportedPrototypesHaveExplicitSourcePrices()
    {
        var rows=Crafting.Recipes.Values.Where(r=>r.Type.StartsWith("IC")).ToArray();
        Assert.Equal(28,rows.Length);
        Assert.All(rows,r=> { Assert.True(CraftingRules.Find(r.ResultGoid).Price.HasValue); Assert.True(OrdinaryItemCode.Supported(r.ResultGoid)); });
    }
    [Fact]
    public void DelayedExceptionsReachRefusalHandlerOnceAndPreserveException()
    {
        var error=new InvalidOperationException("transaction rolled back"); int refusals=0; bool committed=false;
        OrdinaryItemCompile.CompleteSafely(()=>throw error, ex=> { Assert.Same(error,ex); refusals++; });
        Assert.Equal(1,refusals); Assert.False(committed);
        OrdinaryItemCompile.CompleteSafely(()=>committed=true,_=>refusals++);
        Assert.True(committed); Assert.Equal(1,refusals);
    }
    [Fact]
    public void CompletionRequiresExactLifeWorldAndCast()
    {
        Assert.True(OrdinaryItemCompile.Matches(1,2,30,1,2,30));
        Assert.False(OrdinaryItemCompile.Matches(1,2,30,2,2,30));
        Assert.False(OrdinaryItemCompile.Matches(1,2,30,1,3,30));
        Assert.False(OrdinaryItemCompile.Matches(1,2,30,1,2,31));
        Assert.False(CraftingRules.Succeeds(0,33,100));
        Assert.True(Enumerable.Range(0,101).All(r=>CraftingRules.Succeeds(50,33,r)));
        Assert.True(Enumerable.Range(0,101).All(r=>!OrdinaryItemCode.Succeeds(1,23,r,0)));
        Assert.True(Enumerable.Range(0,101).All(r=>OrdinaryItemCode.Succeeds(20,10,r,-100)==OrdinaryItemCode.Succeeds(20,10,r,0)));
    }
    [Fact]
    public void NativeModeHasTwoDirectChargesAndOnlyGuardedLoadoutCommands()
    {
        var plan=new OrdinaryIcCompileMode().Plan;
        Assert.Contains("5044864",string.Join("\n",plan.AfterLoadout));
        Assert.True(plan.DevCommands);
        Assert.False(plan.FixedCallbackRolls); Assert.False(plan.GmCharacter110);
    }
}
