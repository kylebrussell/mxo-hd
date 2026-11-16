using hds;
namespace HeadlessClient;
// QA-only: caller's ordinary Ride must return without waiting for mission phase advance.
// Requires271's exact typed transport receipt helper. Never writes checkpoint or creates authority.
public static class MissionOutsideUploadProof
{
    public sealed record Token(MissionProgress.SavedState Checkpoint,OwnViewLifeSnapshot Life);
    public static void RequireCurrent(Token token,MissionProgress.SavedState current,OwnViewLifeSnapshot life)
    {
        if(!token.Life.Current||!life.Current||!life.OwnViewReady||!ReferenceEquals(token.Life.Owner,life.Owner)||token.Life.Life!=life.Life)
            throw new InvalidDataException("Outside continuation owner/life changed");
        MissionOutsideContinuation.Require(token.Checkpoint,current,preserveGoalsAndItems:false);
    }
    public static async Task<Token> BeforeOutsideAsync(HeadlessPlayer player,MissionProgress.SavedState before,
        Func<Task<MissionProgress.SavedState>> readCurrent,Func<Task> ordinaryUnfinishedDown,
        double[] savedLobbyStanding,string packetPath,Action<object>? note=null,bool returningInside=false)
    {
        if(savedLobbyStanding?.Length!=3||!savedLobbyStanding.All(double.IsFinite))throw new InvalidDataException("Exact saved lobby Standing required");
        var life=player.World.CurrentOwnViewLife();if(!life.Current||!life.OwnViewReady)throw new InvalidDataException("Current own life required before downride");
        int sections=player.World.ViewSnapshot().Count,packetLine=InteriorAdmissionDeliveryProof.Read(packetPath).Count(c=>c=='\n');
        await ordinaryUnfinishedDown();
        NpcInteriorComputerState.Teleport? teleport=null;
        await player.WaitUntilAsync(()=>{try{teleport=NpcInteriorComputerState.FreshTeleport(player.World.CurrentOwnViewLife(),life,sections,savedLobbyStanding);return true;}catch(InvalidDataException){return false;}},"Actual own-life ordinary downride before outside aid",TimeSpan.FromSeconds(15));
        MissionMidphaseReturnFixture.Arrival? receipt=null;
        await player.WaitUntilAsync(()=>{try{receipt=MissionMidphaseReturnFixture.RequireArrival(InteriorAdmissionDeliveryProof.Read(packetPath),player.CharId,packetLine,teleport!);return true;}catch(InvalidDataException){return false;}},"Exact client ACK before outside aid",TimeSpan.FromSeconds(10));
        var after=await readCurrent();MissionOutsideContinuation.Require(before,after);
        var token=new Token(after,player.World.CurrentOwnViewLife());RequireCurrent(token,after,life);
        note?.Invoke(new{Kind=returningInside?"Ordinary return up and ACK before inside aid":"Ordinary unfinished down and ACK before outside aid",before.Mission,before.Phase,Teleport=teleport,Receipt=receipt,Checkpoint=after,MovementAid=false});return token;
    }
}
