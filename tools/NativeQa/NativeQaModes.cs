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
        bool SeedDisguiseTool=false, string Bootstrap="", bool StealthShieldQa=false, bool HealthCallbackQa=false, bool CompileItemsQa=false, Dictionary<string,string>? environmentOverrides=null)
    {
        Abilities=Array.AsReadOnly((ushort[])abilities.Clone()); X=x; Z=z;
        BeforeLoadout=Array.AsReadOnly((string[])beforeLoadout.Clone());
        AfterLoadout=Array.AsReadOnly((string[])afterLoadout.Clone());
        AfterHelpers=Array.AsReadOnly((string[])afterHelpers.Clone());
        EnvironmentOverrides=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>(environmentOverrides ?? new(),StringComparer.Ordinal));
        this.SeedDisguiseTool=SeedDisguiseTool;
        this.DevCommands=DevCommands; this.GmCharacter110=GmCharacter110; this.FixedCallbackRolls=FixedCallbackRolls;
        this.Bootstrap=Bootstrap; this.StealthShieldQa=StealthShieldQa; this.HealthCallbackQa=HealthCallbackQa; this.CompileItemsQa=CompileItemsQa;
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
