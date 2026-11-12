namespace NativeQa.Modes;
/// <summary>Diagnostic-only C1 profile: no new action handler or gameplay override.</summary>
public sealed class ClientActionsResearchMode : NativeQaMode
{
    public override string Name=>"client-actions-research";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17},23828,13984,
        Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>());
}
