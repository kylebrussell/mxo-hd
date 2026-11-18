using System.Text.Json;
using HeadlessClient;
namespace hds.Tests;
public class ZionAfter1212ActorRouteTests
{
    static MissionNpcInteriorProof.Plan Plan(string role="Npc:7:0")
    {
        string json=JsonSerializer.Serialize(new {Actors=new[]{new {Role="Npc:6:0",Nodes=new[]{1}},new {Role=role,Nodes=new[]{2}}},Selection=new {Actors=new[]{new {Kind="Npc",Id=6,Index=0},new {Kind="Npc",Id=7,Index=0}}},Doors=new[]{99}});
        using var d=JsonDocument.Parse(json);
        return new("owner",1,2,"site","raw",d.RootElement.Clone(),null!,null!);
    }
    [Fact] public void LaterActorUsesItsExactProofAndRoleWithoutMutatingOriginalWitness()
    {
        var p=Plan();string before=p.Witness.GetRawText();var next=ZionAfter1212ActorRoute.Select(p,1);
        Assert.Equal("Npc:7:0",next.Witness.GetProperty("Actors")[0].GetProperty("Role").GetString());
        Assert.Equal(2,next.Witness.GetProperty("Actors")[0].GetProperty("Nodes")[0].GetInt32());
        Assert.Equal(7,next.Witness.GetProperty("Selection").GetProperty("Actors")[0].GetProperty("Id").GetInt32());
        Assert.Equal(99,next.Witness.GetProperty("Doors")[0].GetInt32());Assert.Equal(before,p.Witness.GetRawText());
        Assert.Equal(p.Identity,next.Identity);Assert.Equal(p.Site,next.Site);Assert.Equal(p.Raw,next.Raw);
    }
    [Theory] [InlineData(-1)] [InlineData(2)]
    public void InvalidActorIndexRefuses(int index)=>Assert.Throws<InvalidDataException>(()=>ZionAfter1212ActorRoute.Select(Plan(),index));
    [Fact] public void WrongActorRoleDoesNotBorrowAnotherActorsTopology()=>Assert.Throws<InvalidDataException>(()=>ZionAfter1212ActorRoute.Select(Plan("Npc:6:0"),1));
}
