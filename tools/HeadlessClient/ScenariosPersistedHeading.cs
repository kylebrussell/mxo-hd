namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task PersistedHeadingLogin(ScenarioContext context)
    {
        foreach(var test in new[]{(0d,(byte)0),(192d,(byte)192),(255d,(byte)255),(-1.5708d,(byte)0),(256d,(byte)0),(65536d,(byte)0)})
        {
            // Seed before AddPlayerAsync's initial UDP login, where SetPlayerValues reads the persisted row.
            var seed=new CharacterSeed("Heading"+test.Item2+"Case"+Math.Abs(test.Item1).ToString(System.Globalization.CultureInfo.InvariantCulture).Replace('.','x')){Rotation=test.Item1};
            if(test.Item1==192)seed=seed with{District="medium01",DistrictId=14,X=39250,Y=-953,Z=27000};
            var p=await context.AddPlayerAsync(seed);
            await p.EnterWorldAsync();
            var self=p.World.CreationSnapshot().Last(c=>c.ViewId==2);
            Check.That(self.Attributes.TryGetValue("YawInterval",out var yaw),"self creation carries protocol yaw");
            Check.Equal(test.Item2,yaw![0],"persisted heading preserves bytes or uses neutral invalid-row fallback");
            Check.Equal(0,p.World.Problems.Count,"world login heading parser");
        }
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-persisted-heading-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  persisted heading: actual pre-login rows0/192/255/-1.5708/256/65536 -> valid world entry and exact yaw bytes0/192/255/0/0/0; no exception.");
    }
}
