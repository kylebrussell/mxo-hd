using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using HeadlessClient;
using hds;
using Xunit;
public class OpenWorldCombatFramesTests
{
    const string Literal="0100022E001B000100000000C0F2D24000000000000886C0000000002097ED400100000003000000020001000500030000";
    static Dictionary<ushort,OpenWorldCombatFrames.View> Views()=>new(){[2]=new(12,1,true),[5]=new(599,3,true),[27]=new(55,4,true)};
    static OpenWorldCombatFrames Reader(Dictionary<ushort,OpenWorldCombatFrames.View>? views=null)=>new(id=>(views??Views()).GetValueOrDefault(id),0x00010002);
    static byte[] Centre=>new[]{19403d,-705,60601}.SelectMany(BitConverter.GetBytes).ToArray();
    static byte[] OnView(byte[] body)=>BitConverter.GetBytes((ushort)27).Concat(body).ToArray();
    static byte[] Init()=>ServerPackets.BuildCombatInitialize(27,Centre,0x00010002,0x00030005);
    static byte[] Delete=>Convert.FromHexString("01000101001B00");
    static byte[] Handoff=>ServerPackets.BuildSelfPositionHandoff(1,19403,-705,60601,0xffffffff);
    [Fact]public void LiteralAndActualProducerHaveTheClosedSizedInitializeShape()
    {
        Assert.Equal(49,Convert.FromHexString(Literal).Length);var reader=Reader();Assert.True(reader.TryRead(Convert.FromHexString(Literal),0,out var frame));Assert.Equal(49,frame.Length);Assert.Equal("initialize",frame.Kind);
        var entry=new CombatRoundEntry();var produced=ServerPackets.BuildCombatInitialize(27,Centre,0x00010002,0x00030005,entry);Assert.True(Reader().TryRead(produced,0,out var withEntry));Assert.Equal(170,withEntry.Length);
    }
    [Fact]public void KnownCombatRoundFinalPositionDeleteAndOwnHandoffAreScoped()
    {
        var reader=Reader();Assert.True(reader.TryRead(Init(),0,out _));var entry=new CombatRoundEntry{FirstOffset=new[]{1f,2,3},SecondOffset=new[]{4f,5,6}};
        Assert.True(reader.TryRead(OnView(ServerPackets.BuildCombatViewRound(Centre,uint.MaxValue,entry)),0,out var round));Assert.Equal(157,round.Length);
        Assert.True(reader.TryRead(OnView(ServerPackets.BuildCombatViewFinalRecord(Centre,uint.MaxValue)),0,out var final));Assert.Equal(40,final.Length);
        Assert.True(reader.TryRead(OnView(ServerPackets.BuildCombatViewAttackerPosition(Centre)),0,out var position));Assert.Equal(28,position.Length);
        Assert.Throws<InvalidDataException>(()=>reader.TryRead(Handoff,0,out _));Assert.True(reader.TryRead(Delete,0,out var deleted));Assert.Equal((ushort)27,deleted.DeletedView);
        Assert.True(reader.TryRead(Handoff,0,out var handoff));Assert.Equal(21,handoff.Length);Assert.Throws<InvalidDataException>(()=>reader.TryRead(Handoff,0,out _));Assert.False(reader.TryRead(OnView(ServerPackets.BuildCombatViewRound(Centre,2,entry)),0,out _));
    }
    [Fact]public void UnknownSizedTruncatedMasksEntriesAndForeignHandlesNeverConsume()
    {
        var initial=Init();for(int length=0;length<initial.Length;length++){bool accepted=false;try{accepted=Reader().TryRead(initial[..length],0,out _);}catch(InvalidDataException){}Assert.False(accepted);}
        foreach(int offset in new[]{3,7,36,48}){var bad=(byte[])initial.Clone();bad[offset]=0xff;Assert.Throws<InvalidDataException>(()=>Reader().TryRead(bad,0,out _));}
        var views=Views();views.Remove(5);Assert.Throws<InvalidDataException>(()=>Reader(views).TryRead(initial,0,out _));views=Views();views[5]=new(599,8,true);Assert.Throws<InvalidDataException>(()=>Reader(views).TryRead(initial,0,out _));views=Views();views[27]=new(599,4,true);Assert.Throws<InvalidDataException>(()=>Reader(views).TryRead(initial,0,out _));
        var reader=Reader();reader.TryRead(initial,0,out _);Assert.Throws<InvalidDataException>(()=>reader.TryRead(OnView(ServerPackets.BuildCombatViewRound(Centre,2,new CombatRoundEntry{SecondSlot=3})),0,out _));
    }
    [Fact]public void BirthRetirementDoesNotAuthorizeReuseOrForeignOwnHandoff()
    {
        var views=Views();var reader=Reader(views);reader.TryRead(Init(),0,out _);reader.TryRead(Delete,0,out _);Assert.Throws<InvalidDataException>(()=>reader.TryRead(Init(),0,out _));
        views[27]=new(55,5,true);Assert.True(reader.TryRead(Init(),0,out _));views[5]=new(599,6,true);Assert.Throws<InvalidDataException>(()=>reader.TryRead(OnView(ServerPackets.BuildCombatViewRound(Centre,2,new CombatRoundEntry())),0,out _));
        var observer=Views();observer[8]=new(12,2,true);var foreign=Reader(observer);foreign.TryRead(ServerPackets.BuildCombatInitialize(27,Centre,0x00020008,0x00030005),0,out _);foreign.TryRead(Delete,0,out _);Assert.Throws<InvalidDataException>(()=>foreign.TryRead(Handoff,0,out _));
    }
    [Fact]public void All192RetainedSectionsKeepThePreexistingSecondRunnerCurrent()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        using var raw=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"tests/hds.Tests/Fixtures/OpenWorldCombatViews-6056c89fea.json")));var log=new OwnViewLifeLog();int count=0;
        foreach(var row in raw.RootElement.GetProperty("Views").EnumerateArray()){byte[] bytes=Convert.FromHexString(row.GetString()!);log.Observe(false,bytes,count++,WireFormat.FindCreations(bytes));}
        Assert.Equal(192,count);var current=OpenWorldCurrentNpc.Require(log.Snapshot(),20097,-705,61491);Assert.Equal((ushort)22,current.View.ViewId);Assert.Equal((byte)20,current.View.SpawnId);Assert.Equal(new[]{20592.46484375,-705,61558.19140625},current.Position);
    }
}
