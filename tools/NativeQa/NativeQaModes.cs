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
    public IReadOnlyList<ushort> HotbarAbilities { get; }
    public IReadOnlyList<ushort> HotbarPageThree { get; }
    public IReadOnlyList<AbilitySweepHotbar.InventoryShortcut> HotbarInventory { get; }
    public bool HotbarWithdraw { get; }
    public bool AllowManualHotbarSetup { get; }
    public bool HasHotbar => HotbarAbilities.Count != 0 || HotbarWithdraw || HotbarInventory.Count != 0;
    public string? MissionFixture { get; }
    public string? TestMobFixture { get; }
    public bool LoadAbilityAncestors { get; }
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
        bool SeedDisguiseTool=false, string Bootstrap="", bool StealthShieldQa=false, bool HealthCallbackQa=false, bool CompileItemsQa=false, Dictionary<string,string>? environmentOverrides=null, Dictionary<string,object?>? extraQaMetadata=null, string[]? readyMessages=null,string? missionFixture=null, string? testMobFixture=null,
        bool loadAbilityAncestors=false, ushort[]? hotbarAbilities=null, bool hotbarWithdraw = false, ushort[]? hotbarPageThree = null,
        AbilitySweepHotbar.InventoryShortcut[]? hotbarInventory = null, bool allowManualHotbarSetup = false)
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
        HotbarAbilities=Array.AsReadOnly((ushort[])(hotbarAbilities ?? Array.Empty<ushort>()).Clone());
        HotbarPageThree=Array.AsReadOnly((ushort[])(hotbarPageThree ?? Array.Empty<ushort>()).Clone());
        HotbarInventory=Array.AsReadOnly(hotbarInventory?.ToArray() ?? Array.Empty<AbilitySweepHotbar.InventoryShortcut>());
        HotbarWithdraw = hotbarWithdraw;
        AllowManualHotbarSetup = allowManualHotbarSetup;
        if (HasHotbar) AbilitySweepHotbar.Validate(HotbarAbilities, HotbarWithdraw, HotbarInventory);
        var hotbarInstructions = !HasHotbar ? Array.Empty<string>() :
            new[] { "Open stock hotbar page 2." }.Concat(HotbarAbilities.Select((id, index) => $"slot {index + 1} = ability {id}"));
        if (HotbarWithdraw) hotbarInstructions = hotbarInstructions.Append($"slot {HotbarAbilities.Count + 1} = Withdraw");
        int itemStart = HotbarAbilities.Count + (HotbarWithdraw ? 1 : 0) + 1;
        hotbarInstructions = hotbarInstructions.Concat(HotbarInventory.Select((item, index) =>
            $"slot {itemStart + index} = {item.Label} (bag slot {item.BagSlot}, GOID {item.Goid})"));
        if (HotbarPageThree.Count != 0)
        {
            if (!HasHotbar) throw new ArgumentException("Page 3 seeding also requires a page 2 plan.", nameof(hotbarPageThree));
            AbilitySweepHotbar.Validate(HotbarPageThree);
            hotbarInstructions = hotbarInstructions.Append("Stock hotbar page 3 is also seeded.")
                .Concat(HotbarPageThree.Select((id, index) => $"page 3 slot {index + 1} = ability {id}"));
        }
        ReadyMessages=Array.AsReadOnly(hotbarInstructions.Concat(readyMessages ?? Array.Empty<string>()).ToArray());
        if(missionFixture!=null&&(Path.GetFileName(missionFixture)!=missionFixture||!missionFixture.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A mission fixture must name one XML file.",nameof(missionFixture));
        MissionFixture=missionFixture;
        if (testMobFixture != null && (Path.GetFileName(testMobFixture) != testMobFixture ||
            !testMobFixture.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A test mob fixture must name one CSV file.", nameof(testMobFixture));
        TestMobFixture = testMobFixture;
        LoadAbilityAncestors = loadAbilityAncestors;
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

public sealed record NativeQaBootstrapContext(string OutputDirectory, string[] HelperNames, Dictionary<string,HeadlessPlayer> Helpers,
    ServerAddress? Server = null, ThrowawayDatabase? OwnedDatabase = null, ServerInstance? ActualServer = null,
    string? RepositoryRoot = null, HeadlessPlayer? StockProbe = null)
{
    public ServerAddress Address => Server ?? ActualServer?.ConfiguredAddress ?? new ServerAddress("127.0.0.1",11000,10000,10000);
    public void Deconstruct(out string output,out string[] names,out Dictionary<string,HeadlessPlayer> helpers,out ServerAddress? server)
    {output=OutputDirectory;names=HelperNames;helpers=Helpers;server=Server;}
}
public abstract class NativeQaMode
{
    public abstract string Name { get; }
    public abstract NativeQaModePlan Plan { get; }
    public virtual Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context) => Task.FromResult(Array.Empty<byte>());
    internal virtual bool HelperConnectionReady(HeadlessPlayer helper)
    {
        var life = helper.World.CurrentOwnViewLife();
        return life.Current && life.OwnViewReady;
    }
}

public static class NativeQaModes
{
    private static readonly IReadOnlyDictionary<string,NativeQaMode> modes = new ReadOnlyDictionary<string,NativeQaMode>(
        typeof(NativeQaMode).Assembly.GetTypes().Where(t=>!t.IsAbstract && typeof(NativeQaMode).IsAssignableFrom(t))
        .Select(t=>(NativeQaMode)Activator.CreateInstance(t)!).ToDictionary(m=>m.Name,StringComparer.Ordinal));
    public static NativeQaMode Find(string name) => modes.TryGetValue(name,out var mode)?mode:modes[""];
    public static NativeQaMode RequireKnown(string name) => modes.TryGetValue(name,out var mode)?mode:
        throw new ArgumentException("Unknown NativeQa mode: "+name,nameof(name));
    public static IEnumerable<string> EnvironmentKeys => modes.Values.SelectMany(m=>m.Plan.EnvironmentOverrides.Keys).Distinct(StringComparer.Ordinal);
    public static IEnumerable<string> Names => modes.Keys.Order(StringComparer.Ordinal);
}
