using hds.databases;
namespace hds.Tests;
public class PersistedHeadingTests
{
    [Fact]
    public void AllProtocolBytesAndLegacyFractionalTruncationArePreserved()
    {
        for(int value=0;value<=255;value++)
        {
            Assert.Equal((byte)value,MyWorldDbAccess.PersistedYawInterval(value));
            Assert.Equal((byte)value,MyWorldDbAccess.PersistedYawInterval((decimal)value));
        }
        foreach(decimal value in new[]{0.99m,1.99m,127.99m,192.9m,255.999999999999999999999999m})
            Assert.Equal((byte)value,MyWorldDbAccess.PersistedYawInterval(value));
    }
    [Theory]
    [InlineData(-1.5708)] // Actual Medium01 native login failure: source radians persisted as a byte.
    [InlineData(-1)] [InlineData(-256)] [InlineData(256)] [InlineData(65536)]
    [InlineData(double.MinValue)] [InlineData(double.MaxValue)]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void InvalidPersistedNumbersUseNeutralHeadingWithoutThrowing(double value)
        => Assert.Equal((byte)0,MyWorldDbAccess.PersistedYawInterval(value));
    [Fact]
    public void InvalidDatabaseRepresentationsCannotAbortWorldLogin()
    {
        foreach(object value in new object[]{DBNull.Value,"not a heading",decimal.MinValue,decimal.MaxValue})
            Assert.Equal((byte)0,MyWorldDbAccess.PersistedYawInterval(value));
        Assert.Equal((byte)0,MyWorldDbAccess.PersistedYawInterval(null!));
    }
}
