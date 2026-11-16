using System.Globalization;
using System.Text.RegularExpressions;
using hds;
namespace HeadlessClient;
// QA-only request-boundary proof. No state writes or replacement transport authority.
public static class MissionOutsideUploadProof
{
    public sealed record Token(MissionProgress.SavedState Checkpoint,OwnViewLifeSnapshot Life);
    public sealed class RequestBoundary
    {
        int marks;
        public void Mark(Action capture){if(marks!=0)throw new InvalidDataException("Ordinary ride request marked twice");marks=1;capture();}
        public void RequireMarked(){if(marks!=1)throw new InvalidDataException("Ordinary ride did not mark its actual wire request");}
    }
    public static void RequirePhysicalTrip(string afterRequest,double[] expected)
    {
        if(expected?.Length!=3||!expected.All(double.IsFinite))throw new InvalidDataException("Exact saved target Standing required");
        var trips=Regex.Matches(afterRequest,@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)");
        if(!trips.Cast<Match>().Any(m=>Enumerable.Range(1,3).Select(i=>double.Parse(m.Groups[i].Value,CultureInfo.InvariantCulture)).SequenceEqual(expected)))
            throw new InvalidDataException("No matching physical mission trip committed after the actual door request");
    }
    public static void RequireCurrent(Token token,MissionProgress.SavedState current,OwnViewLifeSnapshot life)
    {
        if(!token.Life.Current||!life.Current||!life.OwnViewReady||!ReferenceEquals(token.Life.Owner,life.Owner)||token.Life.Life!=life.Life)
            throw new InvalidDataException("Outside continuation owner/life changed");
        MissionOutsideContinuation.Require(token.Checkpoint,current,preserveGoalsAndItems:false);
    }
    public static async Task<Token> BeforeOutsideAsync(HeadlessPlayer player,MissionProgress.SavedState before,
        Func<Task<MissionProgress.SavedState>> readCurrent,Func<Action,Task> ordinaryRide,
        double[] savedTargetStanding,string packetPath,Func<string> debugLog,Action<object>? note=null,bool returningInside=false)
    {
        if(savedTargetStanding?.Length!=3||!savedTargetStanding.All(double.IsFinite))throw new InvalidDataException("Exact saved target Standing required");
        var originalLife=player.World.CurrentOwnViewLife();if(!originalLife.Current||!originalLife.OwnViewReady)throw new InvalidDataException("Current own life required before ride");
        OwnViewLifeSnapshot? requestLife=null;int sections=-1,packetLine=-1,logOffset=-1;var boundary=new RequestBoundary();
        void Mark()=>boundary.Mark(()=>
        {
            var now=player.World.CurrentOwnViewLife();
            if(!originalLife.Current||!now.Current||!now.OwnViewReady||!ReferenceEquals(originalLife.Owner,now.Owner)||originalLife.Life!=now.Life)throw new InvalidDataException("Ride owner/life changed before wire request");
            requestLife=now;sections=player.World.ViewSnapshot().Count;packetLine=InteriorAdmissionDeliveryProof.Read(packetPath).Count(c=>c=='\n');logOffset=debugLog().Length;
        });
        // Caller invokes Mark AFTER its approach and checkpoint recheck, immediately BEFORE80c8.
        await ordinaryRide(Mark);boundary.RequireMarked();
        if(requestLife==null||sections<0||packetLine<0||logOffset<0)throw new InvalidDataException("Incomplete ordinary request boundary");
        string log=debugLog();if(log.Length<logOffset)throw new InvalidDataException("Mission trip log replaced during request");
        RequirePhysicalTrip(log[logOffset..],savedTargetStanding); // Ground/no-op door cannot qualify through approach TP.
        NpcInteriorComputerState.Teleport? teleport=null;
        await player.WaitUntilAsync(()=>{try{teleport=NpcInteriorComputerState.FreshTeleport(player.World.CurrentOwnViewLife(),requestLife,sections,savedTargetStanding);return true;}catch(InvalidDataException){return false;}},"Actual post-request own-life ordinary ride",TimeSpan.FromSeconds(15));
        MissionMidphaseReturnFixture.Arrival? receipt=null;
        await player.WaitUntilAsync(()=>{try{receipt=MissionMidphaseReturnFixture.RequireArrival(InteriorAdmissionDeliveryProof.Read(packetPath),player.CharId,packetLine,teleport!);return true;}catch(InvalidDataException){return false;}},"Exact post-request client ACK before movement aid",TimeSpan.FromSeconds(10));
        var after=await readCurrent();MissionOutsideContinuation.Require(before,after);
        var token=new Token(after,player.World.CurrentOwnViewLife());RequireCurrent(token,after,requestLife);
        note?.Invoke(new{Kind=returningInside?"Ordinary return up and ACK before inside aid":"Ordinary unfinished down and ACK before outside aid",before.Mission,before.Phase,ViewCursor=sections,PacketLine=packetLine,LogOffset=logOffset,Teleport=teleport,Receipt=receipt,Checkpoint=after,MovementAid=false});return token;
    }
}
