namespace HeadlessClient;

public static class SixthSenseFixture
{
    public const int X = 17000, Y = 495, Z = 2300, NearX = 17800, FarX = 18600;
    public const string Scan = "2e 67 00 00 00 00 00 00 00 00 00 00 00 81 00 00 60";
    public const string Discovery = "81 67 17 00 ?? ?? 3c 02 00 7a 00 95 00 00 00";
    public const string RedPing = "80 c5 05 00 0a 01 00 00 08 00 02";

    public static CharacterSeed At(string handle, int x = X) =>
        new(handle) { X = x, Y = Y, Z = Z, Level = 50 };

    public static void AssertPosition(HeadlessPlayer player, int x)
    {
        var own = player.World.CreationSnapshot().Last(v => v.ViewId == 2);
        Check.That(EngageFoesFixture.Position(own).SequenceEqual(new double[] { x, Y, Z }),
            $"{player.Handle} entered at the intended scan distance");
    }
}
