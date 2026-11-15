using HeadlessClient;
namespace hds.Tests;
public class ExpectedServerDiagnosticsTests
{
    [Fact]
    public void OnlyTheExactOwnedFailureIsAcknowledged()
    {
        var diagnostics = new ExpectedServerDiagnostics();
        const string expected = "[CLIENT] Packet handling failed for charId 1001: System.OverflowException: UInt16";
        diagnostics.Acknowledge(expected);
        Assert.Empty(diagnostics.Unacknowledged(new[] { expected }));
        Assert.Single(diagnostics.Unacknowledged(new[] { expected, expected }));
        var other = new[] { expected.Replace("1001", "1002"), expected.Replace("OverflowException", "InvalidOperationException"),
            expected + " additional failure", "[CLIENT] Packet handling failed" };
        Assert.Equal(other, diagnostics.Unacknowledged(other));
        // Inspecting the same log twice does not mutate the accepted occurrence count.
        Assert.Empty(diagnostics.Unacknowledged(new[] { expected }));
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("first\nsecond")]
    [InlineData("first\rsecond")]
    public void EmptyOrMultilineSuppressionIsRejected(string value) =>
        Assert.Throws<ArgumentException>(() => new ExpectedServerDiagnostics().Acknowledge(value));

    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public void NonpositiveCountsAreRejected(int count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpectedServerDiagnostics().Acknowledge("exact", count));
}
