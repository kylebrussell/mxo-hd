using System.Text;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace hds.Tests;

public class ZionAfter1212CheckpointPayloadTests
{
    [Fact] public void StoredEscapedApostropheMatchesTheActualAngeloCreationName()
    {
        const string raw="{\"Mission\":101216,\"Phase\":0,\"Mobs\":[{\"Npc\":3,\"Name\":\"Angelo O\\u0027Nanski\"}]}";
        string hex=Convert.ToHexString(Encoding.UTF8.GetBytes(raw));
        var current=ZionAfter1212CheckpointPayload.Read(hex)!;
        Assert.Equal("Angelo O'Nanski",Assert.Single(current.Mobs).Name);
        // mysql -N -B doubles the literal JSON backslash in ordinary text output.
        var batch=JsonSerializer.Deserialize<MissionProgress.SavedState>(raw.Replace("\\","\\\\"),new JsonSerializerOptions{IncludeFields=true})!;
        Assert.Equal("Angelo O\\u0027Nanski",Assert.Single(batch.Mobs).Name);
        Assert.NotEqual(batch.Mobs[0].Name,current.Mobs[0].Name);
        Assert.Equal(raw,ZionAfter1212CheckpointPayload.Raw(hex));
    }
    [Fact] public void HexPreservesEscapesAndUnicodeWithoutUnescapingLiteralContentTwice()
    {
        const string name="Literal\\u0027 and a quote\"\tline\né";
        var state=new MissionProgress.SavedState{Mission=101216,Mobs=new(){new(){Name=name}}};
        string raw=JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true});
        Assert.Equal(name,ZionAfter1212CheckpointPayload.Read(Convert.ToHexString(Encoding.UTF8.GetBytes(raw)))!.Mobs[0].Name);
        Assert.Null(ZionAfter1212CheckpointPayload.Read("NULL"));
        Assert.Null(ZionAfter1212CheckpointPayload.Read(null));
        Assert.Throws<FormatException>(()=>ZionAfter1212CheckpointPayload.Read("not-hex"));
        Assert.Throws<DecoderFallbackException>(()=>ZionAfter1212CheckpointPayload.Read("FF"));
    }
}
