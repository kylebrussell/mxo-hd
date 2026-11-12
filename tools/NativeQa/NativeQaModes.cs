using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.ObjectModel;
using HeadlessClient;

namespace NativeQa;

/// <summary>Immutable data used by both the read-only manifest and the real fixture executor.</summary>
public sealed class NativeQaModePlan
{
    public IReadOnlyList<ushort> Abilities { get; }
    public int X { get; }
    public int Z { get; }
    public IReadOnlyList<string> BeforeLoadout { get; }
    public IReadOnlyList<string> AfterLoadout { get; }
    public IReadOnlyList<string> AfterHelpers { get; }
    public IReadOnlyDictionary<string,string> EnvironmentOverrides { get; }
    public IReadOnlyDictionary<string,JsonElement> ExtraQaMetadata { get; }
    public IReadOnlyList<string> ReadyMessages { get; }
    public bool SeedDisguiseTool { get; }
    public bool DevCommands { get; }
    public bool GmCharacter110 { get; }
    public bool FixedCallbackRolls { get; }
    public string Bootstrap { get; }
    public bool StealthShieldQa { get; }
    public bool HealthCallbackQa { get; }
    public bool CompileItemsQa { get; }
    public NativeQaModePlan(ushort[] abilities, int x, int z, string[] beforeLoadout, string[] afterLoadout,
        string[] afterHelpers, bool DevCommands=false, bool GmCharacter110=false, bool FixedCallbackRolls=false,
        bool SeedDisguiseTool=false, string Bootstrap="", bool StealthShieldQa=false, bool HealthCallbackQa=false, bool CompileItemsQa=false, Dictionary<string,string>? environmentOverrides=null, Dictionary<string,object?>? extraQaMetadata=null, string[]? readyMessages=null)
    {
        Abilities=Array.AsReadOnly((ushort[])abilities.Clone()); X=x; Z=z;
        BeforeLoadout=Array.AsReadOnly((string[])beforeLoadout.Clone());
        AfterLoadout=Array.AsReadOnly((string[])afterLoadout.Clone());
        AfterHelpers=Array.AsReadOnly((string[])afterHelpers.Clone());
        EnvironmentOverrides=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>(environmentOverrides ?? new(),StringComparer.Ordinal));
        ExtraQaMetadata=new ReadOnlyDictionary<string,JsonElement>((extraQaMetadata ?? new()).ToDictionary(p=>p.Key,p=>
        {
            if(p.Value is not (null or string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal))
                throw new ArgumentException("Extra QA metadata accepts only immutable JSON primitives.",nameof(extraQaMetadata));
            return JsonSerializer.SerializeToElement(p.Value);
        },StringComparer.Ordinal));
        ReadyMessages=Array.AsReadOnly((string[])(readyMessages ?? Array.Empty<string>()).Clone());
        this.SeedDisguiseTool=SeedDisguiseTool;
        this.DevCommands=DevCommands; this.GmCharacter110=GmCharacter110; this.FixedCallbackRolls=FixedCallbackRolls;
        this.Bootstrap=Bootstrap; this.StealthShieldQa=StealthShieldQa; this.HealthCallbackQa=HealthCallbackQa; this.CompileItemsQa=CompileItemsQa;
    }
    /// <summary>Mode metadata can append fields, never replace standard fixture/provenance values.</summary>
    public JsonObject StateWithMetadata(object standard)
    {
        var state=JsonSerializer.SerializeToNode(standard)!.AsObject();
        foreach(var pair in ExtraQaMetadata) state.Add(pair.Key,JsonSerializer.SerializeToNode(pair.Value));
        return state;
    }

}

public sealed record NativeQaBootstrapContext(string OutputDirectory, string[] HelperNames, Dictionary<string,HeadlessPlayer> Helpers);
public abstract class NativeQaMode
{
    public abstract string Name { get; }
    public abstract NativeQaModePlan Plan { get; }
    public virtual Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context) => Task.FromResult(Array.Empty<byte>());
}

public static class NativeQaModes
{
    private static readonly IReadOnlyDictionary<string,NativeQaMode> modes = new ReadOnlyDictionary<string,NativeQaMode>(
        typeof(NativeQaMode).Assembly.GetTypes().Where(t=>!t.IsAbstract && typeof(NativeQaMode).IsAssignableFrom(t))
        .Select(t=>(NativeQaMode)Activator.CreateInstance(t)!).ToDictionary(m=>m.Name,StringComparer.Ordinal));
    public static NativeQaMode Find(string name) => modes.TryGetValue(name,out var mode)?mode:modes[""];
    public static IEnumerable<string> EnvironmentKeys => modes.Values.SelectMany(m=>m.Plan.EnvironmentOverrides.Keys).Distinct(StringComparer.Ordinal);
    public static IEnumerable<string> Names => modes.Keys.Order(StringComparer.Ordinal);
}
