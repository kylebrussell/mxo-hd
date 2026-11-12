using hds;

namespace hds.Tests;

public class WriterSessionTests
{
    [Fact]
    public void WriterUsesItsOwnWireIdentityToolAndLinkedRecord()
    {
        var body = new byte[41]; BitConverter.GetBytes((ushort)970).CopyTo(body, 0);
        body[35] = 2; body[36] = 1;
        Assert.Equal(((byte)2, true), CraftingSession.Parse(body));
        body[36] = 0; Assert.Equal(((byte)2, false), CraftingSession.Parse(body));
        BitConverter.GetBytes((ushort)37).CopyTo(body, 0); Assert.Null(CraftingSession.Parse(body));
        Assert.True(CraftingSession.Handles(970)); Assert.False(CraftingSession.Handles(37));
        Assert.Equal(531, CraftingSession.ToolFor(970)); Assert.Equal(1271, CraftingSession.ToolFor(969));
        var state = new CraftingSession.State { Ability=970, Record=0x123, Slot=2 };
        Assert.Equal("80-BC-1D-00-23-01-00-CA-03-00-00-C8-03-00-00-00-00-00-00-00-00-00",
            BitConverter.ToString(CraftingSession.Record(state, false)));
        Assert.Equal(1, CraftingSession.Record(state, true)[17]);
        Assert.Equal(0, AbilityCosts.Cost(970));
    }

    [Fact]
    public void ScopedWriterClosePreservesCompilerAndCleanupRestoresOnlyTypingStance()
    {
        var c = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData();
        c.playerData.craftingSession = new CraftingSession.State { Ability=969 };
        Assert.False(CraftingSession.End(c, false, 970));
        Assert.NotNull(c.playerData.craftingSession);
        c.playerData.craftingSession = new CraftingSession.State { Ability=970 };
        c.playerData.stance = CraftingSession.TypingStance;
        Assert.True(CraftingSession.End(c, false, 970));
        Assert.Null(c.playerData.craftingSession); Assert.Equal(0, c.playerData.stance);
        c.playerData.craftingSession = new CraftingSession.State { Ability=970 };
        c.playerData.stance = 10;
        Assert.True(CraftingSession.End(c, false)); Assert.Equal(10, c.playerData.stance);
        Assert.False(CraftingSession.End(c, false));
    }
}
