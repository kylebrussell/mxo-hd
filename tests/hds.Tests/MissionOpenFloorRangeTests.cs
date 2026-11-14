using hds;
namespace hds.Tests;
public class MissionOpenFloorRangeTests
{
    private const string Identity="0000000000000000000000000000803f";
    private static readonly OpenFloorPoint Zero=new(0,0,0);
    private static MissionOpenFloorUsableRange.Profile Box()=>new(548,3,100,new(50,40,70),"literal GOB548/native Usable5 proof");
    [Theory] [InlineData(548,50,40,70,135)] [InlineData(552,40,50,35,100)]
    public void PositiveAuthoredDistanceHasTheNativeSizeFloor(int goid,double x,double y,double z,float expected)
    {
        var p=new MissionOpenFloorUsableRange.Profile((ushort)goid,3,100,new(x,y,z),"literal native template");
        Assert.Equal(expected,p.LowerBound);
    }
    [Fact]
    public void ComputerUsesTheProvenInitialCvarBranchWithoutAnInventedAuthoredValueOrSizeFloor()
    {
        var p=MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,40),"initial UsableExecuteMaxDist400 native proof");
        Assert.Equal(0,p.StoredMaxDistance);Assert.Equal(400,p.LowerBound);Assert.True(p.AssumesInitialClientConfiguration);
        Assert.Equal(MissionOpenFloorUsableRange.RangeSource.InitialClientFallback,p.Source);
        Assert.True(MissionOpenFloorUsableRange.Test(p,new(443,0,0),Zero,new(Identity)).Within);
        Assert.False(MissionOpenFloorUsableRange.Test(p,new(443.01,0,0),Zero,new(Identity)).Within);
        var large=MissionOpenFloorUsableRange.Profile.InitialClient400(3,5,new(43,25,1000),"literal branch independence");
        Assert.Equal(400,large.LowerBound); // fallback must NOT apply Z+65
        Assert.Throws<ArgumentException>(()=>MissionOpenFloorUsableRange.Profile.InitialClient400(548,3,new(50,40,70),"unseen fallback"));
    }
    [Fact]
    public void ThreeDimensionalOrientedBoxDistanceIncludesVerticalAndCornerExcess()
    {
        var q=new OpenFloorOrientation(Identity);var p=Box();
        var x=MissionOpenFloorUsableRange.Test(p,new(185,0,0),Zero,q);Assert.True(x.Within);Assert.Equal(18225,x.DistanceSquared);
        Assert.False(MissionOpenFloorUsableRange.Test(p,new(185.01,0,0),Zero,q).Within);
        Assert.True(MissionOpenFloorUsableRange.Test(p,new(0,175,0),Zero,q).Within);
        Assert.False(MissionOpenFloorUsableRange.Test(p,new(0,175.01,0),Zero,q).Within);
        // Independent 81/108/135 Pythagorean triangle outside the OBB, not centre distance.
        Assert.True(MissionOpenFloorUsableRange.Test(p,new(131,148,0),Zero,q).Within);
        Assert.False(MissionOpenFloorUsableRange.Test(p,new(131.01,148,0),Zero,q).Within);
        Assert.True(MissionOpenFloorUsableRange.Test(p,new(50,40,70),Zero,q).Within);
    }
    [Fact]
    public void NativeYawUsesTheActualInteractionAxesAndPreservesLiteralQuaternion()
    {
        const string yaw="00000000f304353f00000000f304353f";var q=new OpenFloorOrientation(yaw);
        Assert.Equal(yaw,q.Hex);Assert.True(q.YawOnly);
        Assert.True(MissionOpenFloorUsableRange.Test(Box(),new(0,0,200),Zero,new(Identity)).Within);
        Assert.False(MissionOpenFloorUsableRange.Test(Box(),new(0,0,200),Zero,q).Within);
        var local=q.NativeLocalDelta(new(0,0,200),Zero);Assert.InRange(local[0],-200.0001f,-199.9999f);Assert.InRange(local[2],-.0001f,.0001f);
    }
    [Theory] [InlineData(33554432)] [InlineData(-33554432)]
    public void NativeSubtractsDoubleWorldPositionsBeforeFloatNarrowing(double origin)
    {
        var w=MissionOpenFloorUsableRange.Test(Box(),new(origin+185,origin,origin),new(origin,origin,origin),new(Identity));
        Assert.True(w.Within);Assert.Equal(18225,w.DistanceSquared);
    }
    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void InvalidPointCannotProduceARangeWitness(double bad)
    {
        Assert.False(MissionOpenFloorUsableRange.Test(Box(),new(bad,0,0),Zero,new(Identity)).Within);
        Assert.False(MissionOpenFloorUsableRange.Test(Box(),Zero,new(0,bad,0),new(Identity)).Within);
    }
    [Fact]
    public void NonpositiveOrUnprovenProfilesNeverUseAnInventedDefault()
    {
        Assert.Throws<ArgumentException>(()=>new MissionOpenFloorUsableRange.Profile(548,3,0,new(50,40,70),"proof"));
        Assert.Throws<ArgumentException>(()=>new MissionOpenFloorUsableRange.Profile(548,3,float.NaN,new(50,40,70),"proof"));
        Assert.Throws<ArgumentException>(()=>new MissionOpenFloorUsableRange.Profile(548,3,100,new(50,40,70),""));
        Assert.Throws<ArgumentException>(()=>new OpenFloorOrientation("00000000000000000000000000000000"));
    }
    [Fact]
    public void OutwardEnvelopeRoundingDoesNotChangeTheSeparatelyProvenSupportHeight()
    {
        var p=new OpenFloorModelBounds(3,new(-43,-25.01,-42),new(43,25.14,42),-25.003776550292969,"literal conservative envelope plus source support");
        Assert.Equal(2025.003776550293,p.CentreY(2000));Assert.Equal(2000,p.CentreY(2000)+p.SupportMinY);
        Assert.True(p.CentreY(2000)+p.Min.Y<2000); // conservative envelope is retained, not moved upward to fake contact
        Assert.Throws<ArgumentException>(()=>new OpenFloorModelBounds(3,new(-43,-25,-42),new(43,25,42),-26,"outside envelope"));
    }
    [Fact]
    public void RestingHeightComesFromActualMeshMinimumRatherThanInteractionHalfExtents()
    {
        var measured=new OpenFloorModelBounds(548,new(-10,-17,-20),new(10,90,20),"literal measured asymmetric model");
        Assert.Equal(2017,measured.CentreY(2000));var corners=measured.Corners(new(100,2017,300),new(Identity));
        Assert.Equal(8,corners.Count);Assert.Equal(2000,corners.Min(p=>p.Y));Assert.Equal(2107,corners.Max(p=>p.Y));
        Assert.Equal(100,corners.Average(p=>p.X));Assert.Equal(300,corners.Average(p=>p.Z));
    }
    [Fact]
    public void ComputerActionProfilesAreExplicitImmutableAndShareOnlyTheProvenInitialFallback()
    {
        byte[] actions={8,4,5};var profile=MissionOpenFloorUsableRange.Profile.InitialComputerActions(actions,new(43,25,40),"literal three Usable5 components zero beyond stored327");actions[0]=99;
        Assert.Equal(new byte[]{4,5,8},profile.Uses);Assert.Equal(4,profile.Use);Assert.True(profile.AssumesInitialClientConfiguration);Assert.Equal(400,profile.LowerBound);
        foreach(byte use in new byte[]{4,5,8})Assert.Equal(400,MissionOpenFloorUsableRange.Profile.InitialClient400(3,use,new(43,25,1000),"same nonpositive branch bypasses size floor").LowerBound);
        Assert.Throws<ArgumentException>(()=>MissionOpenFloorUsableRange.Profile.InitialComputerActions(new byte[]{3},new(43,25,40),"unproven action"));
        Assert.Throws<ArgumentException>(()=>MissionOpenFloorUsableRange.Profile.InitialComputerActions(Array.Empty<byte>(),new(43,25,40),"unknown action"));
    }}
