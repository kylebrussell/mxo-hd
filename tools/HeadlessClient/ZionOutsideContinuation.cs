using System.Text.Json;
using hds;
namespace HeadlessClient;

// QA continuation identity only. Natural entered/departed/timer changes are not normalized or written.
public static class ZionOutsideContinuation
{
    public static void Require(MissionProgress.SavedState before,MissionProgress.SavedState after)
    {
        int item=before.Mission==101208?6831:before.Mission==101212?6835:0;
        if(item==0||!before.Items.ContainsKey((uint)item))throw new InvalidDataException("Expected earned pre-Upload item carry");
        MissionOutsideContinuation.Require(before,after);
    }
}
