namespace HeadlessClient;
public static class MissionReceiptSeedGuard
{
    public static void ValidateReceipts(IReadOnlyList<int> requested,IReadOnlyList<int> prior,int next)
    {
        ArgumentNullException.ThrowIfNull(requested);ArgumentNullException.ThrowIfNull(prior);
        if(requested.Count==0||requested.Any(id=>id<=0)||prior.Any(id=>id<=0)||next<=0||
            requested.Distinct().Count()!=requested.Count||prior.Distinct().Count()!=prior.Count||
            requested.Intersect(prior).Any()||requested.Contains(next)||prior.Contains(next))
            throw new ArgumentException("Earlier receipts, requested chain and next offer must be positive, unique and disjoint");
    }
    public static CharacterSeed BeforeMargin(CharacterSeed seed,IReadOnlyList<int> requested,IReadOnlyList<int> prior,int next)
    {
        ValidateReceipts(requested,prior,next);
        if(seed.CompletedMissions?.Count>0)throw new ArgumentException("The profile already has prior receipts");
        return seed with {CompletedMissions=prior.ToArray()};
    }
}
