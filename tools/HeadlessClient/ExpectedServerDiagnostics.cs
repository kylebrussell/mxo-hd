namespace HeadlessClient;

/// <summary>Only explicitly verified, exact single-line diagnostics can be acknowledged by a scenario.</summary>
public sealed class ExpectedServerDiagnostics
{
    private readonly Dictionary<string, int> lines = new(StringComparer.Ordinal);
    public void Acknowledge(string line, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Contains('\n') || line.Contains('\r'))
            throw new ArgumentException("Expected diagnostic must be one exact nonempty line.", nameof(line));
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        lines[line] = checked(Count(line) + count);
    }
    public int Count(string line) => lines.GetValueOrDefault(line);
    public IEnumerable<string> Unacknowledged(IEnumerable<string> observed)
    {
        var remaining = new Dictionary<string, int>(lines, StringComparer.Ordinal);
        foreach (string line in observed)
        {
            if (remaining.GetValueOrDefault(line) > 0) remaining[line]--;
            else yield return line;
        }
    }
}
