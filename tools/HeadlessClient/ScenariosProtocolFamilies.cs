using MxoHd.PacketResearch;

namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ProtocolFamilyCanary(ScenarioContext context)
    {
        var actor=await context.AddPlayerAsync(new CharacterSeed("FamilyActor"){Level=50});
        await LoadAbilitiesAsync(context,actor,6);
        var observer=(await context.AddPlayersInWorldAsync(new CharacterSeed("FamilyObserver"){X=17100,Level=50}))[0];
        await actor.EnterWorldAsync();
        var creation=await observer.WaitForViewNamedAsync("FamilyActor");
        Check.Equal("FamilyActor",creation.Name!,"03 creation schema owns the character-name bytes");
        Check.That(creation.Attributes.ContainsKey("Position"),"03 variable mask decodes position separately");
        Cast(actor,6);
        await actor.WaitForRpcAsync(ModPattern(6,180));
        await actor.WaitForRpcAsync(ModPattern(6,180,true),TimeSpan.FromSeconds(30));
        var records=actor.World.RpcSnapshot().Where(r=>r.Opcode==0xbc).ToArray();
        var decoded=records.Select(r=>ManageBonusRecord.TryDecode(r.Bytes,out var n)?n:null).ToArray();
        Check.That(decoded.All(r=>r!=null),"Every fresh 80bc has the native22-byte contract");
        var on=decoded.Single(r=>r!.SourceId==6&&r.TargetId==180&&r.AddRemoveType==0)!;
        var off=decoded.Single(r=>r!.SourceId==6&&r.TargetId==180&&r.AddRemoveType==1)!;
        Check.Equal(on.Locator,off.Locator,"Expiry removes the same native locator");
        Check.Equal(235,on.SignedValue,"Captured Consistent Technique value");
        Check.Equal(20f,on.Duration,"Captured Consistent Technique duration");
        Check.That(on.ValueBits==off.ValueBits&&on.DurationBits==off.DurationBits,"Off retains raw field bits");
        Check.Equal(0,actor.World.Problems.Count,"Actor protocol errors");
        Check.Equal(0,observer.World.Problems.Count,"Observer protocol errors");
        Console.WriteLine($"  Native80bc decoded {decoded.Length}; type={on.Type:x2} locator={on.Locator:x8}; 03 profile={creation.Name}");
    }
}
