using hds;
using hds.world.Structures;
using HeadlessClient;

namespace hds.Tests;

public class SubwayStateFramingTests
{
    [Theory]
    [InlineData("824c22e2420304000102be0b0000000004000102be0b000000000000")]
    [InlineData("82cc45e3420304000102be0b0000000004000102be0b000000000000")]
    public void BothNativeTerminalPacketsContainAnEarlyTerminator(string terminal)
    {
        byte[] packet=Convert.FromHexString(terminal);
        Assert.Equal(3,packet[5]);
        Assert.Throws<FormatException>(()=>SubwayStateWire.DecodeComplete(packet.AsSpan(6)));
        // The old first state consumes eight bytes; its next two zeroes end 03.
        Assert.Equal("0000",Convert.ToHexString(packet.AsSpan(14,2)));
        Assert.Equal("0400",Convert.ToHexString(packet.AsSpan(16,2)));
    }

    [Fact]
    public void BatchedCanonicalStatesHaveOneOuterTerminatorAndConsumeEveryByte()
    {
        var data=new ClientData();var packet=new WorldPacket(data);
        foreach(byte state in new byte[]{0xbe,0xb9})
            packet.addObjectContent(new SequencedMessage(new byte[]{4,0}.Concat(Subway.BuildStateUpdate(state)).ToArray()));
        byte[] plain=packet.getFinalData(data);int start=(plain[0]&0x80)!=0?6:2;
        var decoded=SubwayStateWire.DecodeComplete(plain.AsSpan(start));
        Assert.Equal(new[]{new SubwayStateWire.State(4,0xbbe),new SubwayStateWire.State(4,0xbb9)},decoded);
        Assert.Equal(18,plain.Length-start);
    }

    [Theory]
    [InlineData(0xb9)][InlineData(0xba)][InlineData(0xbb)]
    [InlineData(0xbc)][InlineData(0xbd)][InlineData(0xbe)]
    public void StatePayloadUsesTheDeclaredClass4209Attribute(int state)
    {
        var obj=new AttributeClass4209("Subway Car Default",6568);
        obj.CurrentState.enable();obj.CurrentState.setValue(BitConverter.GetBytes((uint)(0xb00|state)));
        Assert.Equal(obj.GetUpdateAttributes(new(){obj.CurrentState}),Subway.BuildStateUpdate((byte)state));
        Assert.Equal(6,Subway.BuildStateUpdate((byte)state).Length);
    }

    [Fact]
    public void DeparturePublishesOnceWithoutChangingTheCycle()
    {
        var car=new Subway(new StaticWorldObject{quat="0000000000000000000000000000803f"});
        car.currentState=Subway.SUBWAY_STATE.ACTIVATE_INTERACTION;car.stateByte=0xbc;car.secondsLeft=1;
        var published=new List<byte>();void Tick()=>car.AdvanceCountdown(()=>published.Add(car.stateByte));
        Tick();Assert.Equal(new byte[]{0xbd},published);Assert.Equal(3,car.secondsLeft);
        Tick();Tick();Assert.Single(published);Tick();
        Assert.Equal(new byte[]{0xbd,0xbe},published);Assert.Equal(5,car.secondsLeft);
        for(int i=0;i<5;i++)Tick();
        Assert.Equal(new byte[]{0xbd,0xbe,0xb9},published);Assert.Equal(3,car.secondsLeft);
    }
}
