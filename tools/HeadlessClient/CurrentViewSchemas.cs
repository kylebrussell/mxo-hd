namespace HeadlessClient;
// Per-current-own-life framing metadata ONLY. A deleted view's known schema grants no live object authority.
internal sealed class CurrentViewSchemas
{
    private readonly Dictionary<ushort,ushort> births=new();
    internal void Birth(ViewCreation view)=>births[view.ViewId]=view.Goid;
    internal ushort? Lookup(ushort view,ushort? current)=>current??(births.TryGetValue(view,out var goid)?goid:null);
}
