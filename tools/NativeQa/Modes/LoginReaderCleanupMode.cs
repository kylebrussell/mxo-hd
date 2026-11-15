namespace NativeQa.Modes;
public sealed class LoginReaderCleanupMode : NativeQaMode
{
    public override string Name => "login-reader-cleanup";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, 23828, 13984,
        Array.Empty<string>(),
        new[] { "UPDATE characters SET district='slums',districtId=1,y=495,rotation=126,healthC=300,healthM=300 WHERE charId=110" },
        Array.Empty<string>(),
        extraQaMetadata: new() { ["LoginReaderCleanupQa"] = true, ["NativeActorHasValidHealth"] = true },
        readyMessages: new[] { "Normal valid CSR110 Mara login/relog only, abilities2/17. No malformed native actor, dev/GM permissions or forced rolls. Error recovery is exercised only by the disposable headless negative-health fixture. See LOGIN-READER-CLEANUP.md." });
}
