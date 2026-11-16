using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using HeadlessClient;
using hds;
using Xunit;
public class ZionOutsideContinuationTests
{
    static MissionProgress.SavedState Captured()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"tests/hds.Tests/Fixtures/ZionOutsideCarry-101208.json")))root=root.Parent;
        return JsonSerializer.Deserialize<MissionProgress.SavedState>(File.ReadAllText(Path.Combine(root!.FullName,"tests/hds.Tests/Fixtures/ZionOutsideCarry-101208.json")),new JsonSerializerOptions{IncludeFields=true})!;
    }
    [Fact] public void ActualEarned101208CarryMustRemainInItsUnfinishedPhase()
    {
        var before=Captured();Assert.Equal(101208,before.Mission);Assert.Equal(0,before.Phase);Assert.Contains((uint)6831,before.Items.Keys);
        ZionOutsideContinuation.Require(before,Captured());
        var changed=Captured();changed.Phase++;Assert.Throws<InvalidDataException>(()=>ZionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Identity="different";Assert.Throws<InvalidDataException>(()=>ZionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Items.Clear();Assert.Throws<InvalidDataException>(()=>ZionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Done=Array.Empty<int>();Assert.Throws<InvalidDataException>(()=>ZionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Exits[0][0]+=1;Assert.Throws<InvalidDataException>(()=>ZionOutsideContinuation.Require(before,changed));
        changed=Captured();changed.Instance++;Assert.Throws<InvalidDataException>(()=>ZionOutsideContinuation.Require(before,changed));
    }
}
