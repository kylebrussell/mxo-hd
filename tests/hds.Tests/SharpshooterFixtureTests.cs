using HeadlessClient;
using hds;
namespace hds.Tests;
public class SharpshooterFixtureTests
{
    private static readonly byte[] Literal=Convert.FromHexString("81682a001d00030000000a0001006400320000000000011902000000000b0053686172704669737473000d00536861727053686f6f74657200");
    [Fact]public void CompleteOrdinaryShotHasExactWeaponActorsAndIndependentNumbers()
    {
        Assert.True(SharpshooterFixture.ReadShot(Literal,"SharpShooter","SharpFists",true,out var s));
        Assert.Equal(new SharpshooterFixture.Shot(true,10,1,100,50),s);
        var target=Literal.ToArray();target[22]=0;Assert.True(SharpshooterFixture.ReadShot(target,"SharpShooter","SharpFists",false,out var t));Assert.Equal(s,t);
        Assert.False(SharpshooterFixture.ReadShot(Literal,"SharpShooter","SharpFists",false,out _));
        Assert.False(SharpshooterFixture.ReadShot(Literal,"Other","SharpFists",true,out _));
        Assert.False(SharpshooterFixture.ReadShot(Literal,"SharpShooter","Other",true,out _));
        for(int n=0;n<Literal.Length;n++)Assert.False(SharpshooterFixture.ReadShot(Literal[..n],"SharpShooter","SharpFists",true,out _));
        Assert.False(SharpshooterFixture.ReadShot(Literal.Concat(new byte[]{0}).ToArray(),"SharpShooter","SharpFists",true,out _));
        foreach(int offset in new[]{2,4,6,18,22,23}){var bad=Literal.ToArray();bad[offset]^=1;Assert.False(SharpshooterFixture.ReadShot(bad,"SharpShooter","SharpFists",true,out _));}
        var miss=Literal.ToArray();miss[6]=4;Assert.False(SharpshooterFixture.ReadShot(miss,"SharpShooter","SharpFists",true,out _));
        Array.Clear(miss,10,4);Assert.True(SharpshooterFixture.ReadShot(miss,"SharpShooter","SharpFists",true,out var missed));Assert.False(missed!.Hit);
    }
    [Fact]public void CurrentInputRecordDoesNotTurnNaturalWireNumbersIntoASeededContest()
    {
        const string line="[SHARPSHOOTER OURS] actor=SharpShooter class56 defenderGunFalse metres1.200 opponents2 countSampledTrue loaded148False modifier-20 attackBonus75->55";
        var p=SharpshooterFixture.ReadInput(line,"SharpShooter");Assert.NotNull(p);Assert.Equal(-20,p.Modifier);Assert.Equal(75,p.Before);Assert.Equal(55,p.After);Assert.True(p.CountSampled);
        Assert.Null(SharpshooterFixture.ReadInput(line,"Other"));Assert.Null(SharpshooterFixture.ReadInput(line.Replace("->55","->75"),"SharpShooter"));
        Assert.Null(SharpshooterFixture.ReadInput("prefix"+line,"SharpShooter"));Assert.Null(SharpshooterFixture.ReadInput(line+"suffix","SharpShooter"));
        Assert.Null(SharpshooterFixture.ReadInput(line.Replace("class56","class256"),"SharpShooter"));
    }
    [Fact]public void ModeUsesTheSameOwnedParentsAndNoRandomControl()
    {
        foreach(var required in AbilityPrerequisites.Current.Closure(148))Assert.Contains(required.Key,SharpshooterFixture.Abilities);
        Assert.True(SharpshooterFixture.Abilities.Sum(id=>AbilityCatalog.Find(id)!.Number("Ability/InventoryMemCost"))<=AbilityMemory.ForLevel(50));
        var mode=NativeQa.NativeQaModes.Find("sharpshooter-freefire");Assert.Equal("sharpshooter-freefire",mode.Name);Assert.Equal(SharpshooterFixture.Abilities,mode.Plan.Abilities);
        Assert.False(mode.Plan.DevCommands);Assert.False(mode.Plan.FixedCallbackRolls);Assert.False(mode.Plan.GmCharacter110);
        Assert.DoesNotContain(mode.Plan.EnvironmentOverrides.Keys,k=>k.Contains("ROLL")||k.Contains("DRAW"));Assert.Contains("OURS",mode.Plan.ReadyMessages.Single());
    }
}
