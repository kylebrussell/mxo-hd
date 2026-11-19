namespace NativeQa.Modes;

public sealed class StyleChoreographyMode : NativeQaMode
{
    public override string Name => "style-choreography";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 101, 132, 133, 129, 147, 158, 246, 197, 412, 407, 267, 428, 241, 105, 250 },
        24699, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            "UPDATE characters SET healthC=5000,healthM=5000,innerStrC=1500,innerStrM=1500 WHERE charId=110",
            "DELETE FROM inventory WHERE charId=110 AND slot IN (10,11,12,105)",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,2565,10,1,0),(110,692,11,1,0),(110,2503,12,1,0)"
        },
        extraQaMetadata: new() { ["ChoreographyNativeStatus"] = "Pending" },
        readyMessages: new[]
        {
            "Target WinterFrost, Trainman or Blackwoods at the NPC cluster. Use fresh NPCs as needed.",
            "Self Defense 17: Head Butt 197 -> HeadButt 284.",
            "Aikido 101: Tomo Nage 412 -> 751. Punch Reversal 407 -> RevSlamChicken 827/828/829 for Self Defense Speed/Power/Grab.",
            "Karate 132: Sky High Sidekick 267 -> 5937. Kung-Fu 133: Suicidal Butterfly 428 -> 8084.",
            "Equip the revolver from bag 10 and select Handguns 129: stand 21052-21055, Pistol Slide 241 -> 9527.",
            "Equip the rifle from bag 11 and select Rifles 147: stand 21068-21071, Body Shot 105 -> 11364.",
            "Equip the dual SMGs from bag 12 and select Submachine Guns 158: stand 21076-21079, Controlled Burst 250 -> 21082 after the cast.",
            "Log each landed special name and move ID, compare the motion on screen and save the full outgoing RPC. An ordinary miss does not establish special playback."
        });
}
