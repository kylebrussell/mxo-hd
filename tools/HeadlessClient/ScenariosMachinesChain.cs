namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MachinesPrimaChain(ScenarioContext context)
    {
        await RunPrimaChain(context,"MachinesChain",2,
            MachinesChainFixture.Missions,MachinesChainFixture.Prerequisites,"machines_*.xml");
        // Preserve successful protocol diagnostics before the disposable server is torn down.
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-machines-chain-success-debug.log"),
            context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
    }
}
