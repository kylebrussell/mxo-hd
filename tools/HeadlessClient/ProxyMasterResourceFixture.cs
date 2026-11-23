using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HeadlessClient;

public static class ProxyMasterResourceFixture
{
    private static readonly Regex Meter = new(@"pet=(\d+) tick=(\d+\.\d+) IS=(\d+\.\d+) cost=(\d+\.\d+)",
        RegexOptions.CultureInvariant);

    public static async Task CheckRateAsync(HeadlessPlayer caster, byte pet, double expectedRate, double expectedCost)
    {
        caster.SkipReceived();
        caster.SendChat("?pet exhaust");
        await caster.WaitForTextAsync("Simulacrum IS exhausted");
        var first = await ReadAsync();
        await Task.Delay(TimeSpan.FromSeconds(4));
        var second = await ReadAsync();
        double elapsed = second.Tick - first.Tick;
        Check.That(elapsed >= 2 && second.Is < 100, $"Pet {pet} has two uncapped resource samples");
        Check.That(Math.Abs((second.Is - first.Is) / elapsed - expectedRate) < 0.01,
            $"Pet {pet} regenerates {expectedRate} IS/s, sampled {first.Is} to {second.Is} over {elapsed}s");
        Console.WriteLine($"PET RESOURCE PASS pet={pet} rate={expectedRate} IS/s cost={expectedCost}.");

        async Task<(double Tick, double Is)> ReadAsync()
        {
            caster.SkipReceived();
            caster.SendChat("?pet meter");
            var response = await caster.WaitForTextAsync("[PET METER]");
            var sample = Meter.Match(Encoding.ASCII.GetString(response.Bytes));
            Check.That(sample.Success && byte.Parse(sample.Groups[1].Value, CultureInfo.InvariantCulture) == pet,
                $"The live meter reports the expected pet {pet}");
            double Value(int index) => double.Parse(sample.Groups[index].Value, CultureInfo.InvariantCulture);
            Check.Equal(expectedCost, Value(4), $"Pet {pet} action cost");
            return (Value(2), Value(3));
        }
    }
}
