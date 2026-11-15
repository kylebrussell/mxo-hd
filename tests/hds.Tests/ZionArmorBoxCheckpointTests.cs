using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text;
using HeadlessClient;
using NativeQa;
namespace hds.Tests;

public class ZionArmorBoxCheckpointTests
{
    [Fact]
    public void VerbatimEarnedCaptureHasNoItemsOrBeyondCaptureReceipt()
    {
        var state=ZionArmorBoxCheckpoint.Verified();Assert.Equal(101216,state.Mission);Assert.Equal(3,state.Phase);Assert.Empty(state.Items);
        Assert.Equal("d57dd5e3a9e9470cba8b369e6bb93eb8",state.Identity);Assert.Equal(1794736946,state.Started);Assert.All(state.Mobs,m=>Assert.Equal(2150,m.Health));
        Assert.DoesNotContain(101216,ZionArmorBoxCheckpoint.EarlierReceipts);Assert.DoesNotContain(101217,ZionArmorBoxCheckpoint.EarlierReceipts);
        Assert.Contains(101215,ZionArmorBoxCheckpoint.EarlierReceipts);Assert.Single(state.Objects);Assert.Equal(0x0d1000d0u,state.Objects[0].OpenFloor.SourceKey);
        Assert.Equal(new[]{28413.453786483045,1695,14219.695388506345},state.Objects[0].OpenFloor.Route.Take(3));
    }
    [Fact]
    public void CaptureAndManifestFailClosedBeforeSql()
    {
        byte[] payload=ZionArmorBoxCheckpoint.Payload;var a=typeof(ZionArmorBoxCheckpoint).Assembly;using var stream=a.GetManifestResourceStream(a.GetManifestResourceNames().Single(n=>n.EndsWith("zion-armor-box.manifest.json")))!;using var mem=new MemoryStream();stream.CopyTo(mem);byte[] manifest=mem.ToArray();
        Assert.NotNull(ZionArmorBoxCheckpoint.Verify(payload,manifest));
        var changed=payload.ToArray();changed[1]^=1;Assert.Throws<InvalidDataException>(()=>ZionArmorBoxCheckpoint.Verify(changed,manifest));
        Assert.Throws<InvalidDataException>(()=>ZionArmorBoxCheckpoint.Verify(payload[..^1],manifest));
        string json=Encoding.UTF8.GetString(manifest).Replace("\"Phase\": 3","\"Phase\": 2");Assert.Throws<InvalidDataException>(()=>ZionArmorBoxCheckpoint.Verify(payload,Encoding.UTF8.GetBytes(json)));
    }
    [Theory][InlineData("")][InlineData("reality_hd")][InlineData("mxotest_")][InlineData("mxotest_bad;DROP")]
    public void NonownedSchemaRefuses(string schema)=>Assert.Throws<InvalidDataException>(()=>ZionArmorBoxCheckpoint.RequireOwnedSchema(schema));
    [Fact]
    public void SourceAndStrictHexCannotNormalizeCorruptEvidence()
    {
        string h=new('A',64);ZionArmorBoxCheckpoint.RequireSource(h,h.ToLowerInvariant());
        Assert.Throws<InvalidDataException>(()=>ZionArmorBoxCheckpoint.RequireSource(null,h));Assert.Throws<InvalidDataException>(()=>ZionArmorBoxCheckpoint.RequireSource(new('B',64),h));
        Assert.Equal("{\"quote\":\"a\\b\"}",ZionArmorBoxCheckpoint.StrictUtf8Hex(Convert.ToHexString(Encoding.UTF8.GetBytes("{\"quote\":\"a\\b\"}"))));
        Assert.Throws<InvalidDataException>(()=>ZionArmorBoxCheckpoint.StrictUtf8Hex("ABC"));Assert.Throws<DecoderFallbackException>(()=>ZionArmorBoxCheckpoint.StrictUtf8Hex("FF"));
    }
    [Fact]
    public void OptionalContextKeepsDefaultsIdentityAndFourFieldDeconstruction()
    {
        var helpers=new Dictionary<string,HeadlessPlayer>();var context=new NativeQaBootstrapContext("proof",Array.Empty<string>(),helpers);
        Assert.Equal(new ServerAddress("127.0.0.1",11000,10000,10000),context.Address);Assert.Null(context.OwnedDatabase);Assert.Null(context.ActualServer);
        var (output,names,actualHelpers,address)=context;Assert.Equal("proof",output);Assert.Same(helpers,actualHelpers);Assert.Null(address);
        var db=(ThrowawayDatabase)RuntimeHelpers.GetUninitializedObject(typeof(ThrowawayDatabase));var server=(ServerInstance)RuntimeHelpers.GetUninitializedObject(typeof(ServerInstance));
        var attached=context with {OwnedDatabase=db,ActualServer=server};Assert.Same(db,attached.OwnedDatabase);Assert.Same(server,attached.ActualServer);
        Assert.Throws<InvalidDataException>(()=>ZionArmorBoxCheckpoint.RequireContext(context));
        // Reject the nonowned service before dereferencing or contacting any fake server.
        typeof(ThrowawayDatabase).GetField("<Name>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(db,"reality_hd");
        Assert.Throws<InvalidDataException>(()=>ZionArmorBoxCheckpoint.RequireContext(attached));
    }
    [Fact]
    public void ModeHasRealHookWithoutNpcRewritesOrProgressBeyondSnapshot()
    {
        var mode=new NativeQa.Modes.ZionArmorBoxCheckpointMode();Assert.True(NativeQaBootstrapChecks.HasCustomHook(mode));Assert.False(mode.Plan.DevCommands);Assert.False(mode.Plan.GmCharacter110);
        Assert.Equal(28500,mode.Plan.X);Assert.Equal(14400,mode.Plan.Z);Assert.Empty(mode.Plan.AfterHelpers);
    }
}
