using HeadlessClient;
using System.Text.Json;

namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class StealthShieldsMode : NativeQaMode
{
    public override string Name => "stealth-shields";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,1110,1125,217,1111,1116,1112,49 }, 24800, -1498,
        new string[] {  },
        new string[] {  },
        new string[] { @"UPDATE characters SET alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114); DELETE FROM inventory WHERE charId IN (110,6111,6112,6113,6114) AND slot=1; INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,531,1,1,0),(6111,531,1,1,0),(6112,531,1,1,0),(6113,531,1,1,0),(6114,531,1,1,0)" },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "stealth-shield-217", StealthShieldQa: true, HealthCallbackQa: false, CompileItemsQa: false);
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach(string name in context.HelperNames)
        {
            var helper=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),name,name);
            context.Helpers.Add(name,helper); await helper.EnterWorldAsync();
        }
        byte[] ack=await StealthShieldFixture.ActivateTargetAsync(context.Helpers["QAEnemy"]);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"shield-ready.json"),JsonSerializer.Serialize(new {
            Target="QAEnemy",Organization=2,PvpFlag=true,Parent=217,Child=624,ModifierAck=WireFormat.ToHex(ack),At=DateTime.UtcNow }));
        return ack;
    }
}
