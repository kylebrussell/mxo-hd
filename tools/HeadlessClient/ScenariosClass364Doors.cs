namespace HeadlessClient;

public static partial class Scenarios
{
    // Reuse the previously audited source/world/quaternion fixture; no mission source is forced.
    private static async Task Class364Doors(ScenarioContext context)
    {
        bool passed=false;string? failure=null;
        var fixture=NativeWorldDoorFixtures.Medium03Prefab with { Mode="class364-doors" };
        try
        {
            await NativeWorldDoorDiagnostic(context,fixture);
            passed=true;
        }
        catch(Exception ex){failure=ex.ToString();throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-class364-doors-"+context.Server.DatabaseName);
            string packet=Path.Combine(context.Server.WorkingDirectory,"PacketLog.txt");
            string? packetSha=null,packetFailure=null;
            try
            {
                using var source=new FileStream(packet,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                using var copy=new MemoryStream();await source.CopyToAsync(copy);byte[] bytes=copy.ToArray();
                await File.WriteAllBytesAsync(stem+"-PacketLog.txt",bytes);
                packetSha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            }
            catch(Exception ex){packetFailure=ex.ToString();}
            await File.WriteAllTextAsync(stem+".json",System.Text.Json.JsonSerializer.Serialize(new
            {
                Case="class364-doors",Passed=passed&&packetFailure==null,ProtocolCasePassed=passed,Failure=failure,context.Server.ProcessId,Schema=context.Server.DatabaseName,
                context.Server.WorkingDirectory,context.Server.SourceAssembly,
                SourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),
                PacketLog=packet,PacketLogSha256=packetSha,PacketLogFailure=packetFailure,
                Fixture=fixture,NativeRenderingProven=false,NativePassageProven=false
            },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            await File.WriteAllTextAsync(stem+"-debug.log",context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
            if(passed&&packetFailure!=null)throw new InvalidOperationException("Class364 packet proof was not retained: "+packetFailure);
        }
    }
}
