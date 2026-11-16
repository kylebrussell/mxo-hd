namespace HeadlessClient;

// A completed final Upload can commit and publish its summary before the next
// phase tick logs "goals done". This observer grants no movement/door authority.
public static class ZionFinalCompletionObservation
{
    public static bool Matches(int phase,int phaseCount,string persistedPhase,long receiptCount,
        IReadOnlyList<byte[]> currentMissionSummaries,uint experience,uint information)
    {
        if(phaseCount<=0||phase<0||phase!=phaseCount-1||persistedPhase!="NULL"||receiptCount!=1||
            currentMissionSummaries.Count!=1)return false;
        var summary=currentMissionSummaries[0];
        if(summary.Length<2||summary[0]!=0x80||summary[1]!=0xa2)return false;
        try{ZionAfter1212Fixture.RequireCompletion(currentMissionSummaries,experience,information);return true;}
        catch(InvalidDataException){return false;}
    }
}
