using System;
using System.IO;
using System.Text.Json;
using HeadlessClient;
using hds;
using Xunit;
public class MachinesOutsideContinuationTests
{
    static MissionProgress.SavedState Captured()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"tests/hds.Tests/Fixtures/MachinesOutsideUpload-201206.json")))root=root.Parent;
        return JsonSerializer.Deserialize<MissionProgress.SavedState>(File.ReadAllText(Path.Combine(root!.FullName,"tests/hds.Tests/Fixtures/MachinesOutsideUpload-201206.json")),new JsonSerializerOptions{IncludeFields=true})!;
    }
    [Fact] public void ActualUnfinishedPdaCarryMustSurviveDownrideBeforeOutsideAid()
    {
        var before=Captured();Assert.Equal(201206,before.Mission);Assert.Equal(1,before.Phase);Assert.Contains((uint)6820,before.Items.Keys);
        MissionOutsideContinuation.Require(before,Captured());
        var changed=Captured();changed.Phase++;Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Items.Clear();Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Done=Array.Empty<int>();Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Identity="another";Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Exits[0][0]+=1;Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Instance++;Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(before,changed));
    }
    [Fact] public void SubsequentSameBoothUploadMayHaveLegitimateItemGoalChangesButNotAnotherSite()
    {
        var before=Captured();var after=Captured();after.Items.Clear();after.Done=new[]{1,2};after.EverDone=new[]{1,2};
        Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(before,after));
        MissionOutsideContinuation.Require(before,after,preserveGoalsAndItems:false);
        after.Phase++;Assert.Throws<InvalidDataException>(()=>MissionOutsideContinuation.Require(before,after,preserveGoalsAndItems:false));
    }
    [Fact] public void ExistingMachinesTripsKeepCompletedPhaseWaitAndNoRequestHookByDefault()
    {
        var method=typeof(Scenarios).GetMethod("MachinesNextRide",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        Assert.Equal(true,method.GetParameters().Single(p=>p.Name=="waitForPhaseAdvance").DefaultValue);
        Assert.Null(method.GetParameters().Single(p=>p.Name=="beforeRequest").DefaultValue);
    }
}
