namespace HeadlessClient;

/// <summary>Disposable radius boundary actors shared by read-only native plans and the fresh wire scenario.</summary>
public static class ResistDamageFixture
{
    public static string TargetsSql(long near,long expanded,long outside,long other,int x,int z) =>
        $"UPDATE characters SET x=CASE charId WHEN {near} THEN {x+600} WHEN {expanded} THEN {x+800} WHEN {outside} THEN {x+900} ELSE {x+1000} END,z={z},y=495,alignment=1,pvpflag=0,healthC=100,healthM=5000 WHERE charId IN ({near},{expanded},{outside},{other})";
}
